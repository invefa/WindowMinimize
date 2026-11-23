using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowMinimize;

/// <summary>
/// A read-only hotkey field that listens only while it has focus and the dialog is active. The
/// current text stays untouched until a complete combination is pressed.
/// </summary>
internal sealed class HotkeyCaptureBox : TextBox {
    private Native.LowLevelKeyboardProc? _hookProc;
    private IntPtr _hook;
    private string _previous = string.Empty;
    private uint _modifiers;
    private bool _capturing;
    private bool _resumeOnActivate;

    internal HotkeyCaptureBox(string text, string prompt) {
        Text = text;
        PlaceholderText = prompt;
        ReadOnly = true;
        BackColor = SystemColors.Window;
        ForeColor = SystemColors.WindowText;
        Cursor = Cursors.IBeam;
        ShortcutsEnabled = false;
        Margin = new Padding(0, 3, 0, 3);
    }

    internal event Action<string>? CaptureRejected;

    internal event Action? CaptureAccepted;

    /// <summary>
    /// Enabled once the dialog is shown, so the automatic focus while the dialog opens does not
    /// start listening behind the user's back.
    /// </summary>
    internal bool AllowCapture { get; set; }

    internal void ClearValue() {
        if (_capturing) EndCapture(keep: true);
        _previous = string.Empty;
        Text = string.Empty;
        ForeColor = SystemColors.WindowText;
    }

    /// <summary>Stop listening and put the previous value back.</summary>
    internal void CancelCapture() {
        _resumeOnActivate = false;
        EndCapture(keep: false);
    }

    /// <summary>Stop listening while the dialog is inactive, remembering to resume later.</summary>
    internal void SuspendCapture() {
        bool wasCapturing = _capturing;
        EndCapture(keep: false);
        _resumeOnActivate = wasCapturing;
    }

    /// <summary>Resume listening when the dialog becomes active again with this field focused.</summary>
    internal void ResumeCapture() {
        bool resume = _resumeOnActivate;
        _resumeOnActivate = false;
        if (resume && Focused) BeginCapture();
    }

    protected override void OnGotFocus(EventArgs e) {
        base.OnGotFocus(e);
        if (AllowCapture) BeginCapture();
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        AllowCapture = true;
        BeginCapture();
    }

    protected override void OnLeave(EventArgs e) {
        _resumeOnActivate = false;
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
            _resumeOnActivate = false;
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
