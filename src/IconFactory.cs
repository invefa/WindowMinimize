using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WindowMinimize;

/// <summary>
/// 托盘图标。按系统小图标尺寸直接画（从 exe 里的 32×32 缩到 16 会发虚），画不出来时退回 exe 内嵌图标。
/// </summary>
internal static class IconFactory
{
    private static Icon? _cached;

    internal static Icon Create()
    {
        if (_cached is not null)
        {
            return (Icon)_cached.Clone();
        }

        try
        {
            _cached = Draw(SystemInformation.SmallIconSize.Width);
            return (Icon)_cached.Clone();
        }
        catch
        {
            // 现画失败就退回 exe 内嵌图标
        }

        Icon? fromExe = LoadFromExecutable();
        if (fromExe is not null)
        {
            _cached = fromExe;
            return (Icon)fromExe.Clone();
        }

        _cached = Draw(32);
        return (Icon)_cached.Clone();
    }

    private static Icon? LoadFromExecutable()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (exe is null || !File.Exists(exe))
            {
                return null;
            }

            Icon? icon = Icon.ExtractAssociatedIcon(exe);
            if (icon is null)
            {
                return null;
            }

            // 退回路径：拿到的内嵌图标是 32×32，再由系统缩到托盘尺寸
            using var bmp = icon.ToBitmap();
            using var scaled = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(bmp, 0, 0, 32, 32);
            }

            IntPtr handle = scaled.GetHicon();
            try
            {
                using Icon temp = Icon.FromHandle(handle);
                return (Icon)temp.Clone();
            }
            finally
            {
                DestroyIcon(handle);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 画一个“最小化”图标：两条向下的折线箭头收到底部一条横线上，示意把窗口收起到任务栏。
    /// 几何与 build\make-icon.ps1 保持一致（都是 32 单位坐标系），改一边要同步改另一边。
    /// </summary>
    private static Icon Draw(int size)
    {
        using var bmp = new Bitmap(size, size);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            float scale = size / 32f;
            PointF P(float x, float y) => new(x * scale, y * scale);

            using var pen = new Pen(Color.FromArgb(46, 124, 246), 3f * scale)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };

            g.DrawLines(pen, new[] { P(10.5f, 6.6f), P(16f, 12f), P(21.5f, 6.6f) });
            g.DrawLines(pen, new[] { P(10.5f, 13.4f), P(16f, 18.8f), P(21.5f, 13.4f) });
            g.DrawLine(pen, P(7.5f, 24.6f), P(24.5f, 24.6f));
        }

        IntPtr handle = bmp.GetHicon();
        try
        {
            using Icon temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
