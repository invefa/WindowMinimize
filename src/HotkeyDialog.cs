using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace WindowMinimize;

/// <summary>
/// Settings dialog: hotkey, mode, auto start and the debug log. The hotkey that is in effect keeps
/// working while the dialog is open.
/// </summary>
internal sealed class HotkeyDialog : Form {
    private readonly HotkeyCaptureBox _hotkeyBox;
    private readonly HotkeyCaptureBox _moveHotkeyBox;
    private readonly HotkeyCaptureBox _restoreHotkeyBox;
    private readonly ComboBox _monitorABox;
    private readonly ComboBox _monitorBBox;
    private readonly ComboBox _restoreAnimationBox;
    private readonly CheckBox _trackAllMinimizedBox;
    private readonly CheckBox _checkUpdatesBox;
    private readonly CheckBox _autoUpdateBox;
    private readonly RadioButton _minimizeRadio;
    private readonly RadioButton _hideRadio;
    private readonly CheckBox _autoStartBox;
    private readonly CheckBox _autoStartAdminBox;
    private readonly CheckBox _debugBox;
    private readonly CheckBox _notifyStartupBox;
    private readonly CheckBox _notifyErrorsBox;
    private readonly CheckBox _notifyPermissionsBox;
    private readonly ComboBox _languageBox;
    private readonly Label _hint;
    private readonly Button? _restartButton = null;

    internal string HotkeyText => _hotkeyBox.Text.Trim();

    internal string MoveHotkeyText => _moveHotkeyBox.Text.Trim();

    internal string RestoreHotkeyText => _restoreHotkeyBox.Text.Trim();

    internal string MonitorA => (_monitorABox.SelectedItem as MonitorChoice)?.DeviceName ?? "";

    internal string MonitorB => (_monitorBBox.SelectedItem as MonitorChoice)?.DeviceName ?? "";

    internal string RestoreAnimation => _restoreAnimationBox.SelectedIndex switch {
        1 => "fade",
        2 => "disable",
        3 => "default",
        _ => "native",
    };

    internal bool TrackAllMinimized => _trackAllMinimizedBox.Checked;

    internal bool CheckUpdates => _checkUpdatesBox.Checked;

    internal bool AutoUpdate => _autoUpdateBox.Checked;

    internal string Mode => _hideRadio.Checked ? "hide" : "minimize";

    internal string Language => _languageBox.SelectedIndex switch {
        1 => "en",
        2 => "zh",
        _ => "auto",
    };

    internal bool AutoStart => _autoStartBox.Checked;

    internal bool AutoStartAdmin => _autoStartBox.Checked && _autoStartAdminBox.Checked;

    internal bool Debug => _debugBox.Checked;

    internal bool NotifyStartup => _notifyStartupBox.Checked;

    internal bool NotifyErrors => _notifyErrorsBox.Checked;

    internal bool NotifyPermissions => _notifyPermissionsBox.Checked;

    internal bool RestartRequested { get; private set; }

