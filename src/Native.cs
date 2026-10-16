using System.Runtime.InteropServices;

namespace WindowMinimize;

/// <summary>
/// Every Win32 call this tool uses, collected in one place.
/// </summary>
internal static class Native {
    internal const int WM_HOTKEY = 0x0312;
    internal const int WM_SYSCOMMAND = 0x0112;
    internal const int SC_MINIMIZE = 0xF020;

    internal const uint MOD_ALT = 0x0001;
    internal const uint MOD_CONTROL = 0x0002;
    internal const uint MOD_SHIFT = 0x0004;
    internal const uint MOD_WIN = 0x0008;
    internal const uint MOD_NOREPEAT = 0x4000;

    internal const int SW_HIDE = 0;
    internal const int SW_SHOWMINIMIZED = 2;
    internal const int SW_SHOW = 5;
    internal const int SW_MINIMIZE = 6;
    internal const int SW_RESTORE = 9;
    internal const int SW_FORCEMINIMIZE = 11;

    internal const int DWMWA_CLOAKED = 14;

    internal const uint GA_ROOT = 2;

    internal const int SMTO_ABORTIFHUNG = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    [DllImport("user32.dll")]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, int flags, int timeout,
        out IntPtr result
    );

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(
        IntPtr hWnd, System.Text.StringBuilder name, int maxCount
    );

    [DllImport("user32.dll")]
    internal static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    internal static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetWindowText(
        IntPtr hWnd, System.Text.StringBuilder text, int maxCount
    );

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(
        IntPtr hWnd, int attribute, out int value, int size
    );

    // low level keyboard hook
    internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    internal const int WH_KEYBOARD_LL = 13;

    internal const int WM_KEYDOWN = 0x0100;
    internal const int WM_SYSKEYDOWN = 0x0104;
    internal const int WM_KEYUP = 0x0101;
    internal const int WM_SYSKEYUP = 0x0105;

    [StructLayout(LayoutKind.Sequential)]
    internal struct KbdLlHookStruct {
        internal uint VkCode;
        internal uint ScanCode;
        internal uint Flags;
        internal uint Time;
        internal IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(
        int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId
    );

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(
        IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam
    );

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);

    /// <summary>
    /// Synthesize one key press, used to tap the mask key after a hotkey swallowed a key.
    /// </summary>
    [DllImport("user32.dll")]
    internal static extern void keybd_event(
        byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo
    );

    internal const uint KEYEVENTF_KEYUP = 0x0002;

    internal const byte VK_SHIFT = 0x10;
    internal const byte VK_CONTROL = 0x11;

    /// <summary>
    /// A suspended UWP window is only cloaked by DWM and still counts as not minimized, so a plain
    /// minimize is ignored and it needs special handling.
    /// </summary>
    internal static bool IsCloaked(IntPtr hWnd) {
        try {
            return DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0
                && cloaked != 0;
        } catch (DllNotFoundException) {
            return false;
        } catch (EntryPointNotFoundException) {
            return false;
        }
    }

    /// <summary>Window title, used by the log only.</summary>
    internal static string GetWindowTitle(IntPtr hWnd) {
        try {
            int len = GetWindowTextLength(hWnd);
            if (len <= 0) return string.Empty;

            var sb = new System.Text.StringBuilder(len + 2);
            int copied = GetWindowText(hWnd, sb, sb.Capacity);
            return copied > 0 ? sb.ToString() : string.Empty;
        } catch {
            return string.Empty;
        }
    }

    /// <summary>
    /// Window class name, used to recognize the shell surfaces such as the taskbar, the desktop and
    /// the start menu.
    /// </summary>
    internal static string GetWindowClassName(IntPtr hWnd) {
        try {
            var sb = new System.Text.StringBuilder(256);
            return GetClassName(hWnd, sb, sb.Capacity) > 0 ? sb.ToString() : string.Empty;
        } catch {
            return string.Empty;
        }
    }

    /// <summary>
    /// Process name of the window, empty when it cannot be read. Used to recognize the shell
    /// processes such as the start menu and search, which use the CoreWindow class name too.
    /// </summary>
    internal static string GetProcessName(IntPtr hWnd) {
        try {
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0) return string.Empty;

            using System.Diagnostics.Process process =
                System.Diagnostics.Process.GetProcessById((int)pid);
            return process.ProcessName;
        } catch {
            return string.Empty;
        }
    }

    /// <summary>
    /// Whether the window belongs to this process. While a tray menu is open the foreground window
    /// is the menu window of this process.
    /// </summary>
    internal static bool IsOwnProcessWindow(IntPtr hWnd) {
        try {
            GetWindowThreadProcessId(hWnd, out uint pid);
            return pid == (uint)Environment.ProcessId;
        } catch {
            return false;
        }
    }
}
