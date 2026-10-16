using System.Runtime.InteropServices;

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
    private bool _activeIsHook;
    private bool _lastDown;

    internal HotkeySource(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    /// <summary>注册成功时触发；回调在消息线程上。</summary>
    internal event Action? Triggered;

    /// <summary>当前实际生效的方式：注册表或键盘钩子。</summary>
    internal bool UsingHook => _useHook;

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
    /// 尝试注册。失败时不改变当前已生效的热键。
    /// </summary>
    internal bool TryRegister(HotkeySpec spec, out string error)
    {
        error = string.Empty;

        if (_spec is not null && _spec.Equals(spec))
        {
            return true;
        }

        // 先注册新的，成功后再撤旧的：新键失败不会把旧键弄丢
        if (Native.RegisterHotKey(_hwnd, HotkeyId, spec.Modifiers | Native.MOD_NOREPEAT, spec.VirtualKey))
        {
            UnregisterCurrent();
            _spec = spec;
            _useHook = false;
            _activeIsHook = false;
            Log.Info($"热键已注册：{spec.Text}（RegisterHotKey）");
            return true;
        }

        int err = Marshal.GetLastWin32Error();
        Log.Debug($"RegisterHotKey 失败，Win32 错误码 {err}，改用键盘钩子");

        if (TryRegisterHook(spec, out string hookError))
        {
            UnregisterCurrent();
            _spec = spec;
            _useHook = true;
            _activeIsHook = true;
            Log.Info($"热键已注册：{spec.Text}（低级键盘钩子，该组合被其他程序占用）");
            return true;
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

    private void UnregisterCurrent()
    {
        if (_activeIsHook)
        {
            if (_hook != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }

            _proc = null;
            _hookSpec = null;
            _lastDown = false;
            _activeIsHook = false;
        }
        else
        {
            Native.UnregisterHotKey(_hwnd, HotkeyId);
            _activeIsHook = false;
        }
    }

    /// <summary>彻底清空当前注册，用于“暂停热键”。</summary>
    internal void Reset()
    {
        UnregisterCurrent();
        _activeIsHook = false;
        _useHook = false;
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
                        Log.Debug($"热键触发（键盘钩子）{_hookSpec}");
                        Triggered?.Invoke();
                    }

                    // 吞掉按键，避免该组合继续传给当前窗口
                    return 1;
                }
            }
            else if (message is 0x0101 or 0x0105) // WM_KEYUP / WM_SYSKEYUP
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
