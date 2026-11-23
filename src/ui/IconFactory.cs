using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WindowMinimize;

/// <summary>
/// Tray icon. It is drawn right at the small icon size of the system, because scaling the 32x32
/// icon from the exe down to 16 comes out blurry, and the icon embedded in the exe is used when
/// drawing fails.
/// </summary>
internal static class IconFactory {
    private static Icon? _cached;

    internal static Icon Create() {
        if (_cached is not null) return (Icon)_cached.Clone();

        try {
            _cached = Draw(SystemInformation.SmallIconSize.Width);
            return (Icon)_cached.Clone();
        } catch {
            // drawing failed, fall back to the icon embedded in the exe
        }

        Icon? fromExe = LoadFromExecutable();
        if (fromExe is not null) {
            _cached = fromExe;
            return (Icon)fromExe.Clone();
        }

        _cached = Draw(32);
        return (Icon)_cached.Clone();
    }

    private static Icon? LoadFromExecutable() {
        try {
            string? exe = Environment.ProcessPath;
            if (exe is null || !File.Exists(exe)) return null;

            Icon? icon = Icon.ExtractAssociatedIcon(exe);
            if (icon is null) return null;

            // fallback path: the embedded icon comes out 32x32 and the system scales it to the tray
            using var bmp    = icon.ToBitmap();
            using var scaled = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(scaled)) {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(bmp, 0, 0, 32, 32);
            }

            IntPtr handle = scaled.GetHicon();
            try {
                using Icon temp = Icon.FromHandle(handle);
                return (Icon)temp.Clone();
            } finally {
                DestroyIcon(handle);
            }
        } catch {
            return null;
        }
    }

    /// <summary>
    /// Draw a minimize glyph: two chevrons pointing down onto a bar at the bottom, the window
    /// collapsing into the taskbar. The geometry matches build\make-icon.ps1, both use a 32 unit
    /// grid, so a change on one side has to be made on the other side too.
    /// </summary>
    private static Icon Draw(int size) {
        using var bmp = new Bitmap(size, size);
        using (Graphics g = Graphics.FromImage(bmp)) {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            float scale = size / 32f;
            PointF P(float x, float y) => new(x * scale, y * scale);

            using var pen = new Pen(Color.FromArgb(46, 124, 246), 3f * scale) {
                StartCap = LineCap.Round,
                EndCap   = LineCap.Round,
                LineJoin = LineJoin.Round,
            };

            g.DrawLines(pen, new[] { P(10.5f, 6.6f), P(16f, 12f), P(21.5f, 6.6f) });
            g.DrawLines(pen, new[] { P(10.5f, 13.4f), P(16f, 18.8f), P(21.5f, 13.4f) });
            g.DrawLine(pen, P(7.5f, 24.6f), P(24.5f, 24.6f));
        }

        IntPtr handle = bmp.GetHicon();
        try {
            using Icon temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        } finally {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
