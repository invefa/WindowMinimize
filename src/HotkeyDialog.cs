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

    internal HotkeyDialog(AppConfig config) {
        Text = "WindowMinimize settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(392, 236);
        Font = new Font("Segoe UI", 9f);
        AutoScaleMode = AutoScaleMode.Font;

        var hotkeyLabel = new Label {
            Text = "Hotkey",
            Location = new Point(16, 20),
            AutoSize = true,
        };

        _hotkeyBox = new TextBox {
            Location = new Point(84, 17),
            Width = 288,
            Text = config.Hotkey,
        };

        var syntaxHint = new Label {
            Text = "Ctrl, Alt, Shift and Win plus one main key, joined with +, for example "
                + "Win+Alt+M",
            Location = new Point(84, 45),
            AutoSize = false,
            Size = new Size(288, 34),
            ForeColor = Color.FromArgb(96, 96, 96),
        };

        var modeLabel = new Label {
            Text = "Mode",
            Location = new Point(16, 92),
            AutoSize = true,
        };

        _minimizeRadio = new RadioButton {
            Text = "Minimize to taskbar",
            Location = new Point(84, 89),
            AutoSize = true,
            Checked = !config.Mode.Equals("hide", StringComparison.OrdinalIgnoreCase),
        };

        _hideRadio = new RadioButton {
            Text = "Hide window",
            Location = new Point(232, 89),
            AutoSize = true,
            Checked = config.Mode.Equals("hide", StringComparison.OrdinalIgnoreCase),
        };

        _autoStartBox = new CheckBox {
            Text = "Start with Windows",
            Location = new Point(84, 122),
            AutoSize = true,
            Checked = ConfigStore.IsAutoStartEnabled(),
        };

        _hint = new Label {
            Text = string.Empty,
            Location = new Point(16, 156),
            AutoSize = false,
            Size = new Size(356, 34),
            ForeColor = Color.FromArgb(176, 0, 32),
        };

        var save = new Button {
            Text = "Save",
            Location = new Point(208, 194),
            Size = new Size(80, 28),
        };

        var cancel = new Button {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(296, 194),
            Size = new Size(76, 28),
        };

        save.Click += OnSaveClicked;

        Controls.AddRange([
            hotkeyLabel, _hotkeyBox, syntaxHint, modeLabel, _minimizeRadio, _hideRadio,
            _autoStartBox, _hint, save, cancel,
        ]);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void OnSaveClicked(object? sender, EventArgs e) {
        if (!HotkeySpec.TryParse(_hotkeyBox.Text, out HotkeySpec? spec, out string error)) {
            _hint.Text = error;
            return;
        }

        // write the canonical text back, win+alt+m becomes Win+Alt+M
        _hotkeyBox.Text = spec!.Text;
        DialogResult = DialogResult.OK;
        Close();
    }
}
