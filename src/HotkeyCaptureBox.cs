using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowMinimize;

/// <summary>
/// A read-only hotkey field that listens while it has focus, without replacing its current text
/// until a complete combination is pressed.
/// </summary>
internal sealed class HotkeyCaptureBox : TextBox {
    private Native.LowLevelKeyboardProc? _hookProc;
    private IntPtr _hook;
    private string _previous = string.Empty;
    private uint _modifiers;
    private bool _capturing;

    internal HotkeyCaptureBox(string text, string prompt) {
        Text = text;
        PlaceholderText = prompt;
        ReadOnly = true;
        BackColor = SystemColors.Window;
        ForeColor = SystemColors.WindowText;
        Cursor = Cursors.Hand;
        ShortcutsEnabled = false;
        Margin = new Padding(0, 3, 0, 3);
    }

    internal event Action<string>? CaptureRejected;

    internal event Action? CaptureAccepted;

    internal void ClearValue() {
        if (_capturing) EndCapture(keep: true);
        _previous = string.Empty;
        Text = string.Empty;
        ForeColor = SystemColors.WindowText;
    }

    internal void CancelCapture() => EndCapture(keep: false);

    internal void ResumeCapture() {
        if (Focused) BeginCapture();
    }

    protected override void OnEnter(EventArgs e) {
        base.OnEnter(e);
        BeginCapture();
    }

    protected override void OnLeave(EventArgs e) {
        EndCapture(keep: false);
        base.OnLeave(e);
    }

    private void BeginCapture() {
        if (_capturing || IsDisposed || !Focused) return;

        _capturing = true;
        _previous = Text;
        _modifiers = CurrentModifiers();
        _hookProc = HookProc;
        _hook = Native.SetWindowsHookEx(
            Native.WH_KEYBOARD_LL, _hookProc, Native.GetModuleHandle(null), 0
        );
        if (_hook == IntPtr.Zero) {
            EndCapture(keep: false);
            CaptureRejected?.Invoke(Strings.ErrorHookInstallFailed(
                Marshal.GetLastWin32Error().ToString()
            ));
        }
    }

    private void EndCapture(bool keep) {
        if (!_capturing) return;
        _capturing = false;

        if (_hook != IntPtr.Zero) {
            Native.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        _hookProc = null;
        _modifiers = 0;
        if (!keep) Text = _previous;
        ForeColor = SystemColors.WindowText;
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam) {
        if (nCode < 0 || !_capturing) {
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        int message = wParam.ToInt32();
        bool down = message is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN;
        bool up = message is Native.WM_KEYUP or Native.WM_SYSKEYUP;
        if (!down && !up) return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

        var info = Marshal.PtrToStructure<Native.KbdLlHookStruct>(lParam);
        uint vk = info.VkCode;

        if (IsModifier(vk)) {
            UpdateModifier(vk, down);
            return new IntPtr(1);
        }

        if (!down) return new IntPtr(1);

        if (vk == 0x1B) {
            EndCapture(keep: false);
            return new IntPtr(1);
        }

        uint modifiers = _modifiers | CurrentModifiers();
        if ((vk == 0x08 || vk == 0x2E) && modifiers == 0) {
            _previous = string.Empty;
            Text = string.Empty;
            return new IntPtr(1);
        }

        if (HotkeySpec.TryCreate(modifiers, vk, out HotkeySpec? spec, out string error)) {
            _previous = spec!.Text;
            Text = spec.Text;
            CaptureAccepted?.Invoke();
        } else {
            CaptureRejected?.Invoke(error);
        }

        return new IntPtr(1);
    }

    private static bool IsModifier(uint vk) => vk is
        0x10 or 0x11 or 0x12 or 0x5B or 0x5C
        or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;

    private void UpdateModifier(uint vk, bool down) {
        uint bit = vk switch {
            0x10 or 0xA0 or 0xA1 => Native.MOD_SHIFT,
            0x11 or 0xA2 or 0xA3 => Native.MOD_CONTROL,
            0x12 or 0xA4 or 0xA5 => Native.MOD_ALT,
            0x5B or 0x5C => Native.MOD_WIN,
            _ => 0,
        };

        if (down) _modifiers |= bit;
        else _modifiers &= ~bit;
    }

    private static uint CurrentModifiers() {
        uint modifiers = 0;
        if (IsDown(0x11)) modifiers |= Native.MOD_CONTROL;
        if (IsDown(0x12)) modifiers |= Native.MOD_ALT;
        if (IsDown(0x10)) modifiers |= Native.MOD_SHIFT;
        if (IsDown(0x5B) || IsDown(0x5C)) modifiers |= Native.MOD_WIN;
        return modifiers;
    }

    private static bool IsDown(int vk) => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;

    protected override void Dispose(bool disposing) {
        if (disposing) EndCapture(keep: false);
        base.Dispose(disposing);
    }
}
