using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WindowMinimize;

/// <summary>A borderless, transparent X icon button.</summary>
internal sealed class ClearIconButton : Control {
    private bool _hover;
    private bool _pressed;

    internal ClearIconButton() {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true
        );
        Size = new Size(26, 23);
        TabStop = false;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = "Clear shortcut";
    }

    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (_hover) {
            using var path = RoundedRectangle(1, 1, Width - 3, Height - 3, 4);
            using var fill = new SolidBrush(SystemColors.ControlLight);
            e.Graphics.FillPath(fill, path);
        }

        int centerX = Width / 2;
        int centerY = Height / 2;
        const int arm = 5;
        using var pen = new Pen(SystemColors.GrayText, 1.5f);
        e.Graphics.DrawLine(
            pen, centerX - arm, centerY - arm, centerX + arm, centerY + arm
        );
        e.Graphics.DrawLine(
            pen, centerX + arm, centerY - arm, centerX - arm, centerY + arm
        );
    }

    private static GraphicsPath RoundedRectangle(
        int x, int y, int width, int height, int radius
    ) {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(x, y, diameter, diameter, 180, 90);
        path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
        path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e) {
        bool clicked = _pressed && ClientRectangle.Contains(e.Location);
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
        if (clicked) OnClick(EventArgs.Empty);
    }

    protected override void OnMouseEnter(EventArgs e) {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e) {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }
}
