using System.Runtime.InteropServices;
using System.Threading;

namespace WindowMinimize;

/// <summary>
/// 全局热键的注册方式：先用系统 RegisterHotKey；若该组合已被别的程序占用，
/// 自动退回到低级键盘钩子（覆盖式抢键，别人收不到该按键）。
/// </summary>
internal sealed class HotkeySource : IDisposable
{
    private const int HotkeyId = 0x574D; // 'WM'

    private readonly IntPtr _hwnd;
    private Native.LowLevelKeyboardProc? _proc;
    private IntPtr _hook;
    private HotkeySpec? _spec;
    private bool _useHook;
    private bool _lastDown;

    internal HotkeySource(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    /// <summary>注册成功时触发；回调在消息线程上。</summary>
    internal event Action? Triggered;

    /// <summary>当前实际生效的方式：注册表或键盘钩子。</summary>
    internal bool UsingHook => _useHook;

    /// <summary>
    /// 走键盘钩子是否是因为组合被 Windows 自己占用（例如 Win+S 打开搜索）。
    /// 这种情况接管后系统原有功能不再可用，提示语要说明清楚。
    /// </summary>
    internal bool HookIsWindowsOwned { get; private set; }

    internal HotkeySpec? Spec => _spec;

    /// <summary>供消息窗口转发消息用：是本次注册的热键消息则返回 true。</summary>
    internal bool HandleMessage(int msg, IntPtr wParam)
    {
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Log.Debug($"热键触发（RegisterHotKey）{_spec}");
            Triggered?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 尝试注册。新组合完全不可用时恢复原来的组合，不会把还能用的热键弄丢。
    /// </summary>
    internal bool TryRegister(HotkeySpec spec, out string error)
    {
        error = string.Empty;

        if (_spec is not null && _spec.Equals(spec))
        {
            return true;
        }

        // RegisterHotKey 的注册项按“窗口 + id”唯一，同一个 id 直接再注册等于换成新组合，
        // 所以要先把旧的撤掉；万一新组合用不了，下面再把旧的恢复回来。
        HotkeySpec? previous = _spec;
        UnregisterCurrent();
        _spec = null;

        if (Native.RegisterHotKey(_hwnd, HotkeyId, spec.Modifiers | Native.MOD_NOREPEAT, spec.VirtualKey))
        {
            _spec = spec;
            _useHook = false;
            HookIsWindowsOwned = false;
            Log.Info($"热键已注册：{spec.Text}（RegisterHotKey）");
            return true;
        }

        int err = Marshal.GetLastWin32Error();
        Log.Debug($"RegisterHotKey 失败，Win32 错误码 {err}，改用键盘钩子");

        if (TryRegisterHook(spec, out string hookError))
        {
            _spec = spec;
            _useHook = true;
            HookIsWindowsOwned = HotkeySpec.IsWindowsOwnedWinCombo(spec.Modifiers, spec.VirtualKey);
            Log.Info(HookIsWindowsOwned
                ? $"热键已注册：{spec.Text}（低级键盘钩子，该组合由 Windows 占用，已被本程序接管）"
                : $"热键已注册：{spec.Text}（低级键盘钩子，该组合被其他程序占用）");
            return true;
        }

        if (previous is not null && !RestorePrevious(previous))
        {
            Log.Error($"原热键 {previous.Text} 也没能恢复，当前没有生效的热键");
        }

        error = err == 1409
            ? $"组合 {spec.Text} 已被其他程序注册（Win32 1409）"
            : $"注册 {spec.Text} 失败，Win32 错误码 {err}";
        if (hookError.Length > 0)
        {
            error += $"；备用按键钩子也失败：{hookError}";
        }

        return false;
    }

    /// <summary>恢复上一次可用的热键，顺序与正常注册一致：先试注册表，再试键盘钩子。</summary>
    private bool RestorePrevious(HotkeySpec previous)
    {
        if (Native.RegisterHotKey(_hwnd, HotkeyId, previous.Modifiers | Native.MOD_NOREPEAT, previous.VirtualKey))
        {
            _spec = previous;
            _useHook = false;
            HookIsWindowsOwned = false;
            Log.Info($"已恢复原热键：{previous.Text}（RegisterHotKey）");
            return true;
        }

        if (TryRegisterHook(previous, out _))
        {
            _spec = previous;
            _useHook = true;
            HookIsWindowsOwned = HotkeySpec.IsWindowsOwnedWinCombo(previous.Modifiers, previous.VirtualKey);
            Log.Info($"已恢复原热键：{previous.Text}（低级键盘钩子）");
            return true;
        }

        return false;
    }

    private void UnregisterCurrent()
    {
        if (_useHook)
        {
            if (_hook != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }

            _proc = null;
            _hookSpec = null;
            _lastDown = false;
            _useHook = false;
            return;
        }

        Native.UnregisterHotKey(_hwnd, HotkeyId);
    }

    /// <summary>彻底清空当前注册，用于“暂停热键”。</summary>
    internal void Reset()
    {
        UnregisterCurrent();
        _useHook = false;
        HookIsWindowsOwned = false;
        _spec = null;
    }

    private bool TryRegisterHook(HotkeySpec spec, out string error)
    {
        error = string.Empty;
        _hookSpec = spec;

        _proc = HookProc;
        IntPtr module = Native.GetModuleHandle(null);
        _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc, module, 0);
        if (_hook == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            _proc = null;
            _hookSpec = null;
            error = $"SetWindowsHookEx 失败，Win32 错误码 {err}";
            return false;
        }

        return true;
    }

    private HotkeySpec? _hookSpec;

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _hookSpec is not null)
        {
            int message = wParam.ToInt32();
            if (message is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN)
            {
                var info = Marshal.PtrToStructure<Native.KbdLlHookStruct>(lParam);
                if (info.VkCode == _hookSpec.VirtualKey && ModifiersMatch(_hookSpec.Modifiers))
                {
                    if (!_lastDown)
                    {
                        _lastDown = true;

                        // 按键马上要被吞掉，系统在这段 Win（或 Alt）按住的时间里就看不到别的按键了，
                        // 松开 Win 会被当成“单独按了一下 Win”而弹出开始面板，所以补一次掩码键。
                        QueueMenuMask(_hookSpec.Modifiers);

                        Log.Debug($"热键触发（键盘钩子）{_hookSpec}");
                        Triggered?.Invoke();
                    }

                    // 吞掉按键，避免该组合继续传给当前窗口
                    return 1;
                }
            }
            else if (message is Native.WM_KEYUP or Native.WM_SYSKEYUP)
            {
                var info = Marshal.PtrToStructure<Native.KbdLlHookStruct>(lParam);
                if (info.VkCode == _hookSpec.VirtualKey)
                {
                    _lastDown = false;
                }
            }
        }

        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <summary>
    /// 补一个掩码键：吞掉按键后，系统在 Win（或 Alt）按住期间一个按键事件都看不到，
    /// 会把松开 Win 当成“单独按了一下 Win”并弹出开始面板。轻敲一次别的修饰键，系统就认为
    /// Win 期间按过别的键，松开顺序怎么来都不会弹。只有 Win 和 Alt 有这种行为，其他组合不补。
    /// </summary>
    private void QueueMenuMask(uint modifiers)
    {
        if ((modifiers & (Native.MOD_WIN | Native.MOD_ALT)) == 0)
        {
            return;
        }

        byte mask;
        if (!IsDown(Native.VK_CONTROL))
        {
            mask = Native.VK_CONTROL;
        }
        else if (!IsDown(Native.VK_SHIFT))
        {
            mask = Native.VK_SHIFT;
        }
        else
        {
            // 两个候选掩码键都正被按着，补不了，只能维持原样
            return;
        }

        // 不能在键盘钩子回调里直接合成按键，丢到线程池去发
        ThreadPool.QueueUserWorkItem(_ =>
        {
            Log.Debug($"补掩码键 0x{mask:X2}，避免松开 Win 被当成单独按 Win");
            Native.keybd_event(mask, 0, 0, UIntPtr.Zero);
            Native.keybd_event(mask, 0, Native.KEYEVENTF_KEYUP, UIntPtr.Zero);
        });
    }

    private static bool ModifiersMatch(uint modifiers)
    {
        if (IsDown(0x11) != ((modifiers & Native.MOD_CONTROL) != 0))
        {
            return false;
        }

        if (IsDown(0x12) != ((modifiers & Native.MOD_ALT) != 0))
        {
            return false;
        }

        if (IsDown(0x10) != ((modifiers & Native.MOD_SHIFT) != 0))
        {
            return false;
        }

        bool win = IsDown(0x5B) || IsDown(0x5C);
        return win == ((modifiers & Native.MOD_WIN) != 0);
    }

    private static bool IsDown(int vk) => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;

    public void Dispose()
    {
        UnregisterCurrent();
        _spec = null;
    }
}
