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
        Size = new Size(20, 20);
        TabStop = false;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = "Clear shortcut";
    }

    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (_hover) {
            var circle = new Rectangle(1, 1, Width - 3, Height - 3);
            using var fill = new SolidBrush(SystemColors.ControlLight);
            e.Graphics.FillEllipse(fill, circle);
        }

        using var pen = new Pen(SystemColors.GrayText, 1.5f);
        int left = 6;
        int top = 6;
        int right = Width - 7;
        int bottom = Height - 7;
        e.Graphics.DrawLine(pen, left, top, right, bottom);
        e.Graphics.DrawLine(pen, right, top, left, bottom);
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
