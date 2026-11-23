using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace WindowMinimize;

/// <summary>A small circular question mark with the standard hover tooltip.</summary>
internal sealed class HelpBadge : Control {
    private bool _hover;

    internal HelpBadge() {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true
        );
        Size = new Size(18, 18);
        Cursor = Cursors.Help;
        TabStop = false;
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = "?";
        Font = new Font("Segoe UI", 10f, FontStyle.Bold);
    }

    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        var circle = new RectangleF(2f, 2f, Width - 4f, Height - 4f);
        Color color = _hover ? SystemColors.ControlText : SystemColors.ControlDark;
        using (var border = new Pen(color, 1f)) {
            e.Graphics.DrawEllipse(border, circle);
        }

        using var format = new StringFormat {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoClip,
        };
        using var brush = new SolidBrush(color);
        e.Graphics.DrawString(
            "?", Font, brush, new RectangleF(0f, 0f, Width, Height), format
        );
    }

    protected override void OnMouseEnter(EventArgs e) {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e) {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }
}
