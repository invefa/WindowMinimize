using System.Drawing;
using System.Windows.Forms;

namespace WindowMinimize;

/// <summary>
/// Settings dialog: change the hotkey, pick the mode, toggle auto start. The hotkey that is in
/// effect keeps working while the dialog is open.
/// </summary>
internal sealed class HotkeyDialog : Form {
    private readonly TextBox _hotkeyBox;
    private readonly RadioButton _minimizeRadio;
    private readonly RadioButton _hideRadio;
    private readonly CheckBox _autoStartBox;
    private readonly Label _hint;

    internal string HotkeyText => _hotkeyBox.Text.Trim();

    internal string Mode => _hideRadio.Checked ? "hide" : "minimize";

    internal bool AutoStart => _autoStartBox.Checked;

    internal HotkeyDialog(AppConfig config, bool autoStart, bool autoStartLocked) {
        Text = Strings.DialogTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(460, 0);
        Font = new Font("Segoe UI", 9f);
        AutoScaleMode = AutoScaleMode.Font;

        var layout = new TableLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));

        var hotkeyLabel = new Label {
            Text = Strings.DialogHotkey,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 6, 12, 3),
        };

        _hotkeyBox = new TextBox {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 3, 0, 3),
            Text = config.Hotkey,
        };

        // AutoSize with a maximum width wraps the hint and grows the row to the needed height, so
        // a longer translation is never clipped.
        var syntaxHint = new Label {
            Text = Strings.DialogSyntaxHint,
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            ForeColor = Color.FromArgb(96, 96, 96),
            Margin = new Padding(0, 0, 0, 10),
        };

        var modeLabel = new Label {
            Text = Strings.DialogMode,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 6, 12, 3),
        };

        var modes = new FlowLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = new Padding(0, 3, 0, 3),
        };

        _minimizeRadio = new RadioButton {
            Text = Strings.DialogMinimize,
            AutoSize = true,
            Checked = !config.Mode.Equals("hide", StringComparison.OrdinalIgnoreCase),
            Margin = new Padding(0, 3, 16, 3),
        };

        _hideRadio = new RadioButton {
            Text = Strings.DialogHide,
            AutoSize = true,
            Checked = config.Mode.Equals("hide", StringComparison.OrdinalIgnoreCase),
            Margin = new Padding(0, 3, 0, 3),
        };

        modes.Controls.Add(_minimizeRadio);
        modes.Controls.Add(_hideRadio);

        _autoStartBox = new CheckBox {
            Text = autoStartLocked ? Strings.MenuAutoStartAdmin : Strings.MenuAutoStart,
            AutoSize = true,
            Checked = autoStart,
            Enabled = !autoStartLocked,
            Margin = new Padding(0, 6, 0, 6),
        };

        _hint = new Label {
            Text = string.Empty,
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            ForeColor = Color.FromArgb(176, 0, 32),
            Visible = false,
            Margin = new Padding(0, 6, 0, 0),
        };

        var buttons = new FlowLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 14, 0, 0),
        };

        var save = new Button {
            Text = Strings.DialogSave,
            AutoSize = true,
            MinimumSize = new Size(80, 28),
            Margin = new Padding(6, 0, 0, 0),
        };

        var cancel = new Button {
            Text = Strings.DialogCancel,
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            MinimumSize = new Size(76, 28),
            Margin = new Padding(0),
        };

        save.Click += OnSaveClicked;
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);

        layout.Controls.Add(hotkeyLabel, 0, 0);
        layout.Controls.Add(_hotkeyBox, 1, 0);
        layout.Controls.Add(syntaxHint, 1, 1);
        layout.Controls.Add(modeLabel, 0, 2);
        layout.Controls.Add(modes, 1, 2);
        layout.Controls.Add(_autoStartBox, 1, 3);
        layout.Controls.Add(_hint, 1, 4);
        layout.Controls.Add(buttons, 1, 5);

        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void OnSaveClicked(object? sender, EventArgs e) {
        if (!HotkeySpec.TryParse(_hotkeyBox.Text, out HotkeySpec? spec, out string error)) {
            _hint.Text = error;
            _hint.Visible = true;
            return;
        }

        // write the canonical text back, win+alt+m becomes Win+Alt+M
        _hotkeyBox.Text = spec!.Text;
        DialogResult = DialogResult.OK;
        Close();
    }
}
