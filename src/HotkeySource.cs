using System.Runtime.InteropServices;
using System.Threading;

namespace WindowMinimize;

/// <summary>
/// How the global hotkey is registered: RegisterHotKey first, and when another program already owns
/// that combination it falls back to a low level keyboard hook, which swallows the key before
/// anyone else sees it.
/// </summary>
internal sealed class HotkeySource : IDisposable {
    private const int HotkeyId = 0x574D; // 'WM'

    private readonly IntPtr _hwnd;
    private Native.LowLevelKeyboardProc? _proc;
    private IntPtr _hook;
    private HotkeySpec? _spec;
    private HotkeySpec? _hookSpec;
    private bool _useHook;
    private bool _lastDown;

    internal HotkeySource(IntPtr hwnd) {
        _hwnd = hwnd;
    }

    /// <summary>Raised on a successful trigger, the callback runs on the message thread.</summary>
    internal event Action? Triggered;

    /// <summary>Which way is actually in use: RegisterHotKey or the keyboard hook.</summary>
    internal bool UsingHook => _useHook;

    /// <summary>
    /// Whether the keyboard hook is in use because Windows itself owns the combination, Win+S to
    /// search for example. Taking such a combination over stops the system feature from working, so
    /// the message has to say so.
    /// </summary>
    internal bool HookIsWindowsOwned { get; private set; }

    internal HotkeySpec? Spec => _spec;

