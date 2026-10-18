using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace WindowMinimize;

/// <summary>
/// Locating, reading, writing and live reloading of the config file.
/// </summary>
internal static class ConfigStore {
    private const string AppFolderName = "WindowMinimize";
    private const string ConfigFileName = "hotkey.ini";
    private const string LogFileName = "hotkey.log";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "WindowMinimize";

    /// <summary>
    /// Directory the config lives in: next to the exe, or %APPDATA%\WindowMinimize when the exe
    /// directory is not writable.
    /// </summary>
    internal static string Directory { get; private set; } = AppContext.BaseDirectory;

    internal static string LogPath => Path.Combine(Directory, LogFileName);

    /// <summary>
    /// Read the config. When there is no hotkey.ini next to the exe, a commented default one is
    /// written there first, and the location falls back to %APPDATA% when that directory is not
    /// writable, so a portable copy can also live in a read only place.
    /// </summary>
    internal static AppConfig Load(string? explicitPath) {
        var config = new AppConfig();
        string path = ResolvePath(explicitPath);

        config.ConfigPath = path;
        config.ConfigFileExists = File.Exists(path);

        if (!config.ConfigFileExists)
            TryWriteDefault(path, config);
        else
            ParseIni(path, config);

        Normalize(config);
        return config;
    }

    /// <summary>
    /// Work out where the config and the log live, without reading or writing the config file. The
    /// help and the bad argument paths use it so they can point the log somewhere without leaving a
    /// default hotkey.ini behind.
    /// </summary>
    internal static string ResolvePath(string? explicitPath) {
        string path;

        if (!string.IsNullOrWhiteSpace(explicitPath)) {
            path = Path.GetFullPath(explicitPath);
            Directory = Path.GetDirectoryName(path) is { Length: > 0 } dir ? dir : Directory;
        } else {
            string exeDir = AppContext.BaseDirectory;
            string exeDirConfig = Path.Combine(exeDir, ConfigFileName);
            if (File.Exists(exeDirConfig) || IsWritable(exeDir)) {
                Directory = exeDir;
            } else {
                Directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    AppFolderName
                );
                System.IO.Directory.CreateDirectory(Directory);
            }

            path = Path.Combine(Directory, ConfigFileName);
        }

