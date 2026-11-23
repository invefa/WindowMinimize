using System.Drawing;
using System.Windows.Forms;

namespace WindowMinimize;

/// <summary>A small circular question mark with the standard hover tooltip.</summary>
internal sealed class HelpBadge : Control {
    // Segoe MDL2 Assets: Unknown, drawn as a question mark inside a circle
    private static readonly string Glyph = ((char)0xE9CE).ToString();
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
        Size = new Size(22, 22);
        Cursor = Cursors.Help;
        TabStop = false;
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = "?";
        Font = new Font("Segoe MDL2 Assets", 16f, FontStyle.Regular);
    }

    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        Color color = _hover ? SystemColors.ControlText : SystemColors.ControlDark;
        TextRenderer.DrawText(
            e.Graphics, Glyph, Font, ClientRectangle, color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine
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
