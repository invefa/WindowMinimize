using System.Diagnostics;

namespace WindowMinimize;

/// <summary>
/// 目标窗口的查找与最小化。
/// </summary>
internal static class WindowOps
{
    /// <summary>取当前前台窗口的顶层窗口，跳过本程序自己的窗口。</summary>
    internal static IntPtr GetForegroundTarget(IntPtr selfHandle)
    {
        IntPtr hwnd = Native.GetAncestor(Native.GetForegroundWindow(), Native.GA_ROOT);
        if (hwnd == IntPtr.Zero)
        {
            hwnd = Native.GetForegroundWindow();
        }

        if (hwnd == IntPtr.Zero || (selfHandle != IntPtr.Zero && hwnd == selfHandle) || !Native.IsWindow(hwnd))
        {
            return IntPtr.Zero;
        }

        return hwnd;
    }

    /// <summary>
    /// 最小化一个顶层窗口。常规窗口直接用 SW_MINIMIZE；
    /// UWP 等被 DWM 遮蔽的窗口改发 WM_SYSCOMMAND/SC_MINIMIZE 并放前台兜底。
    /// </summary>
    internal static bool Minimize(IntPtr hwnd, string mode)
    {
        if (mode.Equals("hide", StringComparison.OrdinalIgnoreCase))
        {
            return Native.ShowWindowAsync(hwnd, Native.SW_HIDE);
        }

        if (Native.IsCloaked(hwnd))
        {
            Log.Debug("目标窗口被 DWM 遮蔽（UWP 挂起窗口），改用 SC_MINIMIZE");
            bool posted = SendSystemCommand(hwnd, Native.SC_MINIMIZE);
            Native.ShowWindowAsync(hwnd, Native.SW_SHOWMINIMIZED);
            return posted;
        }

        Native.ShowWindowAsync(hwnd, Native.SW_MINIMIZE);

        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        Log.Debug($"已发送最小化：hwnd=0x{hwnd.ToInt64():X} pid={pid} 标题=\"{Native.GetWindowTitle(hwnd)}\" 方式={mode}");
        return true;
    }

    /// <summary>
    /// 后台延迟复查：个别窗口（部分 UWP、部分自绘窗口）第一次最小化会被忽略，
    /// 300ms 后仍未最小化就再补一次，必要时放前台再最小化。
    /// </summary>
    internal static void MinimizeWithRetry(IntPtr hwnd, string mode)
    {
        Minimize(hwnd, mode);

        if (mode.Equals("hide", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(350).ConfigureAwait(false);
            if (!Native.IsWindow(hwnd) || Native.IsIconic(hwnd))
            {
                return;
            }

            Log.Debug($"第一次最小化未生效，重试：hwnd=0x{hwnd.ToInt64():X}");
            SendSystemCommand(hwnd, Native.SC_MINIMIZE);
            Native.ShowWindowAsync(hwnd, Native.SW_SHOWMINIMIZED);

            await Task.Delay(300).ConfigureAwait(false);
            if (Native.IsWindow(hwnd) && !Native.IsIconic(hwnd))
            {
                Log.Debug("重试仍未最小化，改为放前台后强制最小化");
                Native.SetForegroundWindow(hwnd);
                await Task.Delay(80).ConfigureAwait(false);
                Native.ShowWindowAsync(hwnd, Native.SW_FORCEMINIMIZE);
            }
        });
    }

    private static bool SendSystemCommand(IntPtr hwnd, int command)
    {
        try
        {
            Native.SendMessageTimeout(
                hwnd,
                Native.WM_SYSCOMMAND,
                new IntPtr(command),
                IntPtr.Zero,
                Native.SMTO_ABORTIFHUNG,
                200,
                out _);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return false;
        }
    }
}
