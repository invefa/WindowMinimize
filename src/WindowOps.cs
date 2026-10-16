using System.Diagnostics;

namespace WindowMinimize;

/// <summary>
/// Finding the window that should be minimized, and minimizing it.
/// </summary>
internal static class WindowOps {
    /// <summary>
    /// Class names of the Windows shell surfaces. They often hold the foreground when a tray menu
    /// is clicked, but they are never what the user wants to minimize. The start menu and search
    /// shell surfaces too, yet they use the same Windows.UI.Core.CoreWindow class as ordinary UWP
    /// apps, so they can only be told apart by process name, see ShellProcessNames.
    /// </summary>
    private static readonly HashSet<string> ShellWindowClasses = new(StringComparer.Ordinal) {
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow",
        "Progman",
        "WorkerW",
        "TaskListThumbnailWnd",
        "MultitaskingViewFrame",
        "ForegroundStaging",
        "XamlExplorerHostIslandWindow",
    };

    private static readonly HashSet<string> ShellProcessNames =
        new(StringComparer.OrdinalIgnoreCase) {
            "StartMenuExperienceHost",
            "SearchHost",
            "SearchApp",
            "ShellExperienceHost",
            "ShellHost",
        };

    /// <summary>
    /// Top level window of the current foreground window, skipping shell and own windows.
    /// </summary>
    internal static IntPtr GetForegroundTarget(IntPtr selfHandle) {
        IntPtr hwnd = Native.GetAncestor(Native.GetForegroundWindow(), Native.GA_ROOT);
        if (hwnd == IntPtr.Zero) hwnd = Native.GetForegroundWindow();

        if (hwnd == IntPtr.Zero || (selfHandle != IntPtr.Zero && hwnd == selfHandle)
            || !Native.IsWindow(hwnd)) {
            return IntPtr.Zero;
        }

        if (!Native.IsWindowVisible(hwnd)) {
            Log.Debug("foreground window is not visible, ignoring this trigger");
            return IntPtr.Zero;
        }

        if (IsShellSurface(hwnd)) {
            string className = Native.GetWindowClassName(hwnd);
            Log.Debug($"foreground window is a shell surface ({className}), ignoring trigger");
            return IntPtr.Zero;
        }

        return hwnd;
    }

    /// <summary>
    /// The taskbar, desktop, start menu, search and our own windows are never targets.
    /// </summary>
    private static bool IsShellSurface(IntPtr hwnd) {
        if (hwnd == Native.GetShellWindow() || hwnd == Native.GetDesktopWindow()) return true;

        if (Native.IsOwnProcessWindow(hwnd)
            || ShellWindowClasses.Contains(Native.GetWindowClassName(hwnd))) return true;

        return ShellProcessNames.Contains(Native.GetProcessName(hwnd));
    }

    /// <summary>Bring back a window that was hidden with the hide mode.</summary>
    internal static bool Restore(IntPtr hwnd) {
        if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd)) return false;

        Native.ShowWindowAsync(hwnd, Native.SW_RESTORE);
        Native.SetForegroundWindow(hwnd);
        Log.Debug($"window restored: hwnd=0x{hwnd.ToInt64():X}");
        return true;
    }

    /// <summary>
    /// Minimize one top level window. Ordinary windows go through SW_MINIMIZE, the ones DWM cloaks
    /// (UWP and friends) get WM_SYSCOMMAND/SC_MINIMIZE plus the foreground fallback.
    /// </summary>
    internal static bool Minimize(IntPtr hwnd, string mode) {
        if (mode.Equals("hide", StringComparison.OrdinalIgnoreCase))
            return Native.ShowWindowAsync(hwnd, Native.SW_HIDE);

        if (Native.IsCloaked(hwnd)) {
            Log.Debug("target window is cloaked by DWM (suspended UWP window), using SC_MINIMIZE");
            bool posted = SendSystemCommand(hwnd, Native.SC_MINIMIZE);
            Native.ShowWindowAsync(hwnd, Native.SW_SHOWMINIMIZED);
            return posted;
        }

        Native.ShowWindowAsync(hwnd, Native.SW_MINIMIZE);

        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        Log.Debug($"minimize sent: hwnd=0x{hwnd.ToInt64():X} pid={pid} "
            + $"title=\"{Native.GetWindowTitle(hwnd)}\" mode={mode}");
        return true;
    }

    /// <summary>
    /// Background recheck: a few windows (some UWP and some owner drawn ones) ignore the first
    /// minimize. When they are still not minimized after 300ms, try once more and, if that still
    /// does not work, bring the window forward and force it.
    /// </summary>
    internal static void MinimizeWithRetry(IntPtr hwnd, string mode) {
        Minimize(hwnd, mode);

        if (mode.Equals("hide", StringComparison.OrdinalIgnoreCase)) return;

        _ = Task.Run(async () => {
            await Task.Delay(350).ConfigureAwait(false);
            if (!Native.IsWindow(hwnd) || Native.IsIconic(hwnd)) return;

            Log.Debug($"first minimize had no effect, retrying: hwnd=0x{hwnd.ToInt64():X}");
            SendSystemCommand(hwnd, Native.SC_MINIMIZE);
            Native.ShowWindowAsync(hwnd, Native.SW_SHOWMINIMIZED);

            await Task.Delay(300).ConfigureAwait(false);
            if (Native.IsWindow(hwnd) && !Native.IsIconic(hwnd)) {
                Log.Debug("retry did not minimize it either, forcing the foreground window");
                Native.SetForegroundWindow(hwnd);
                await Task.Delay(80).ConfigureAwait(false);
                Native.ShowWindowAsync(hwnd, Native.SW_FORCEMINIMIZE);
            }
        });
    }

    private static bool SendSystemCommand(IntPtr hwnd, int command) {
        try {
            Native.SendMessageTimeout(
                hwnd,
                Native.WM_SYSCOMMAND,
                new IntPtr(command),
                IntPtr.Zero,
                Native.SMTO_ABORTIFHUNG,
                200,
                out _
            );
            return true;
        } catch (Exception ex) {
            Debug.WriteLine(ex);
            return false;
        }
    }
}
