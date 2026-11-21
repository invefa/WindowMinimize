using System.Threading;

namespace WindowMinimize;

/// <summary>
/// The second copy of the program asks the first one to show its settings window. A named event
/// works across integrity levels better than posting a window message, but the message window is
/// kept as a fallback.
/// </summary>
internal static class SingleInstance {
    private const string EventName = @"Local\WindowMinimize.ShowSettings";

    internal static EventWaitHandle CreateShowSettingsEvent() =>
        new(false, EventResetMode.AutoReset, EventName);

    internal static bool RequestShowSettings() {
        for (int i = 0; i < 20; i++) {
            try {
                using EventWaitHandle? handle = EventWaitHandle.OpenExisting(EventName);
                handle.Set();
                return true;
            } catch (WaitHandleCannotBeOpenedException) {
                // the first instance is still starting up
            } catch (UnauthorizedAccessException) {
                break;
            }

            Thread.Sleep(100);
        }

        IntPtr hwnd = Native.FindWindow(null, "WindowMinimize");
        return hwnd != IntPtr.Zero
            && Native.PostMessage(hwnd, Native.WM_SHOW_SETTINGS, IntPtr.Zero, IntPtr.Zero);
    }
}
