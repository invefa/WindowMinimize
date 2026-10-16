using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WindowMinimize;

/// <summary>
/// 托盘图标。优先用 exe 里嵌的图标，取不到时按同一构图现画一个。
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

        Icon? fromExe = LoadFromExecutable();
        if (fromExe is not null)
        {
            _cached = fromExe;
            return (Icon)fromExe.Clone();
        }

        _cached = Draw();
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

            // 托盘图标要小尺寸，转成 32x32 再取最佳匹配
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
    /// 画一个“最小化”图标：居中的短横线，两侧各一条更低更短的线，示意收起到任务栏。
    /// </summary>
    private static Icon Draw()
    {
        using var bmp = new Bitmap(32, 32);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var pen = new Pen(Color.FromArgb(28, 28, 28), 2.4f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };

            g.DrawLine(pen, 6f, 23f, 26f, 23f);
            g.DrawLine(pen, 11f, 17f, 21f, 17f);
            g.DrawLine(pen, 15f, 11f, 17f, 11f);
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
