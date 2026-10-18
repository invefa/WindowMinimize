namespace WindowMinimize;

/// <summary>
/// Runtime configuration. Precedence: command line, then hotkey.ini, then the built-in defaults.
/// </summary>
internal sealed class AppConfig {
    internal const string DefaultHotkey = "Win+S";

    /// <summary>Hotkey text, for example Win+Alt+M.</summary>
    internal string Hotkey { get; set; } = DefaultHotkey;

    /// <summary>minimize = minimize to the taskbar, hide = hide the window outright.</summary>
    internal string Mode { get; set; } = "minimize";

    /// <summary>
    /// Start with Windows. null means the config file does not mention it, in which case the
    /// registry is left alone, so a change the user made by hand is never overwritten.
    /// </summary>
    internal bool? AutoStart { get; set; }

    /// <summary>Write the debug log to hotkey.log.</summary>
    internal bool Debug { get; set; }

    /// <summary>Interface language: auto follows Windows, en or zh forces one.</summary>
    internal string Language { get; set; } = "auto";

    /// <summary>Config file path, filled in by ConfigStore.</summary>
    internal string ConfigPath { get; set; } = string.Empty;

    /// <summary>Whether the config file exists.</summary>
    internal bool ConfigFileExists { get; set; }

    /// <summary>Problems found while parsing, shown to the user.</summary>
    internal List<string> Warnings { get; } = [];

    internal void AddWarning(string message) {
        if (!Warnings.Contains(message)) Warnings.Add(message);
    }

    internal sealed class CliResult {
        internal bool ShowHelp { get; set; }

        internal bool NoTray { get; set; }

        internal string? Error { get; set; }

        internal string? ExplicitConfigPath { get; set; }

        /// <summary>Language of the interface, from --lang.</summary>
        internal string? Language { get; set; }

        /// <summary>--hotkey was given on the command line.</summary>
        internal bool HasHotkey { get; set; }

        /// <summary>--mode was given on the command line.</summary>
        internal bool HasMode { get; set; }
    }

    /// <summary>
    /// Parse the command line overrides. Only known switches are handled and everything else is an
    /// error, so a typo cannot silently look like it worked.
    /// </summary>
    internal static CliResult ApplyCommandLine(AppConfig config, string[] args) {
        var result = new CliResult();

        for (int i = 0; i < args.Length; i++) {
            string arg = args[i];
            switch (arg.ToLowerInvariant()) {
                case "-h":
                case "-?":
                case "/?":
                case "--help":
                    result.ShowHelp = true;
                    break;

                case "--hotkey":
                case "-hotkey":
                    if (!TryTakeValue(args, ref i, arg, out string hotkey, out string error)) {
                        result.Error = error;
                        return result;
                    }

                    config.Hotkey = hotkey;
                    result.HasHotkey = true;
                    break;

                case "--mode":
                case "-mode":
                    if (!TryTakeValue(args, ref i, arg, out string mode, out string modeError)) {
                        result.Error = modeError;
                        return result;
                    }

                    config.Mode = mode;
                    result.HasMode = true;
                    break;

                case "--config":
                case "-config":
                    if (!TryTakeValue(args, ref i, arg, out string path, out string pathError)) {
                        result.Error = pathError;
                        return result;
                    }

                    result.ExplicitConfigPath = path;
                    break;

                case "--lang":
                case "-lang":
                    if (!TryTakeValue(args, ref i, arg, out string lang, out string langError)) {
                        result.Error = langError;
                        return result;
                    }

                    result.Language = lang;
                    break;

                case "--debug":
                case "-debug":
                    config.Debug = true;
                    break;

                case "--no-tray":
                case "-no-tray":
                case "--quiet":
                    result.NoTray = true;
                    break;

                default:
                    if (arg.StartsWith("--hotkey=", StringComparison.OrdinalIgnoreCase)) {
                        config.Hotkey = arg["--hotkey=".Length..];
                        result.HasHotkey = true;
                    } else if (arg.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase)) {
                        config.Mode = arg["--mode=".Length..];
                        result.HasMode = true;
                    } else if (arg.StartsWith("--config=", StringComparison.OrdinalIgnoreCase)) {
                        result.ExplicitConfigPath = arg["--config=".Length..];
                    } else if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase)) {
                        result.Language = arg["--lang=".Length..];
                    } else {
                        result.Error = Strings.MsgUnknownArgument(arg);
                        return result;
                    }

                    break;
            }
        }

        return result;
    }

    private static bool TryTakeValue(
        string[] args, ref int index, string name, out string value, out string error
    ) {
        value = string.Empty;
        error = string.Empty;

        if (index + 1 >= args.Length) {
            error = Strings.MsgArgumentNeedsValue(name);
            return false;
        }

        index++;
        value = args[index];
        return true;
    }
}