    internal HotkeyDialog(
        AppConfig config, bool autoStart, bool autoStartAdmin, bool debug, bool alreadyElevated
    ) {
        Text = Strings.DialogTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        HelpButton = true;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        MinimumSize = new Size(460, 0);
        Font = new Font("Segoe UI", 9f);
        AutoScaleMode = AutoScaleMode.Font;
        HelpButtonClicked += (_, e) => {
            e.Cancel = true;
            OpenHelpDocument();
        };

        var layout = new TableLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));

        var hotkeyLabel = RowLabel(Strings.DialogHotkey);

        _hotkeyBox = new HotkeyCaptureBox(config.Hotkey, Strings.HotkeyCapturePrompt) {
            Dock = DockStyle.Fill,
        };

        var syntaxHint = new Label {
            Text = Strings.DialogSyntaxHint,
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            ForeColor = Color.FromArgb(96, 96, 96),
            Margin = new Padding(0, 0, 0, 10),
        };

        var moveHotkeyLabel = RowLabel(Strings.DialogMoveHotkey);

        _moveHotkeyBox = new HotkeyCaptureBox(
            config.MoveHotkey, Strings.HotkeyCapturePrompt
        ) {
            Dock = DockStyle.Fill,
        };

        var restoreHotkeyLabel = RowLabel(Strings.DialogRestoreHotkey);

        _restoreHotkeyBox = new HotkeyCaptureBox(
            config.RestoreHotkey, Strings.HotkeyCapturePrompt
        ) {
            Dock = DockStyle.Fill,
        };

        var hotkeyRow = HotkeyRow(_hotkeyBox);
        var moveHotkeyRow = HotkeyRow(_moveHotkeyBox);
        var restoreHotkeyRow = HotkeyRow(_restoreHotkeyBox);

        var modeLabel = RowLabel(Strings.DialogMode);

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

        var languageLabel = RowLabel(Strings.DialogLanguage);

        _languageBox = new ComboBox {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 180,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 3, 0, 3),
        };
        _languageBox.Items.AddRange([
            Strings.LanguageAuto, Strings.LanguageEnglish, Strings.LanguageChinese,
        ]);
        _languageBox.SelectedIndex = config.Language.ToLowerInvariant() switch {
            "en" => 1,
            "zh" => 2,
            _ => 0,
        };

        var monitors = BuildMonitorChoices();
        var monitorALabel = RowLabel(Strings.DialogMonitorA);
        var monitorADefault = monitors.FirstOrDefault(item => item.Primary) ?? monitors[0];
        _monitorABox = MonitorComboBox(monitors, config.MonitorA, monitorADefault);

        var monitorBLabel = RowLabel(Strings.DialogMonitorB);
        var monitorBDefault = monitors.FirstOrDefault(item => !item.Primary) ?? monitors[0];
        _monitorBBox = MonitorComboBox(monitors, config.MonitorB, monitorBDefault);

        var restoreAnimationLabel = RowLabel(Strings.DialogRestoreAnimation);
        _restoreAnimationBox = new ComboBox {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 300,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 3, 0, 3),
        };
        _restoreAnimationBox.Items.AddRange([
            Strings.RestoreAnimationNative,
            Strings.RestoreAnimationFade,
            Strings.RestoreAnimationDisable,
            Strings.RestoreAnimationDefault,
        ]);
        _restoreAnimationBox.SelectedIndex = config.RestoreAnimation.ToLowerInvariant() switch {
            "fade" => 1,
            "disable" => 2,
            "default" => 3,
            _ => 0,
        };

        _trackAllMinimizedBox = new CheckBox {
            Text = Strings.DialogTrackAllMinimized,
            AutoSize = true,
            Checked = config.TrackAllMinimized,
            Margin = new Padding(0, 3, 0, 3),
        };

        var updateBoxes = new FlowLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 3, 0, 3),
        };
        _checkUpdatesBox = new CheckBox {
            Text = Strings.DialogCheckUpdates,
            AutoSize = true,
            Checked = config.CheckUpdates,
            Margin = new Padding(0, 3, 0, 3),
        };
        _autoUpdateBox = new CheckBox {
            Text = Strings.DialogAutoUpdate,
            AutoSize = true,
            Checked = config.AutoUpdate,
            Margin = new Padding(0, 3, 0, 3),
        };
        updateBoxes.Controls.Add(_checkUpdatesBox);
        updateBoxes.Controls.Add(_autoUpdateBox);

        var startupLabel = RowLabel(Strings.DialogStartup);

        var trackingLabel = RowLabel(Strings.DialogTracking);
        var updatesLabel = RowLabel(Strings.DialogUpdates);

        var startupBoxes = new FlowLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 3, 0, 3),
        };

        // the elevated start is a sub option: it only means something when auto start is on
        _autoStartBox = new CheckBox {
            Text = Strings.MenuAutoStart,
            AutoSize = true,
            Checked = autoStart,
            Margin = new Padding(0, 3, 0, 3),
        };

        _autoStartAdminBox = new CheckBox {
            Text = Strings.MenuAutoStartAdmin,
            AutoSize = true,
            Checked = autoStart && autoStartAdmin,
            Enabled = autoStart,
            Margin = new Padding(0, 3, 0, 3),
        };

        _autoStartBox.CheckedChanged += (_, _) => {
            _autoStartAdminBox.Enabled = _autoStartBox.Checked;
            if (!_autoStartBox.Checked) _autoStartAdminBox.Checked = false;
        };
        _autoStartAdminBox.CheckedChanged += (_, _) => {
            if (_autoStartAdminBox.Checked) _autoStartBox.Checked = true;
        };

        startupBoxes.Controls.Add(_autoStartBox);
        startupBoxes.Controls.Add(_autoStartAdminBox);

        var debugLabel = RowLabel(Strings.DialogLog);

        _debugBox = new CheckBox {
            Text = Strings.DialogDebug,
            AutoSize = true,
            Checked = debug,
            Margin = new Padding(0, 6, 0, 6),
        };

        var notificationsLabel = RowLabel(Strings.DialogNotifications);

        var notificationBoxes = new TableLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 3, 0, 3),
        };
        notificationBoxes.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        notificationBoxes.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        notificationBoxes.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _notifyStartupBox = new CheckBox {
            Text = Strings.DialogNotifyStartup,
            AutoSize = true,
            Checked = config.NotifyStartup,
            Margin = new Padding(0, 3, 8, 3),
        };

        _notifyErrorsBox = new CheckBox {
            Text = Strings.DialogNotifyErrors,
            AutoSize = true,
            Checked = config.NotifyErrors,
            Margin = new Padding(0, 3, 8, 3),
        };

        _notifyPermissionsBox = new CheckBox {
            Text = Strings.DialogNotifyPermissions,
            AutoSize = true,
            Checked = config.NotifyPermissions,
            Margin = new Padding(0, 3, 0, 3),
        };

        notificationBoxes.Controls.Add(_notifyStartupBox, 0, 0);
        notificationBoxes.Controls.Add(_notifyErrorsBox, 1, 0);
        notificationBoxes.Controls.Add(_notifyPermissionsBox, 2, 0);

        _hint = new Label {
            Text = string.Empty,
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            ForeColor = Color.FromArgb(176, 0, 32),
            Visible = false,
            Margin = new Padding(0, 6, 0, 0),
        };

        foreach (HotkeyCaptureBox box in new[] {
            _hotkeyBox, _moveHotkeyBox, _restoreHotkeyBox,
        }) {
            box.CaptureRejected += error => {
                _hint.Text = error;
                _hint.Visible = true;
            };
            box.CaptureAccepted += () => _hint.Visible = false;
        }

        var buttonRow = new TableLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 14, 0, 0),
        };
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        if (alreadyElevated) {
            var adminLabel = new Label {
                Text = Strings.DialogAlreadyElevated,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = Color.FromArgb(96, 96, 96),
                Margin = new Padding(0, 6, 12, 0),
            };
            buttonRow.Controls.Add(adminLabel, 0, 0);
        } else {
            _restartButton = new Button {
                Text = Strings.MenuRestartAdmin,
                AutoSize = true,
                MinimumSize = new Size(0, 28),
                Margin = new Padding(0, 0, 12, 0),
            };
            _restartButton.Click += (_, _) => {
                RestartRequested = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            buttonRow.Controls.Add(_restartButton, 0, 0);
        }

        var rightButtons = new FlowLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
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
        rightButtons.Controls.Add(cancel);
        rightButtons.Controls.Add(save);
        buttonRow.Controls.Add(rightButtons, 1, 0);

        layout.Controls.Add(hotkeyLabel, 0, 0);
        layout.Controls.Add(hotkeyRow, 1, 0);
        layout.Controls.Add(syntaxHint, 1, 1);
        layout.Controls.Add(moveHotkeyLabel, 0, 2);
        layout.Controls.Add(moveHotkeyRow, 1, 2);
        layout.Controls.Add(restoreHotkeyLabel, 0, 3);
        layout.Controls.Add(restoreHotkeyRow, 1, 3);
        layout.Controls.Add(modeLabel, 0, 4);
        layout.Controls.Add(modes, 1, 4);
        layout.Controls.Add(languageLabel, 0, 5);
        layout.Controls.Add(_languageBox, 1, 5);
        layout.Controls.Add(monitorALabel, 0, 6);
        layout.Controls.Add(_monitorABox, 1, 6);
        layout.Controls.Add(monitorBLabel, 0, 7);
        layout.Controls.Add(_monitorBBox, 1, 7);
        layout.Controls.Add(restoreAnimationLabel, 0, 8);
        layout.Controls.Add(_restoreAnimationBox, 1, 8);
        layout.Controls.Add(trackingLabel, 0, 9);
        layout.Controls.Add(_trackAllMinimizedBox, 1, 9);
        layout.Controls.Add(updatesLabel, 0, 10);
        layout.Controls.Add(updateBoxes, 1, 10);
        layout.Controls.Add(startupLabel, 0, 11);
        layout.Controls.Add(startupBoxes, 1, 11);
        layout.Controls.Add(debugLabel, 0, 12);
        layout.Controls.Add(_debugBox, 1, 12);
        layout.Controls.Add(notificationsLabel, 0, 13);
        layout.Controls.Add(notificationBoxes, 1, 13);
        layout.Controls.Add(_hint, 1, 14);
        layout.Controls.Add(buttonRow, 0, 15);
        layout.SetColumnSpan(buttonRow, 2);

        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private static Label RowLabel(string text) => new() {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Top | AnchorStyles.Left,
        Margin = new Padding(3, 3, 12, 3),
    };

    private static TableLayoutPanel HotkeyRow(HotkeyCaptureBox box) {
        var row = new TableLayoutPanel {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 24));

        box.Dock = DockStyle.Fill;
        var clear = new ClearIconButton {
            Anchor = AnchorStyles.None,
            Margin = new Padding(0),
        };
        clear.Click += (_, _) => box.ClearValue();

        row.Controls.Add(box, 0, 0);
        row.Controls.Add(clear, 1, 0);
        return row;
    }

    /// <summary>Open the standalone help document in the default browser.</summary>
    private static void OpenHelpDocument() {
        string file = Strings.Culture.TwoLetterISOLanguageName.Equals(
            "zh", StringComparison.OrdinalIgnoreCase
        ) ? "HELP.zh-Hans.html" : "HELP.html";

        try {
            string path = Path.Combine(AppContext.BaseDirectory, file);
            if (!File.Exists(path)) {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WindowMinimize", "help"
                );
                Directory.CreateDirectory(directory);
                path = Path.Combine(directory, file);

                if (!File.Exists(path)) {
                    using Stream? resource = Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream($"WindowMinimize.{file}");
                    if (resource is null) {
                        throw new FileNotFoundException("help resource is missing", file);
                    }

                    using FileStream output = File.Create(path);
                    resource.CopyTo(output);
                }
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        } catch (Exception ex) {
            MessageBox.Show(
                Strings.HelpOpenFailed(ex.Message), Strings.AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Warning
            );
        }
    }

    private static List<MonitorChoice> BuildMonitorChoices() {
        var choices = new List<MonitorChoice>();
        Screen[] screens = Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++) {
            Screen screen = screens[i];
            string label = Strings.MonitorDisplay(
                i + 1, screen.Bounds.Width, screen.Bounds.Height
            );
            if (screen.Primary) label += "  " + Strings.MonitorPrimary;
            choices.Add(new MonitorChoice(screen.DeviceName, label, screen.Primary));
        }

        if (choices.Count == 0) {
            choices.Add(new MonitorChoice(string.Empty, Strings.MonitorDisplay(1, 0, 0), true));
        }

        return choices;
    }

    private static ComboBox MonitorComboBox(
        List<MonitorChoice> choices, string selected, MonitorChoice fallback
    ) {
        var box = new ComboBox {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 300,
            Margin = new Padding(0, 3, 0, 3),
        };
        foreach (MonitorChoice choice in choices) box.Items.Add(choice);

        box.SelectedItem = choices.FirstOrDefault(
            choice => choice.DeviceName.Equals(selected, StringComparison.OrdinalIgnoreCase)
        ) ?? fallback;
        return box;
    }

    private sealed record MonitorChoice(string DeviceName, string Label, bool Primary) {
        public override string ToString() => Label;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
        if (keyData == (Keys.Alt | Keys.K)) {
            _hotkeyBox.Focus();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.V)) {
            _moveHotkeyBox.Focus();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.B)) {
            _restoreHotkeyBox.Focus();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.M)) {
            _minimizeRadio.Checked = true;
            _minimizeRadio.Focus();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.H)) {
            _hideRadio.Checked = true;
            _hideRadio.Focus();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.A)) {
            _autoStartBox.Checked = !_autoStartBox.Checked;
            _autoStartBox.Focus();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.E) && _autoStartAdminBox.Enabled) {
            _autoStartAdminBox.Checked = !_autoStartAdminBox.Checked;
            _autoStartAdminBox.Focus();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.D)) {
            _debugBox.Checked = !_debugBox.Checked;
            _debugBox.Focus();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.N)) {
            _notifyStartupBox.Focus();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.L)) {
            _languageBox.Focus();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.S)) {
            OnSaveClicked(this, EventArgs.Empty);
            return true;
        }

        if (keyData == (Keys.Alt | Keys.C)) {
            DialogResult = DialogResult.Cancel;
            Close();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.R) && _restartButton is not null) {
            _restartButton.PerformClick();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnSaveClicked(object? sender, EventArgs e) {
        _hotkeyBox.CancelCapture();
        _moveHotkeyBox.CancelCapture();
        _restoreHotkeyBox.CancelCapture();

        if (!HotkeySpec.TryParse(_hotkeyBox.Text, out HotkeySpec? spec, out string error)) {
            ShowHotkeyError(_hotkeyBox, error);
            return;
        }

        if (!HotkeySpec.TryParse(
                _moveHotkeyBox.Text, out HotkeySpec? moveSpec, out string moveError
            )) {
            ShowHotkeyError(_moveHotkeyBox, moveError);
            return;
        }

        if (!HotkeySpec.TryParse(
                _restoreHotkeyBox.Text, out HotkeySpec? restoreSpec, out string restoreError
            )) {
            ShowHotkeyError(_restoreHotkeyBox, restoreError);
            return;
        }

        if (spec!.Equals(moveSpec)) {
            ShowHotkeyError(
                _moveHotkeyBox, Strings.ErrorHotkeyConflict(spec.Text)
            );
            return;
        }

        if (spec.Equals(restoreSpec)) {
            ShowHotkeyError(
                _restoreHotkeyBox, Strings.ErrorHotkeyConflict(spec.Text)
            );
            return;
        }

        if (moveSpec!.Equals(restoreSpec)) {
            ShowHotkeyError(
                _restoreHotkeyBox, Strings.ErrorHotkeyConflict(moveSpec.Text)
            );
            return;
        }

        // write the canonical text back, win+alt+m becomes Win+Alt+M
        _hotkeyBox.Text = spec.Text;
        _moveHotkeyBox.Text = moveSpec.Text;
        _restoreHotkeyBox.Text = restoreSpec!.Text;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ShowHotkeyError(TextBox box, string error) {
        _hint.Text = error;
        _hint.Visible = true;
        box.Focus();
        box.SelectAll();
    }
}
