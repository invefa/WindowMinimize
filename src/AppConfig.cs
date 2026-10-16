using System.Diagnostics;
using System.Text;

namespace WindowMinimize;

/// <summary>
/// 运行配置。优先级：命令行参数 &gt; hotkey.ini &gt; 内置默认值。
/// </summary>
internal sealed class AppConfig
{
    internal const string DefaultHotkey = "Win+S";

    /// <summary>热键写法，例如 Win+Alt+M。</summary>
    internal string Hotkey { get; set; } = DefaultHotkey;

    /// <summary>minimize = 最小化到任务栏；hide = 直接隐藏窗口。</summary>
    internal string Mode { get; set; } = "minimize";

    /// <summary>
    /// 跟随 Windows 启动。null 表示配置文件里没写这一项，此时不动注册表，
    /// 避免覆盖用户手动在任务管理器里做的改动。
    /// </summary>
    internal bool? AutoStart { get; set; }

    /// <summary>写日志到 hotkey.log。</summary>
    internal bool Debug { get; set; }

    /// <summary>配置文件路径（由 ConfigStore 填）。</summary>
    internal string ConfigPath { get; set; } = string.Empty;

    /// <summary>配置文件是否存在。</summary>
    internal bool ConfigFileExists { get; set; }

    /// <summary>解析过程中的问题，用于提示用户。</summary>
    internal List<string> Warnings { get; } = [];

    internal void AddWarning(string message)
    {
        if (!Warnings.Contains(message))
        {
            Warnings.Add(message);
        }
    }

    // ------------------------------------------------------------- 命令行

    internal sealed class CliResult
    {
        internal bool ShowHelp { get; set; }

        internal bool NoTray { get; set; }

        internal string? Error { get; set; }

        internal string? ExplicitConfigPath { get; set; }

        /// <summary>命令行里显式给了 --hotkey。</summary>
        internal bool HasHotkey { get; set; }

        /// <summary>命令行里显式给了 --mode。</summary>
        internal bool HasMode { get; set; }
    }

    /// <summary>
    /// 解析命令行覆盖项。只处理已知开关，其余一律报错，避免用户写错了还以为生效了。
    /// </summary>
    internal static CliResult ApplyCommandLine(AppConfig config, string[] args)
    {
        var result = new CliResult();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "-h":
                case "-?":
                case "/?":
                case "--help":
                    result.ShowHelp = true;
                    break;

                case "--hotkey":
                case "-hotkey":
                    if (!TryTakeValue(args, ref i, arg, out string hotkey, out string error))
                    {
                        result.Error = error;
                        return result;
                    }

                    config.Hotkey = hotkey;
                    result.HasHotkey = true;
                    break;

                case "--mode":
                case "-mode":
                    if (!TryTakeValue(args, ref i, arg, out string mode, out string modeError))
                    {
                        result.Error = modeError;
                        return result;
                    }

                    config.Mode = mode;
                    result.HasMode = true;
                    break;

                case "--config":
                case "-config":
                    if (!TryTakeValue(args, ref i, arg, out string path, out string pathError))
                    {
                        result.Error = pathError;
                        return result;
                    }

                    result.ExplicitConfigPath = path;
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
                    if (arg.StartsWith("--hotkey=", StringComparison.OrdinalIgnoreCase))
                    {
                        config.Hotkey = arg["--hotkey=".Length..];
                        result.HasHotkey = true;
                    }
                    else if (arg.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase))
                    {
                        config.Mode = arg["--mode=".Length..];
                        result.HasMode = true;
                    }
                    else if (arg.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
                    {
                        result.ExplicitConfigPath = arg["--config=".Length..];
                    }
                    else
                    {
                        result.Error = $"无法识别的参数 “{arg}”";
                        return result;
                    }

                    break;
            }
        }

        return result;
    }

    private static bool TryTakeValue(string[] args, ref int index, string name, out string value, out string error)
    {
        value = string.Empty;
        error = string.Empty;

        if (index + 1 >= args.Length)
        {
            error = $"参数 {name} 后面缺少取值";
            return false;
        }

        index++;
        value = args[index];
        return true;
    }

    internal static string Usage() =>
        """
        WindowMinimize - 按热键最小化当前窗口

        用法:
          WindowMinimize.exe [选项]

        选项:
          --hotkey <组合>   指定热键，例如 --hotkey Ctrl+Alt+M
                            写法: 修饰键(Ctrl/Alt/Shift/Win) + 主键(A-Z、0-9、F1-F24、
                            Space、Esc、Enter 等)，用 + 连接；不带修饰键时只能用功能键。
          --mode <方式>     minimize（默认，最小化到任务栏）或 hide（直接隐藏窗口）
          --config <路径>   指定配置文件，默认读 exe 同目录的 hotkey.ini
          --debug           写调试日志到 exe 同目录的 hotkey.log
          --no-tray         不显示托盘图标
          --help            显示本帮助

        配置文件 hotkey.ini 示例:
          [general]
          Hotkey=Win+S
          Mode=minimize
          AutoStart=true
          Debug=false

        说明:
          双击运行后会常驻托盘，按热键最小化当时的前台窗口；托盘菜单可最小化一次、
          暂停热键、打开配置、开机自启、退出。修改 hotkey.ini 后自动生效。
        """;
}
