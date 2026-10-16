using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace WindowMinimize;

/// <summary>
/// 配置文件的定位、读写与热加载。
/// </summary>
internal static class ConfigStore
{
    private const string AppFolderName = "WindowMinimize";
    private const string ConfigFileName = "hotkey.ini";
    private const string LogFileName = "hotkey.log";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "WindowMinimize";

    /// <summary>配置实际所在目录（exe 同目录，不可写时用 %APPDATA%\WindowMinimize）。</summary>
    internal static string Directory { get; private set; } = AppContext.BaseDirectory;

    internal static string LogPath => Path.Combine(Directory, LogFileName);

    /// <summary>
    /// 读取配置。exe 同目录没有 hotkey.ini 时，优先在那里写一份带注释的默认配置；
    /// 该目录不可写则退到 %APPDATA%，保证绿色包放在只读位置也能跑。
    /// </summary>
    internal static AppConfig Load(string? explicitPath)
    {
        var config = new AppConfig();
        string path;

        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            path = Path.GetFullPath(explicitPath);
            Directory = Path.GetDirectoryName(path) is { Length: > 0 } dir ? dir : Directory;
        }
        else
        {
            string exeDir = AppContext.BaseDirectory;
            string exeDirConfig = Path.Combine(exeDir, ConfigFileName);
            if (File.Exists(exeDirConfig) || IsWritable(exeDir))
            {
                Directory = exeDir;
            }
            else
            {
                Directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    AppFolderName);
                System.IO.Directory.CreateDirectory(Directory);
            }

