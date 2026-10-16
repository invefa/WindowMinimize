using System.Drawing;
using System.Windows.Forms;

namespace WindowMinimize;

/// <summary>
/// 配置对话框：改热键、选方式、开机自启。对话框打开期间当前热键照常生效。
/// </summary>
internal sealed class HotkeyDialog : Form
{
    private readonly TextBox _hotkeyBox;
    private readonly RadioButton _minimizeRadio;
    private readonly RadioButton _hideRadio;
    private readonly CheckBox _autoStartBox;
    private readonly Label _hint;

    internal string HotkeyText => _hotkeyBox.Text.Trim();

    internal string Mode => _hideRadio.Checked ? "hide" : "minimize";

    internal bool AutoStart => _autoStartBox.Checked;

    internal HotkeyDialog(AppConfig config)
    {
        Text = "WindowMinimize 设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(392, 236);
        Font = new Font("Microsoft YaHei UI", 9f);
        AutoScaleMode = AutoScaleMode.Font;

        var hotkeyLabel = new Label
        {
            Text = "热键",
            Location = new Point(16, 20),
            AutoSize = true,
        };

        _hotkeyBox = new TextBox
        {
            Location = new Point(84, 17),
            Width = 288,
            Text = config.Hotkey,
        };

        var syntaxHint = new Label
        {
            Text = "修饰键 Ctrl、Alt、Shift、Win 加主键，用 + 连接，例如 Win+Alt+M、Ctrl+Shift+Q、F11",
            Location = new Point(84, 45),
            AutoSize = false,
            Size = new Size(288, 34),
            ForeColor = Color.FromArgb(96, 96, 96),
        };

        var modeLabel = new Label
        {
            Text = "方式",
            Location = new Point(16, 92),
            AutoSize = true,
        };

        _minimizeRadio = new RadioButton
        {
            Text = "最小化到任务栏",
            Location = new Point(84, 89),
            AutoSize = true,
            Checked = !config.Mode.Equals("hide", StringComparison.OrdinalIgnoreCase),
        };

        _hideRadio = new RadioButton
        {
            Text = "隐藏窗口",
            Location = new Point(232, 89),
            AutoSize = true,
            Checked = config.Mode.Equals("hide", StringComparison.OrdinalIgnoreCase),
        };

        _autoStartBox = new CheckBox
        {
            Text = "开机自动启动",
            Location = new Point(84, 122),
            AutoSize = true,
            Checked = ConfigStore.IsAutoStartEnabled(),
        };

        _hint = new Label
        {
            Text = string.Empty,
            Location = new Point(16, 156),
            AutoSize = false,
            Size = new Size(356, 34),
            ForeColor = Color.FromArgb(176, 0, 32),
        };

        var save = new Button
        {
            Text = "保存",
            Location = new Point(208, 194),
            Size = new Size(80, 28),
        };

        var cancel = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Location = new Point(296, 194),
            Size = new Size(76, 28),
        };

        save.Click += OnSaveClicked;

        Controls.AddRange([hotkeyLabel, _hotkeyBox, syntaxHint, modeLabel, _minimizeRadio, _hideRadio, _autoStartBox, _hint, save, cancel]);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        if (!HotkeySpec.TryParse(_hotkeyBox.Text, out HotkeySpec? spec, out string error))
        {
            _hint.Text = error;
            return;
        }

        // 回填规范化写法：win+alt+m 会变成 Win+Alt+M
        _hotkeyBox.Text = spec!.Text;
        DialogResult = DialogResult.OK;
        Close();
    }
}
