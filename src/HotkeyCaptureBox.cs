using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowMinimize;

/// <summary>
/// A read-only hotkey field that captures the next key combination through a low-level keyboard
/// hook, so Windows and modified keys are captured too.
/// </summary>
internal sealed class HotkeyCaptureBox : TextBox {
    private readonly string _prompt;
    private Native.LowLevelKeyboardProc? _hookProc;
    private IntPtr _hook;
    private string _previous = string.Empty;
    private bool _capturing;

    internal HotkeyCaptureBox(string text, string prompt) {
        _prompt = prompt;
        Text = text;
        ReadOnly = true;
        Cursor = Cursors.Hand;
        ShortcutsEnabled = false;
        Margin = new Padding(0, 3, 0, 3);
    }

    internal event Action<string>? CaptureRejected;

    internal event Action? CaptureAccepted;

    protected override void OnEnter(EventArgs e) {
        base.OnEnter(e);
        BeginCapture();
    }

    protected override void OnLeave(EventArgs e) {
        EndCapture(keep: false);
        base.OnLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        if (!_capturing) BeginCapture();
    }

    internal void CancelCapture() => EndCapture(keep: false);

    private void BeginCapture() {
        if (_capturing || IsDisposed) return;

        _capturing = true;
        _previous = Text;
        Text = _prompt;
        ForeColor = SystemColors.GrayText;
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

        if (!down) return new IntPtr(1);

        if (IsModifier(vk)) return new IntPtr(1);

        if (vk == 0x1B) {
            EndCapture(keep: false);
            return new IntPtr(1);
        }

        uint modifiers = CurrentModifiers();
        if ((vk == 0x08 || vk == 0x2E) && modifiers == 0) {
            _previous = string.Empty;
            EndCapture(keep: true);
            return new IntPtr(1);
        }

        if (HotkeySpec.TryCreate(modifiers, vk, out HotkeySpec? spec, out string error)) {
            Text = spec!.Text;
            CaptureAccepted?.Invoke();
            EndCapture(keep: true);
        } else {
            Text = _prompt;
            CaptureRejected?.Invoke(error);
        }

        return new IntPtr(1);
    }

    private static bool IsModifier(uint vk) => vk is
        0x10 or 0x11 or 0x12 or 0x5B or 0x5C;

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
