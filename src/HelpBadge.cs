using System.Drawing;
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
        Size = new Size(20, 20);
        Cursor = Cursors.Help;
        TabStop = false;
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = "?";
        Font = new Font("Segoe UI", 11f, FontStyle.Regular);
    }

    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var circle = new Rectangle(0, 0, Width - 1, Height - 1);

        if (_hover) {
            using var fill = new SolidBrush(SystemColors.ControlLight);
            e.Graphics.FillEllipse(fill, circle);
        }

        using (var border = new Pen(SystemColors.ControlDark)) {
            e.Graphics.DrawEllipse(border, circle);
        }

        TextRenderer.DrawText(
            e.Graphics, "?", Font, circle, SystemColors.ControlText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPadding
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
