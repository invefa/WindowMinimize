using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;

namespace WindowMinimize;

/// <summary>
/// The tray application: a hidden message window receives the hotkey, the tray menu holds the
/// switches and the way into the settings.
/// </summary>
internal sealed class TrayApp : ApplicationContext {
    private readonly MessageWindow _window;
    private readonly HotkeySource _source;
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _hotkeyItem;
    private readonly ToolStripMenuItem _restoreItem;
    private readonly ToolStripMenuItem _suspendItem;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly ToolStripMenuItem _autoStartAdminItem;
    private readonly System.Windows.Forms.Timer _suspendCountdown;
    private ConfigStore.Watcher? _watcher;
    private HotkeySpec? _spec;
    private string _lastError = string.Empty;
    private IntPtr _lastHidden;
    private bool _suspended;
    private bool _elevatedNoticeShown;
    private int _suspendSecondsLeft;

    internal TrayApp(AppConfig config, bool headless) {
        Config = config;
        Headless = headless;

        _window = new MessageWindow();
        _window.MessageReceived += HandleHotkeyMessage;
        _source = new HotkeySource(_window.Handle);
        _source.Triggered += MinimizeForeground;

        var menu = new ContextMenuStrip();

        var minimizeItem = new ToolStripMenuItem(Strings.MenuMinimize);
        minimizeItem.Click += (_, _) => MinimizeForeground();

        // a window hidden with the hide mode leaves no taskbar button, this is the way back
        _restoreItem = new ToolStripMenuItem(Strings.MenuRestore) { Enabled = false };
        _restoreItem.Click += (_, _) => RestoreLastHidden();

        _hotkeyItem = new ToolStripMenuItem { Enabled = false };
        _suspendItem = new ToolStripMenuItem(Strings.MenuSuspend, null, (_, _) => {
            // holding Shift while clicking suspends for 5 minutes, it resumes by itself
            if ((Control.ModifierKeys & Keys.Shift) != 0) {
                SuspendFor(300);
                return;
            }

            SetSuspended(!_suspended);
        });

        var openItem = new ToolStripMenuItem(
            Strings.MenuOpenConfig, null, (_, _) => ConfigStore.OpenInNotepad(Config.ConfigPath)
        );
        var reloadItem = new ToolStripMenuItem(
            Strings.MenuReload, null, (_, _) => Reload(interactive: true)
        );
        var settingsItem = new ToolStripMenuItem(
            Strings.MenuSettings, null, (_, _) => ShowSettingsDialog()
        );

        var autoStartClick = new EventHandler((_, _) =>
            SetAutoStart(!ConfigStore.IsAutoStartEnabled()));
        _autoStartItem = new ToolStripMenuItem(Strings.MenuAutoStart, null, autoStartClick);
        _autoStartAdminItem = new ToolStripMenuItem(
            Strings.MenuAutoStartAdmin, null, (_, _) => ToggleElevatedAutoStart()
        );

        var restartAdminItem = new ToolStripMenuItem(
            Strings.MenuRestartAdmin, null, (_, _) => RestartAsAdministrator()
        ) { Enabled = !IsRunningAsAdministrator() };

        var exitItem = new ToolStripMenuItem(Strings.MenuExit, null, (_, _) => {
            Log.Info("exit requested");
            ExitThread();
        });

        // the version of the exe and its privilege level, as a heading above the actions
        string privilege = IsRunningAsAdministrator()
            ? Strings.PrivilegeAdminSuffix
            : Strings.PrivilegeUserSuffix;
        var versionItem = new ToolStripMenuItem(
            $"{Strings.MenuVersion(AppInfo.Version)} {privilege}"
        ) {
            Enabled = false,
        };

        menu.Items.Add(versionItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(minimizeItem);
        menu.Items.Add(_restoreItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_hotkeyItem);
        menu.Items.Add(_suspendItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(openItem);
        menu.Items.Add(reloadItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(_autoStartAdminItem);
        menu.Items.Add(restartAdminItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _icon = new NotifyIcon {
            Icon = IconFactory.Create(),
            Text = Strings.AppName,
            ContextMenuStrip = menu,
            Visible = !headless,
        };
        _icon.DoubleClick += (_, _) => ShowSettingsDialog();

        _suspendCountdown = new System.Windows.Forms.Timer { Interval = 1000 };
        _suspendCountdown.Tick += (_, _) => {
            _suspendSecondsLeft--;
            if (_suspendSecondsLeft > 0) return;

            _suspendCountdown.Stop();
            SetSuspended(false);
        };

        Apply(config, initial: true);
        SyncAutoStart();

        try {
            _watcher = new ConfigStore.Watcher(config.ConfigPath, () => Reload(interactive: false));
        } catch (Exception ex) {
            Log.Error($"cannot watch the config file: {ex.Message}");
        }
    }

    internal AppConfig Config { get; private set; }

    internal bool Headless { get; }

    internal string LastError => _lastError;

    /// <summary>Handle of the hidden message window of this program.</summary>
    internal IntPtr MessageWindowHandle => _window.Handle;

    /// <summary>The hotkey that is in effect, null when nothing could be registered.</summary>
    internal HotkeySpec? ActiveSpec => _spec;

    internal string ActiveSpecText => _spec?.Text ?? Strings.HotkeyNone;

    /// <summary>
    /// Whether the low level keyboard hook is in use because the combination is taken.
    /// </summary>
    internal bool IsUsingKeyboardHook => _source.UsingHook;

    /// <summary>
    /// Whether the hook is in use because Windows itself owns the combination (Win+S).
    /// </summary>
    internal bool IsUsingHookForWindowsCombo => _source.HookIsWindowsOwned;

    /// <summary>
    /// Handle a message forwarded by the hidden window, a hotkey hit minimizes now.
    /// </summary>
    internal void HandleHotkeyMessage(int msg, IntPtr wParam) => _source.HandleMessage(msg, wParam);

    /// <summary>
    /// Apply a config: register the hotkey and refresh the menu. On failure the hotkey that was in
    /// effect is kept and false is returned.
    /// </summary>
    internal bool Apply(AppConfig config, bool initial = false) {
        Config = config;

        if (!HotkeySpec.TryParse(config.Hotkey, out HotkeySpec? spec, out string parseError)) {
            _lastError = parseError;
            Log.Error(initial
                ? $"hotkey in the config is invalid: {parseError}"
                : $"hotkey text is invalid, keeping the current hotkey: {parseError}");
            RefreshMenu();
            return false;
        }

        if (_suspended) {
            _spec = spec;
            RefreshMenu();
            return true;
        }

        if (!_source.TryRegister(spec!, out string registerError)) {
            _lastError = registerError;
            Log.Error(registerError);

            // the very first registration falls back to the default hotkey so the tool stays usable
            if (initial && TryFallbackToDefault(spec!)) {
                RefreshMenu();
                return true;
            }

            RefreshMenu();
            return false;
        }

        _spec = spec;
        _lastError = string.Empty;
        RefreshMenu();
        return true;
    }

    private bool TryFallbackToDefault(HotkeySpec spec) {
        if (spec.Text.Equals(AppConfig.DefaultHotkey, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!HotkeySpec.TryParse(AppConfig.DefaultHotkey, out HotkeySpec? fallback, out _))
            return false;
        if (!_source.TryRegister(fallback!, out _)) return false;

        _spec = fallback;
        _lastError = Strings.ErrorFallbackUsed(_lastError, fallback!.Text);
        Log.Info($"fell back to the default hotkey {fallback.Text}");
        return true;
    }

    /// <summary>Read the config file again and apply it.</summary>
    internal void Reload(bool interactive) {
        AppConfig fresh = ConfigStore.Load(Config.ConfigPath);
        Log.Debug($"config reloaded: hotkey {fresh.Hotkey}, mode {fresh.Mode}");
        bool ok = Apply(fresh);

        if (!ok && interactive) {
            string message = Strings.BalloonHotkeyNotActive(_lastError);
            _icon.ShowBalloonTip(4000, Strings.AppName, message, ToolTipIcon.Warning);
        }
    }

    private void SetSuspended(bool suspended) {
        _suspended = suspended;

        if (suspended) {
            _source.Reset();
            Log.Info("hotkey suspended");
        } else if (_spec is not null) {
            if (!_source.TryRegister(_spec, out string error)) {
                _lastError = error;
                Log.Error(error);
            }
        }

        RefreshMenu();
    }

    /// <summary>The Suspend hotkey menu entry, with a countdown that resumes by itself.</summary>
    internal void SuspendFor(int seconds) {
        if (!_suspended) SetSuspended(true);

        _suspendSecondsLeft = seconds;
        _suspendCountdown.Stop();
        _suspendCountdown.Start();
    }

    /// <summary>
    /// Sync the auto start registry value with the config, only when it says so explicitly.
    /// </summary>
    private void SyncAutoStart() {
        try {
            if (ConfigStore.IsElevatedAutoStartInstalled()) {
                if (ConfigStore.IsAutoStartEnabled()) ConfigStore.SetAutoStart(false);
                return;
            }
        } catch (Exception ex) {
            Log.Error($"could not check the elevated autostart: {ex.Message}");
        }

        if (Config.AutoStart is not bool wanted) return;

        try {
            if (ConfigStore.IsAutoStartEnabled() != wanted) ConfigStore.SetAutoStart(wanted);
        } catch (Exception ex) {
            Log.Error($"could not sync the auto start setting: {ex.Message}");
        }
    }

    private void SetAutoStart(bool enabled) {
        // the elevated logon task and the Run entry are alternatives, never both
        if (enabled && ConfigStore.IsElevatedAutoStartInstalled()) {
            Log.Info("elevated autostart is installed, keeping the Run entry off");
            RefreshMenu();
            return;
        }

        try {
            ConfigStore.SetAutoStart(enabled);
        } catch (Exception ex) {
            string message = Strings.BalloonAutoStartFailed(ex.Message);
            Log.Error(message);
            _icon.ShowBalloonTip(4000, Strings.AppName, message, ToolTipIcon.Error);
        }

        RefreshMenu();
    }

    /// <summary>
    /// Install or remove the logon task that starts this exe with the highest run level. The task
    /// registers through a UAC prompt once, then starts elevated at every logon without one.
    /// </summary>
    private void ToggleElevatedAutoStart() {
        bool enable = !ConfigStore.IsElevatedAutoStartInstalled();
        string exe = Environment.ProcessPath ?? Application.ExecutablePath;

        bool changed = ConfigStore.TrySetElevatedAutoStart(
            enable, exe, Config.ConfigPath, out string error
        );
        if (!changed) {
            if (error.Length > 0) {
                Log.Error($"could not change the elevated autostart: {error}");
                Notify(Strings.BalloonElevatedAutoStartFailed(error), ToolTipIcon.Error);
            }

            return;
        }

        // the task replaces the normal Run entry; leaving both would start two copies at logon
        if (enable) ConfigStore.SetAutoStart(false);

        Log.Info(enable ? "elevated autostart installed" : "elevated autostart removed");
        if (enable) Notify(Strings.BalloonElevatedAutoStartInstalled);
        RefreshMenu();
    }

    private void RefreshMenu() {
        if (_suspended) {
            _hotkeyItem.Text  = Strings.HotkeyMenuItemSuspended(_spec?.Text ?? Strings.HotkeyNone);
            _suspendItem.Text = Strings.MenuResume;
        } else {
            _hotkeyItem.Text  = Strings.HotkeyMenuItem(_spec?.Text ?? Strings.HotkeyNone);
            _suspendItem.Text = Strings.MenuSuspend;
        }

        bool elevatedAutoStart = ConfigStore.IsElevatedAutoStartInstalled();
        _autoStartItem.Checked      = ConfigStore.IsAutoStartEnabled();
        _autoStartItem.Enabled      = !elevatedAutoStart;
        _autoStartAdminItem.Checked = elevatedAutoStart;
        _restoreItem.Enabled        = _lastHidden != IntPtr.Zero && Native.IsWindow(_lastHidden);

        string tip = Strings.TrayTooltip(_spec?.Text ?? Strings.HotkeyNotSet);
        _icon.Text = Truncate(tip, 62);
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    /// <summary>The action shared by the hotkey and the tray menu.</summary>
    internal void MinimizeForeground() {
        IntPtr hwnd = WindowOps.GetForegroundTarget(_window.Handle);
        if (hwnd == IntPtr.Zero) {
            Log.Debug("no foreground window to minimize, ignoring this trigger");
            return;
        }

        bool hiding = Config.Mode.Equals("hide", StringComparison.OrdinalIgnoreCase);
        if (Native.IsIconic(hwnd) && !hiding) {
            Log.Debug("foreground window is already minimized, ignoring");
            return;
        }

        WindowOps.MinimizeResult result = WindowOps.MinimizeWithRetry(hwnd, Config.Mode);

        if (result.AccessDenied) {
            Log.Info("target window refused the minimize, it probably runs as administrator");
            if (!_elevatedNoticeShown) {
                _elevatedNoticeShown = true;
                Notify(Strings.BalloonElevatedTarget, ToolTipIcon.Warning);
            }
        } else {
            _elevatedNoticeShown = false;
        }

        if (hiding && result.Minimized) {
            _lastHidden = hwnd;
            RefreshMenu();
        }
    }

    /// <summary>
    /// Whether this process already runs with administrator rights, in which case the restart
    /// entry is pointless.
    /// </summary>
    private static bool IsRunningAsAdministrator() {
        try {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        } catch {
            return false;
        }
    }

    /// <summary>
    /// Close now and let a short lived launcher start this exe again through the UAC prompt. The
    /// launcher waits for this process to exit first, so the single instance mutex is free when the
    /// elevated copy starts.
    /// </summary>
    private void RestartAsAdministrator() {
        if (IsRunningAsAdministrator()) return;

        if (MessageBox.Show(Strings.MsgRestartAdmin, Strings.AppName,
                MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) {
            return;
        }

        string exe = Environment.ProcessPath ?? Application.ExecutablePath;
        string processName = Path.GetFileNameWithoutExtension(exe).Replace("'", "''");
        string file = exe.Replace("'", "''");
        string script = $"$p=Get-Process -Name '{processName}' -ErrorAction SilentlyContinue "
            + $"| Where-Object {{ $_.Path -eq '{file}' }}; "
            + "if($p){ $p | ForEach-Object { $_.WaitForExit(8000) } }; "
            + $"Start-Process -FilePath '{file}' -Verb RunAs";

        try {
            Process.Start(new ProcessStartInfo {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            Log.Info("restart as administrator requested");
            ExitThread();
        } catch (Exception ex) {
            Log.Error($"could not restart as administrator: {ex.Message}");
            Notify(Strings.BalloonRestartFailed(ex.Message), ToolTipIcon.Error);
        }
    }

    /// <summary>Bring back the window that the last hide action put away.</summary>
    private void RestoreLastHidden() {
        if (_lastHidden != IntPtr.Zero && !WindowOps.Restore(_lastHidden)) {
            Log.Debug("the window hidden last time is gone, cannot restore it");
        }

        _lastHidden = IntPtr.Zero;
        RefreshMenu();
    }

    /// <summary>Show the settings dialog.</summary>
    internal void ShowSettingsDialog() {
        bool adminAutoStart = ConfigStore.IsElevatedAutoStartInstalled();
        bool normalAutoStart = !adminAutoStart && ConfigStore.IsAutoStartEnabled();

        using var dialog = new HotkeyDialog(
            Config, normalAutoStart, adminAutoStart, Config.Debug
        );
        if (dialog.ShowDialog() != DialogResult.OK) return;

        Config.Hotkey = dialog.HotkeyText;
        Config.Mode = dialog.Mode;
        Apply(Config);

        if (Config.Debug != dialog.Debug) {
            Config.Debug = dialog.Debug;
            Log.Init(ConfigStore.LogPath, Config.Debug);
        }

        ApplyAutoStartChoice(dialog.AutoStart, dialog.AutoStartAdmin, adminAutoStart);

        SaveToFile();
        RefreshMenu();
    }

    /// <summary>
    /// Switch between the two auto starts. The elevated one is a logon task and needs the UAC
    /// prompt, so a failure leaves the previous state and reports the reason.
    /// </summary>
    private void ApplyAutoStartChoice(bool normal, bool admin, bool adminInstalled) {
        string exe = Environment.ProcessPath ?? Application.ExecutablePath;

        if (admin) {
            if (!adminInstalled) {
                bool installed = ConfigStore.TrySetElevatedAutoStart(
                    true, exe, Config.ConfigPath, out string error
                );
                if (!installed) {
                    ReportElevatedAutoStartError(error);
                    return;
                }
            }

            Config.AutoStart = false;
            ConfigStore.SetAutoStart(false);
            return;
        }

        if (adminInstalled) {
            bool removed = ConfigStore.TrySetElevatedAutoStart(
                false, exe, Config.ConfigPath, out string error
            );
            if (!removed) {
                ReportElevatedAutoStartError(error);
                return;
            }
        }

        Config.AutoStart = normal;
        SetAutoStart(normal);
    }

    private void ReportElevatedAutoStartError(string error) {
        if (error.Length == 0) return; // the user declined the UAC prompt

        Log.Error($"could not change the elevated autostart: {error}");
        Notify(Strings.BalloonElevatedAutoStartFailed(error), ToolTipIcon.Error);
    }

    /// <summary>
    /// Write the settings back to hotkey.ini, comments kept, values replaced line by line.
    /// </summary>
    private void SaveToFile() {
        try {
            string path = Config.ConfigPath;
            string text = File.Exists(path)
                ? File.ReadAllText(path)
                : ConfigStore.DefaultIni(Config.Hotkey);

            text = SetIniValue(text, "Hotkey", Config.Hotkey);
            text = SetIniValue(text, "Mode", Config.Mode);

            if (Config.AutoStart is bool autoStart) {
                text = SetIniValue(text, "AutoStart", autoStart ? "true" : "false");
            }

            text = SetIniValue(text, "Debug", Config.Debug ? "true" : "false");

            File.WriteAllText(path, text);
            Log.Info($"settings written to {path}");
        } catch (Exception ex) {
            Log.Error($"could not write the config file: {ex.Message}");
        }
    }

    private static string SetIniValue(string text, string key, string value) {
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        bool replaced = false;

        for (int i = 0; i < lines.Length; i++) {
            string trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith(';') || trimmed.StartsWith('#')) continue;

            int split = lines[i].IndexOf('=');
            if (split <= 0) continue;

            if (lines[i][..split].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) {
                lines[i] = $"{key}={value}";
                replaced = true;
            }
        }

        if (!replaced)
            return text.TrimEnd('\n') + $"{Environment.NewLine}{key}={value}{Environment.NewLine}";

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Show one tray balloon.</summary>
    internal void Notify(string message, ToolTipIcon icon = ToolTipIcon.Info) {
        if (Headless) {
            Log.Info($"{Strings.AppName}: {message}");
            return;
        }

        _icon.ShowBalloonTip(5000, Strings.AppName, message, icon);
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            _watcher?.Dispose();
            _suspendCountdown.Dispose();
            _source.Dispose();
            _icon.Visible = false;
            _icon.Dispose();
            _window.DestroyHandle();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// A message window that only receives WM_HOTKEY, off screen and with no taskbar button.
    /// </summary>
    private sealed class MessageWindow : NativeWindow {
        internal MessageWindow() {
            // only WM_HOTKEY is received, the window sits off screen and is never shown. Do not
            // set a custom ClassName here: NativeWindow does not register a window class, so a
            // made up name would make CreateHandle fail.
            CreateHandle(new CreateParams {
                Caption = "WindowMinimize",
                X = -32000,
                Y = -32000,
                Style = 0,
            });

            Log.Debug($"message window handle = 0x{Handle.ToInt64():X}");
        }

        internal event Action<int, IntPtr>? MessageReceived;

        protected override void WndProc(ref Message m) {
            MessageReceived?.Invoke(m.Msg, m.WParam);
            base.WndProc(ref m);
        }
    }
}
