using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowMinimize;

/// <summary>
/// 托盘应用主体：隐藏消息窗口收热键，托盘菜单提供开关与配置入口。
/// </summary>
internal sealed class TrayApp : ApplicationContext
{
    private readonly MessageWindow _window;
    private readonly HotkeySource _source;
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _hotkeyItem;
    private readonly ToolStripMenuItem _suspendItem;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly System.Windows.Forms.Timer _suspendCountdown;
    private ConfigStore.Watcher? _watcher;
    private HotkeySpec? _spec;
    private string _lastError = string.Empty;
    private bool _suspended;
    private int _suspendSecondsLeft;

    internal TrayApp(AppConfig config, bool headless)
    {
        Config = config;
        Headless = headless;

        _window = new MessageWindow();
        _window.MessageReceived += HandleHotkeyMessage;
        _source = new HotkeySource(_window.Handle);
        _source.Triggered += MinimizeForeground;

        var menu = new ContextMenuStrip();

        var minimizeItem = new ToolStripMenuItem("最小化当前窗口");
        minimizeItem.Click += (_, _) => MinimizeForeground();

        _hotkeyItem = new ToolStripMenuItem { Enabled = false };
        _suspendItem = new ToolStripMenuItem("暂停热键", null, (_, _) =>
        {
            // 按住 Shift 点击等于暂停 5 分钟，到点自动恢复
            if ((Control.ModifierKeys & Keys.Shift) != 0)
            {
                SuspendFor(300);
                return;
            }

            SetSuspended(!_suspended);
        });

        var openItem = new ToolStripMenuItem("打开配置文件…", null, (_, _) => ConfigStore.OpenInNotepad(Config.ConfigPath));
        var reloadItem = new ToolStripMenuItem("重新载入配置", null, (_, _) => Reload(interactive: true));

        var autoStartClick = new EventHandler((_, _) => SetAutoStart(!ConfigStore.IsAutoStartEnabled()));
        _autoStartItem = new ToolStripMenuItem("开机自动启动", null, autoStartClick);

        var exitItem = new ToolStripMenuItem("退出", null, (_, _) =>
        {
            Log.Info("收到退出指令");
            ExitThread();
        });

        menu.Items.Add(minimizeItem);
        menu.Items.Add(_hotkeyItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_suspendItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(openItem);
        menu.Items.Add(reloadItem);
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _icon = new NotifyIcon
        {
            Icon = IconFactory.Create(),
            Text = "WindowMinimize",
            ContextMenuStrip = menu,
            Visible = !headless,
        };
        _icon.DoubleClick += (_, _) => MinimizeForeground();

        _suspendCountdown = new System.Windows.Forms.Timer { Interval = 1000 };
        _suspendCountdown.Tick += (_, _) =>
        {
            _suspendSecondsLeft--;
            if (_suspendSecondsLeft <= 0)
            {
                _suspendCountdown.Stop();
                SetSuspended(false);
            }
        };

        Apply(config, initial: true);
        SyncAutoStart();

        try
        {
            _watcher = new ConfigStore.Watcher(config.ConfigPath, () => Reload(interactive: false));
        }
        catch (Exception ex)
        {
            Log.Error($"无法监视配置文件变化：{ex.Message}");
        }
    }

    /// <summary>按配置里的 AutoStart 同步开机自启注册表项，只在配置显式写了且状态不一致时才写。</summary>
    private void SyncAutoStart()
    {
        if (Config.AutoStart is not bool wanted)
        {
            return;
        }

        try
        {
            if (ConfigStore.IsAutoStartEnabled() != wanted)
            {
                ConfigStore.SetAutoStart(wanted);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"同步开机自启设置失败：{ex.Message}");
        }
    }

    internal AppConfig Config { get; private set; }

    internal bool Headless { get; }

    internal string LastError => _lastError;

    /// <summary>本程序的隐藏消息窗口句柄。</summary>
    internal IntPtr MessageWindowHandle => _window.Handle;

    /// <summary>当前生效的热键；未注册成功时为 null。</summary>
    internal HotkeySpec? ActiveSpec => _spec;

    internal string ActiveSpecText => _spec?.Text ?? "无";

    /// <summary>是否因为组合被占用而用了低级键盘钩子。</summary>
    internal bool IsUsingKeyboardHook => _source.UsingHook;

    /// <summary>处理隐藏消息窗口转发来的消息，命中热键时触发最小化。</summary>
    internal void HandleHotkeyMessage(int msg, IntPtr wParam) => _source.HandleMessage(msg, wParam);

    /// <summary>应用配置：注册热键、刷新菜单。热键失败时保留原来的热键并返回 false。</summary>
    internal bool Apply(AppConfig config, bool initial = false)
    {
        Config = config;

        if (!HotkeySpec.TryParse(config.Hotkey, out HotkeySpec? spec, out string parseError))
        {
            _lastError = parseError;
            if (initial)
            {
                Log.Error($"配置里的热键无效：{parseError}");
            }
            else
            {
                Log.Error($"热键写法无效，保持原热键：{parseError}");
            }

            RefreshMenu();
            return false;
        }

        if (_suspended)
        {
            _spec = spec;
            RefreshMenu();
            return true;
        }

        if (!_source.TryRegister(spec!, out string registerError))
        {
            _lastError = registerError;
            Log.Error(registerError);
            if (initial)
            {
                // 首次注册失败就退回默认热键，保证程序可用
                if (!spec!.Text.Equals(AppConfig.DefaultHotkey, StringComparison.OrdinalIgnoreCase)
                    && HotkeySpec.TryParse(AppConfig.DefaultHotkey, out HotkeySpec? fallback, out _)
                    && _source.TryRegister(fallback!, out _))
                {
                    _spec = fallback;
                    _lastError += $"；已退回默认热键 {fallback!.Text}";
                    Log.Info($"已退回默认热键 {fallback.Text}");
                    RefreshMenu();
                    return true;
                }
            }

            RefreshMenu();
            return false;
        }

        _spec = spec;
        _lastError = string.Empty;
        RefreshMenu();
        return true;
    }

    /// <summary>重新读取配置文件并应用。</summary>
    internal void Reload(bool interactive)
    {
        AppConfig fresh = ConfigStore.Load(Config.ConfigPath);
        Log.Debug($"重新载入配置：热键 {fresh.Hotkey}，方式 {fresh.Mode}");
        bool ok = Apply(fresh);

        if (!ok && interactive)
        {
            _icon.ShowBalloonTip(4000, "WindowMinimize", $"热键未生效：{_lastError}", ToolTipIcon.Warning);
        }
    }

    private void SetSuspended(bool suspended)
    {
        _suspended = suspended;

        if (suspended)
        {
            _source.Reset();
            Log.Info("热键已暂停");
        }
        else if (_spec is not null)
        {
            if (!_source.TryRegister(_spec, out string error))
            {
                _lastError = error;
                Log.Error(error);
            }
        }

        RefreshMenu();
    }

    /// <summary>托盘菜单里的“暂停热键”带倒计时。</summary>
    internal void SuspendFor(int seconds)
    {
        if (!_suspended)
        {
            SetSuspended(true);
        }

        _suspendSecondsLeft = seconds;
        _suspendCountdown.Stop();
        _suspendCountdown.Start();
    }

    private void SetAutoStart(bool enabled)
    {
        try
        {
            ConfigStore.SetAutoStart(enabled);
        }
        catch (Exception ex)
        {
            Log.Error($"设置开机自启失败：{ex.Message}");
            _icon.ShowBalloonTip(4000, "WindowMinimize", $"设置开机自启失败：{ex.Message}", ToolTipIcon.Error);
        }

        RefreshMenu();
    }

    private void RefreshMenu()
    {
        if (_suspended)
        {
            _hotkeyItem.Text = $"热键：{_spec?.Text ?? "无"}（已暂停）";
            _suspendItem.Text = "恢复热键";
        }
        else
        {
            _hotkeyItem.Text = $"热键：{_spec?.Text ?? "无"}";
            _suspendItem.Text = "暂停热键";
        }

        _autoStartItem.Checked = ConfigStore.IsAutoStartEnabled();
        _icon.Text = Truncate($"WindowMinimize | {_spec?.Text ?? "未设置"} | 最小化当前窗口", 62);
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    /// <summary>热键与托盘菜单共用的动作。</summary>
    internal void MinimizeForeground()
    {
        IntPtr hwnd = WindowOps.GetForegroundTarget(_window.Handle);
        if (hwnd == IntPtr.Zero)
        {
            Log.Debug("没有取到可最小化的前台窗口，忽略本次触发");
            return;
        }

        if (Native.IsIconic(hwnd) && !Config.Mode.Equals("hide", StringComparison.OrdinalIgnoreCase))
        {
            Log.Debug("前台窗口已经是最小化状态，忽略");
            return;
        }

        WindowOps.MinimizeWithRetry(hwnd, Config.Mode);
    }

    /// <summary>弹出设置窗口。</summary>
    internal void ShowSettingsDialog()
    {
        using var dialog = new HotkeyDialog(Config);
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        Config.Hotkey = dialog.HotkeyText;
        Config.Mode = dialog.Mode;
        Apply(Config);

        try
        {
            ConfigStore.SetAutoStart(dialog.AutoStart);
        }
        catch (Exception ex)
        {
            Log.Error($"设置开机自启失败：{ex.Message}");
        }

        SaveToFile();
        RefreshMenu();
    }

    /// <summary>把内存里的设置写回 hotkey.ini（保留注释，逐行替换值）。</summary>
    private void SaveToFile()
    {
        try
        {
            string path = Config.ConfigPath;
            string text = File.Exists(path) ? File.ReadAllText(path) : ConfigStore.DefaultIni(Config.Hotkey);

            text = SetIniValue(text, "Hotkey", Config.Hotkey);
            text = SetIniValue(text, "Mode", Config.Mode);
            File.WriteAllText(path, text);
            Log.Info($"设置已写入 {path}");
        }
        catch (Exception ex)
        {
            Log.Error($"写入配置文件失败：{ex.Message}");
        }
    }

    private static string SetIniValue(string text, string key, string value)
    {
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        bool replaced = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith(';') || trimmed.StartsWith('#'))
            {
                continue;
            }

            int split = lines[i].IndexOf('=');
            if (split <= 0)
            {
                continue;
            }

            if (lines[i][..split].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"{key}={value}";
                replaced = true;
            }
        }

        if (!replaced)
        {
            return text.TrimEnd('\n') + $"{Environment.NewLine}{key}={value}{Environment.NewLine}";
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>显示一条托盘气泡提示。</summary>
    internal void Notify(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (Headless)
        {
            Log.Info($"{title}: {message}");
            return;
        }

        _icon.ShowBalloonTip(5000, title, message, icon);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _watcher?.Dispose();
            _suspendCountdown.Dispose();
            _source.Dispose();
            _icon.Visible = false;
            _icon.Dispose();
            _window.DestroyHandle();
        }

        base.Dispose(disposing);
    }

    /// <summary>只收 WM_HOTKEY 的消息窗口，不占任务栏、不显示。</summary>
    private sealed class MessageWindow : NativeWindow
    {
        internal MessageWindow()
        {
            // 只收 WM_HOTKEY，窗口放在屏幕外且不显示。这里不要自定义 ClassName：
            // NativeWindow 不会注册窗口类，写了自己造的新类名会导致 CreateHandle 失败。
            CreateHandle(new CreateParams
            {
                Caption = "WindowMinimize",
                X = -32000,
                Y = -32000,
                Style = 0,
            });

            Log.Debug($"消息窗口句柄 = 0x{Handle.ToInt64():X}");
        }

        internal event Action<int, IntPtr>? MessageReceived;

        protected override void WndProc(ref Message m)
        {
            MessageReceived?.Invoke(m.Msg, m.WParam);
            base.WndProc(ref m);
        }
    }
}