            path = Path.Combine(Directory, ConfigFileName);
        }

        config.ConfigPath = path;
        config.ConfigFileExists = File.Exists(path);

        if (!config.ConfigFileExists)
        {
            TryWriteDefault(path, config);
        }
        else
        {
            ParseIni(path, config);
        }

        Normalize(config);
        return config;
    }

    private static bool IsWritable(string dir)
    {
        try
        {
            string probe = Path.Combine(dir, ".wm-write-probe");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryWriteDefault(string path, AppConfig config)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, DefaultIni(config.Hotkey), new UTF8Encoding(false));
            config.ConfigFileExists = true;
            Log.Info($"已生成默认配置：{path}");
        }
        catch (Exception ex)
        {
            config.AddWarning($"无法生成配置文件 {path}：{ex.Message}");
        }
    }

    internal static string DefaultIni(string hotkey) =>
        $"""
        ; WindowMinimize 配置文件
        ; 改完保存即生效，不必重启（格式写错时会保留上一次可用的热键）
        ;
        ; Hotkey     修饰键用 + 连接，可用 Ctrl、Alt、Shift、Win；
        ;            主键可用 A-Z、0-9、F1-F24、Space、Esc、Enter、Tab、方向键等。
        ;            不带修饰键时只能用功能键，例如 F11。
        ;            示例：Win+S、Ctrl+Shift+Q、Ctrl+Alt+F9、Alt+Space
        ;            注意 Win+S 是系统保留组合（搜索），会被本程序接管，搜索将不再弹出。
        ; Mode       minimize = 最小化到任务栏（默认）；hide = 直接隐藏窗口
        ; AutoStart  是否跟随 Windows 启动
        ; Debug      是否写 hotkey.log 调试日志

        [general]
        Hotkey={hotkey}
        Mode=minimize
        AutoStart=true
        Debug=false

        """;

    private static void ParseIni(string path, AppConfig config)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(path, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            config.AddWarning($"读取配置失败，改用默认值：{ex.Message}");
            return;
        }

        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] is ';' or '#' or '[')
            {
                continue;
            }

            int split = line.IndexOf('=');
            if (split < 0)
            {
                split = line.IndexOf(':');
            }

            if (split <= 0)
            {
                config.AddWarning($"配置行无法解析，已忽略：{line}");
                continue;
            }

            string key = line[..split].Trim();
            string value = line[(split + 1)..].Trim().Trim('"');
            Log.Debug($"配置项 {key} = {value}");

            switch (key.ToLowerInvariant())
            {
                case "hotkey":
                case "hotkeykey":
                    if (value.Length > 0)
                    {
                        config.Hotkey = value;
                    }

                    break;

                case "mode":
                    config.Mode = value;
                    break;

                case "autostart":
                case "minimizetotray":
                    config.AutoStart = ParseBool(value, config.AutoStart ?? true);
                    break;

                case "debug":
                    config.Debug = ParseBool(value, config.Debug);
                    break;

                default:
                    config.AddWarning($"配置项 “{key}” 不是有效项，已忽略");
                    break;
            }
        }
    }

    private static bool ParseBool(string value, bool fallback) => value.ToLowerInvariant() switch
    {
        "1" or "true" or "yes" or "on" or "是" => true,
        "0" or "false" or "no" or "off" or "否" => false,
        _ => fallback,
    };

    private static void Normalize(AppConfig config)
    {
        if (!config.Mode.Equals("minimize", StringComparison.OrdinalIgnoreCase)
            && !config.Mode.Equals("hide", StringComparison.OrdinalIgnoreCase))
        {
            config.AddWarning($"Mode 只能填 minimize 或 hide，已按 minimize 处理");
            config.Mode = "minimize";
        }

        config.Mode = config.Mode.ToLowerInvariant();
    }

    // --------------------------------------------------------------- 热加载

    internal sealed class Watcher : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Action _onChanged;
        private long _lastEventTicks;

        internal Watcher(string path, Action onChanged)
        {
            _onChanged = onChanged;

            // 定时器只负责在 UI 线程上检查到期；文件事件可能来自后台线程，
            // 不能在那里直接操作 WinForms 定时器，只更新时间戳。
            _timer = new System.Windows.Forms.Timer { Interval = 400 };
            _timer.Tick += (_, _) => OnDebounceTick();
            _timer.Start();

            string dir = Path.GetDirectoryName(path)!;
            _watcher = new FileSystemWatcher(dir, Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
        }

        private void OnFileEvent(object? sender, FileSystemEventArgs e)
        {
            // 编辑器保存往往触发多次事件，这里只记时间，等静默一段时间再载入
            Interlocked.Exchange(ref _lastEventTicks, DateTime.UtcNow.Ticks);
        }

        private void OnDebounceTick()
        {
            long ticks = Interlocked.Read(ref _lastEventTicks);
            if (ticks == 0)
            {
                return;
            }

            if (DateTime.UtcNow.Ticks - ticks < TimeSpan.FromMilliseconds(600).Ticks)
            {
                return;
            }

            Interlocked.Exchange(ref _lastEventTicks, 0);
            Log.Debug("配置文件已稳定，开始重新载入");

            try
            {
                _onChanged();
            }
            catch (Exception ex)
            {
                Log.Error($"重新载入配置失败：{ex.Message}");
            }
        }

        public void Dispose()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _timer.Dispose();
        }
    }

    // --------------------------------------------------------------- 辅助

    internal static void OpenInNotepad(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(path, DefaultIni(AppConfig.DefaultHotkey), new UTF8Encoding(false));
            }

            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error($"打开配置失败：{ex.Message}");
        }
    }

    internal static bool IsAutoStartEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(RunValueName) is string value && value.Length > 0;
    }

    internal static void SetAutoStart(bool enabled)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        if (key is null)
        {
            Log.Error("无法打开开机自启注册表项");
            return;
        }

        if (enabled)
        {
            string exe = Environment.ProcessPath ?? Application.ExecutablePath;
            key.SetValue(RunValueName, $"\"{exe}\"");
            Log.Info($"已设置开机自启：{exe}");
        }
        else
        {
            if (key.GetValue(RunValueName) is not null)
            {
                key.DeleteValue(RunValueName, false);
            }

            Log.Info("已取消开机自启");
        }
    }
}