        return path;
    }

    private static bool IsWritable(string dir) {
        try {
            string probe = Path.Combine(dir, ".wm-write-probe");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        } catch {
            return false;
        }
    }

    private static void TryWriteDefault(string path, AppConfig config) {
        try {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, DefaultIni(config.Hotkey), new UTF8Encoding(false));
            config.ConfigFileExists = true;
            Log.Info($"wrote the default config: {path}");
        } catch (Exception ex) {
            config.AddWarning($"cannot write the config file {path}: {ex.Message}");
        }
    }

    internal static string DefaultIni(string hotkey) =>
        $"""
        ; WindowMinimize configuration file
        ; saving the file takes effect right away, no restart needed (a broken line keeps the
        ; hotkey that worked last)
        ;
        ; Hotkey     modifiers Ctrl, Alt, Shift and Win joined with +, plus one main key:
        ;            A-Z, 0-9, F1-F24, Space, Esc, Enter, Tab, the arrow keys and so on.
        ;            without a modifier only function keys work, F11 for example.
        ;            examples: Win+S, Ctrl+Shift+Q, Ctrl+Alt+F9, Alt+Space
        ;            note: Win+S is reserved by Windows (search), this program takes it over and
        ;            the search panel stops opening.
        ; Mode       minimize = minimize to the taskbar (default), hide = hide the window
        ; AutoStart  start with Windows, true or false to take effect, leave the line out to keep
        ;            the registry untouched
        ; Debug      write the hotkey.log debug log

        [general]
        Hotkey={hotkey}
        Mode=minimize
        Debug=false

        """;

    private static void ParseIni(string path, AppConfig config) {
        string[] lines;
        try {
            lines = File.ReadAllLines(path, Encoding.UTF8);
        } catch (Exception ex) {
            config.AddWarning($"cannot read the config, using the defaults: {ex.Message}");
            return;
        }

        foreach (string raw in lines) {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] is ';' or '#' or '[') continue;

            int split = line.IndexOf('=');
            if (split < 0) split = line.IndexOf(':');

            if (split <= 0) {
                config.AddWarning($"cannot parse the config line, ignoring it: {line}");
                continue;
            }

            string key = line[..split].Trim();
            string value = line[(split + 1)..].Trim().Trim('"');
            Log.Debug($"config entry {key} = {value}");

            switch (key.ToLowerInvariant()) {
                case "hotkey":
                case "hotkeykey":
                    if (value.Length > 0) config.Hotkey = value;
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
                    config.AddWarning($"unknown config entry {key}, ignoring it");
                    break;
            }
        }
    }

    private static bool ParseBool(string value, bool fallback) => value.ToLowerInvariant() switch {
        "1" or "true" or "yes" or "on" => true,
        "0" or "false" or "no" or "off" => false,
        _ => fallback,
    };

    private static void Normalize(AppConfig config) {
        if (!config.Mode.Equals("minimize", StringComparison.OrdinalIgnoreCase)
            && !config.Mode.Equals("hide", StringComparison.OrdinalIgnoreCase)) {
            config.AddWarning("Mode must be minimize or hide, using minimize");
            config.Mode = "minimize";
        }

        config.Mode = config.Mode.ToLowerInvariant();
    }

    internal sealed class Watcher : IDisposable {
        private readonly FileSystemWatcher _watcher;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Action _onChanged;
        private long _lastEventTicks;

        internal Watcher(string path, Action onChanged) {
            _onChanged = onChanged;

            // the timer only checks on the UI thread whether the file went quiet: file events can
            // arrive on a background thread, where a WinForms timer must not be touched, so only
            // the timestamp is passed between the two
            _timer = new System.Windows.Forms.Timer { Interval = 400 };
            _timer.Tick += (_, _) => OnDebounceTick();
            _timer.Start();

            string dir = Path.GetDirectoryName(path)!;
            _watcher = new FileSystemWatcher(dir, Path.GetFileName(path)) {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                    | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnFileEvent;
            _watcher.Created += OnFileEvent;
            // editors often write a temporary file and rename it, which only raises Renamed
            _watcher.Renamed += (sender, e) => OnFileEvent(sender, e);
        }

        private void OnFileEvent(object? sender, FileSystemEventArgs e) {
            // one save raises several events, so only the time is kept and the file is loaded once
            // the events went quiet
            Interlocked.Exchange(ref _lastEventTicks, DateTime.UtcNow.Ticks);
        }

        private void OnDebounceTick() {
            long ticks = Interlocked.Read(ref _lastEventTicks);
            if (ticks == 0) return;

            if (DateTime.UtcNow.Ticks - ticks < TimeSpan.FromMilliseconds(600).Ticks) return;

            Interlocked.Exchange(ref _lastEventTicks, 0);
            Log.Debug("the config file settled down, reloading");

            try {
                _onChanged();
            } catch (Exception ex) {
                Log.Error($"reload failed: {ex.Message}");
            }
        }

        public void Dispose() {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _timer.Dispose();
        }
    }

    internal static void OpenInNotepad(string path) {
        try {
            if (!File.Exists(path)) {
                string defaultText = DefaultIni(AppConfig.DefaultHotkey);
                File.WriteAllText(path, defaultText, new UTF8Encoding(false));
            }

            var startInfo = new ProcessStartInfo("notepad.exe", $"\"{path}\"") {
                UseShellExecute = true,
            };
            Process.Start(startInfo);
        } catch (Exception ex) {
            Log.Error($"cannot open the config: {ex.Message}");
        }
    }

    internal static bool IsAutoStartEnabled() {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(RunValueName) is string value && value.Length > 0;
    }

    internal static void SetAutoStart(bool enabled) {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        if (key is null) {
            Log.Error("cannot open the Run registry key");
            return;
        }

        if (enabled) {
            string exe = Environment.ProcessPath ?? Application.ExecutablePath;
            key.SetValue(RunValueName, $"\"{exe}\"");
            Log.Info($"auto start set to {exe}");
        } else {
            if (key.GetValue(RunValueName) is not null) key.DeleteValue(RunValueName, false);
            Log.Info("auto start removed");
        }
    }
}
