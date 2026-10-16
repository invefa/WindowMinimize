using System.Text;
using System.Windows.Forms;

namespace WindowMinimize;

internal static class Program
{
    private const string MutexName = @"Local\WindowMinimize.SingleInstance";

    [STAThread]
    private static int Main(string[] args)
    {
        SetupConsoleEncoding();

        // 先把参数解析成覆盖项，再读配置文件，最后只把显式给出的覆盖项盖上去
        AppConfig overrides = new();
        AppConfig.CliResult cli = AppConfig.ApplyCommandLine(overrides, args);

        AppConfig config = ConfigStore.Load(cli.ExplicitConfigPath);

        // 参数有问题时也要把日志定下来，方便排查启动阶段的问题
        if (cli.Error is not null)
        {
            Log.Init(ConfigStore.LogPath, true);
            Log.Error("参数错误: " + cli.Error);
            WriteError("参数错误: " + cli.Error + Environment.NewLine + Environment.NewLine + AppConfig.Usage());
            return 2;
        }

        if (cli.ShowHelp)
        {
            WriteUsage(AppConfig.Usage());
            return 0;
        }

        if (cli.HasHotkey)
        {
            config.Hotkey = overrides.Hotkey;
        }

        if (cli.HasMode)
        {
            config.Mode = overrides.Mode;
        }

        if (overrides.Debug)
        {
            config.Debug = true;
        }

        Log.Init(ConfigStore.LogPath, config.Debug);

        using var mutex = new Mutex(true, MutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            Log.Info("已经有一个 WindowMinimize 在运行，本次退出");
            if (!cli.NoTray)
            {
                MessageBox.Show("WindowMinimize 已经在运行，请查看任务栏通知区域。", "WindowMinimize",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return 1;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Log.Info($"WindowMinimize 启动，配置热键 {config.Hotkey}，方式 {config.Mode}，配置文件 {config.ConfigPath}");
        foreach (string warning in config.Warnings)
        {
            Log.Error(warning);
        }

        var app = new TrayApp(config, cli.NoTray);

        if (app.Apply(config, initial: true))
        {
            string message = app.IsUsingKeyboardHook
                ? $"已启动，热键 {app.ActiveSpecText}（该组合被其他程序占用，已用按键钩子接管）"
                : $"已启动，热键 {app.ActiveSpecText}，按一次最小化当前窗口";
            app.Notify("WindowMinimize", message);
        }
        else
        {
            Log.Error($"热键未生效：{app.LastError}");
            app.Notify("WindowMinimize", $"热键未生效：{app.LastError}", ToolTipIcon.Warning);

            // 自动退回默认热键也失败时才打扰用户，避免每次开机弹窗
            if (app.ActiveSpec is null)
            {
                app.ShowSettingsDialog();
            }
        }

        Application.Run(app);
        Log.Info("WindowMinimize 退出");
        return 0;
    }

    /// <summary>
    /// 输出帮助。双击运行（无控制台、输出也没重定向）时改用消息框，否则用户什么也看不到。
    /// </summary>
    private static void WriteUsage(string text)
    {
        if (!Console.IsOutputRedirected && !HasConsole())
        {
            MessageBox.Show(text, "WindowMinimize", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Console.WriteLine(text);
    }

    private static void WriteError(string text)
    {
        if (!Console.IsOutputRedirected && !HasConsole())
        {
            MessageBox.Show(text, "WindowMinimize", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Console.WriteLine(text);
    }

    private static bool HasConsole()
    {
        try
        {
            return GetConsoleWindow() != IntPtr.Zero;
        }
        catch
        {
            return false;
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    /// <summary>
    /// 控制台编码：输出被重定向时用系统 OEM 代码页（中文系统即 GBK），
    /// 直接挂控制台时用 UTF-8，避免中文乱码。
    /// </summary>
    private static void SetupConsoleEncoding()
    {
        try
        {
            Console.OutputEncoding = Console.IsOutputRedirected
                ? Encoding.GetEncoding(GetOemCodePage())
                : new UTF8Encoding(false);
        }
        catch
        {
            // 没有控制台或代码页不可用时忽略
        }
    }

    private static int GetOemCodePage()
    {
        try
        {
            uint acp = GetACP();
            return acp is 936 or 950 or 932 or 949 ? (int)acp : 936;
        }
        catch
        {
            return 936;
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint GetACP();
}
