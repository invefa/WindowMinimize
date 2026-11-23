using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WindowMinimize;

/// <summary>A small borderless clear button drawn as an X icon.</summary>
internal sealed class ClearIconButton : Button {
    private bool _hover;

    internal ClearIconButton() {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true
        );
        Text = string.Empty;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseOverBackColor = Color.Transparent;
        FlatAppearance.MouseDownBackColor = Color.Transparent;
        UseVisualStyleBackColor = false;
        Size = new Size(22, 22);
        TabStop = false;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = "Clear shortcut";
    }

    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var circle = new Rectangle(1, 1, Width - 3, Height - 3);
        if (_hover) {
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