    /// <summary>
    /// Used by the message window to forward messages, true when it was the hotkey of this
    /// registration.
    /// </summary>
    internal bool HandleMessage(int msg, IntPtr wParam) {
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == HotkeyId) {
            Log.Debug($"hotkey triggered (RegisterHotKey) {_spec}");
            Triggered?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Try to register. When the new combination cannot be used at all the previous one is
    /// restored, so a hotkey that still works is never lost.
    /// </summary>
    internal bool TryRegister(HotkeySpec spec, out string error) {
        error = string.Empty;

        if (_spec is not null && _spec.Equals(spec)) return true;

        // RegisterHotKey entries are unique per window and id, so registering the same id again
        // just replaces the combination. The old one is removed first and restored below when the
        // new one turns out to be unusable.
        HotkeySpec? previous = _spec;
        UnregisterCurrent();
        _spec = null;

        bool registered = Native.RegisterHotKey(
            _hwnd, HotkeyId, spec.Modifiers | Native.MOD_NOREPEAT, spec.VirtualKey
        );
        if (registered) {
            _spec              = spec;
            _useHook           = false;
            HookIsWindowsOwned = false;
            Log.Info($"hotkey registered: {spec.Text} (RegisterHotKey)");
            return true;
        }

        int err = Marshal.GetLastWin32Error();
        Log.Debug($"RegisterHotKey failed with Win32 error {err}, using the keyboard hook instead");

        if (TryRegisterHook(spec, out string hookError)) {
            _spec = spec;
            _useHook = true;
            HookIsWindowsOwned = HotkeySpec.IsWindowsOwnedWinCombo(
                spec.Modifiers, spec.VirtualKey
            );
            string owner = HookIsWindowsOwned
                ? "Windows owns that combination and this program took it over"
                : "another program owns that combination";
            Log.Info($"hotkey registered: {spec.Text} (low level keyboard hook, {owner})");
            return true;
        }

        if (previous is not null && !RestorePrevious(previous))
            Log.Error($"the previous hotkey {previous.Text} could not be restored either, "
                + "no hotkey is active now");

        error = err == 1409
            ? $"combination {spec.Text} is already registered by another program (Win32 1409)"
            : $"registering {spec.Text} failed with Win32 error {err}";
        if (hookError.Length > 0) error += $"; the fallback key hook failed too: {hookError}";

        return false;
    }

    /// <summary>
    /// Restore the last hotkey that worked, in the order of a normal registration.
    /// </summary>
    private bool RestorePrevious(HotkeySpec previous) {
        bool registered = Native.RegisterHotKey(
            _hwnd, HotkeyId, previous.Modifiers | Native.MOD_NOREPEAT, previous.VirtualKey
        );
        if (registered) {
            _spec              = previous;
            _useHook           = false;
            HookIsWindowsOwned = false;
            Log.Info($"previous hotkey restored: {previous.Text} (RegisterHotKey)");
            return true;
        }

        if (TryRegisterHook(previous, out _)) {
            _spec = previous;
            _useHook = true;
            HookIsWindowsOwned = HotkeySpec.IsWindowsOwnedWinCombo(
                previous.Modifiers, previous.VirtualKey
            );
            Log.Info($"previous hotkey restored: {previous.Text} (low level keyboard hook)");
            return true;
        }

        return false;
    }

    private void UnregisterCurrent() {
        if (_useHook) {
            if (_hook != IntPtr.Zero) {
                Native.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }

            _proc     = null;
            _hookSpec = null;
            _lastDown = false;
            _useHook  = false;
            return;
        }

        Native.UnregisterHotKey(_hwnd, HotkeyId);
    }

    /// <summary>Clear the current registration completely, used by Suspend hotkey.</summary>
    internal void Reset() {
        UnregisterCurrent();
        _useHook           = false;
        HookIsWindowsOwned = false;
        _spec              = null;
    }

    private bool TryRegisterHook(HotkeySpec spec, out string error) {
        error = string.Empty;
        _hookSpec = spec;

        _proc         = HookProc;
        IntPtr module = Native.GetModuleHandle(null);
        _hook         = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc, module, 0);
        if (_hook == IntPtr.Zero) {
            int err = Marshal.GetLastWin32Error();
            _proc     = null;
            _hookSpec = null;
            error     = $"SetWindowsHookEx failed with Win32 error {err}";
            return false;
        }

        return true;
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam) {
        if (nCode >= 0 && _hookSpec is not null) {
            int message = wParam.ToInt32();
            if (message is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN) {
                var info = Marshal.PtrToStructure<Native.KbdLlHookStruct>(lParam);
                if (info.VkCode == _hookSpec.VirtualKey && ModifiersMatch(_hookSpec.Modifiers)) {
                    if (!_lastDown) {
                        _lastDown = true;

                        // the key is about to be swallowed, so the system sees no other key while
                        // Win (or Alt) is held down and releasing Win is read as a single Win press
                        // that pops the start panel, hence the mask key below
                        QueueMenuMask(_hookSpec.Modifiers);

                        Log.Debug($"hotkey triggered (keyboard hook) {_hookSpec}");
                        Triggered?.Invoke();
                    }

                    // swallow the key so the combination does not reach the window in front
                    return 1;
                }
            } else if (message is Native.WM_KEYUP or Native.WM_SYSKEYUP) {
                var info = Marshal.PtrToStructure<Native.KbdLlHookStruct>(lParam);
                if (info.VkCode == _hookSpec.VirtualKey) _lastDown = false;
            }
        }

        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    /// <summary>
    /// Tap one more modifier key. After a key is swallowed the system sees no key at all while Win
    /// (or Alt) is held down, so releasing Win counts as a single Win press and opens the start
    /// panel. Tapping another modifier makes the system think something was pressed in between, and
    /// then the release order does not matter. Only Win and Alt behave this way, so other
    /// combinations get no tap.
    /// </summary>
    private void QueueMenuMask(uint modifiers) {
        if ((modifiers & (Native.MOD_WIN | Native.MOD_ALT)) == 0) return;

        byte mask;
        if (!IsDown(Native.VK_CONTROL)) {
            mask = Native.VK_CONTROL;
        } else if (!IsDown(Native.VK_SHIFT)) {
            mask = Native.VK_SHIFT;
        } else {
            // both candidate mask keys are held down, nothing can be tapped, leave it as it is
            return;
        }

        // a key cannot be synthesized inside a keyboard hook callback, hand it to the thread pool
        ThreadPool.QueueUserWorkItem(_ => {
            Log.Debug($"tapping mask key 0x{mask:X2} so releasing Win is not a Win click");
            Native.keybd_event(mask, 0, 0, UIntPtr.Zero);
            Native.keybd_event(mask, 0, Native.KEYEVENTF_KEYUP, UIntPtr.Zero);
        });
    }

    private static bool ModifiersMatch(uint modifiers) {
        if (IsDown(0x11) != ((modifiers & Native.MOD_CONTROL) != 0)) return false;
        if (IsDown(0x12) != ((modifiers & Native.MOD_ALT) != 0)) return false;
        if (IsDown(0x10) != ((modifiers & Native.MOD_SHIFT) != 0)) return false;

        bool win = IsDown(0x5B) || IsDown(0x5C);
        return win == ((modifiers & Native.MOD_WIN) != 0);
    }

    private static bool IsDown(int vk) => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;

    public void Dispose() {
        UnregisterCurrent();
        _spec = null;
    }
}
