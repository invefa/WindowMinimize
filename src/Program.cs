using System.Text;
using System.Windows.Forms;

namespace WindowMinimize;

internal static class Program {
    private const string MutexName = @"Local\WindowMinimize.SingleInstance";

    [STAThread]
    private static int Main(string[] args) {
        SetupConsoleEncoding();

        // arguments first, config file next, then only the explicit overrides are applied on top
        AppConfig overrides = new();
        AppConfig.CliResult cli = AppConfig.ApplyCommandLine(overrides, args);

        // before anything can be shown, the interface language has to be known
        Strings.Init(cli.Language);

        // help and bad arguments take the log location but leave no config file behind
        if (cli.Error is not null) {
            ConfigStore.ResolvePath(cli.ExplicitConfigPath);
            Log.Init(ConfigStore.LogPath, true);
            Log.Error("bad arguments: " + cli.Error);
            string text = Strings.MsgBadArguments(cli.Error) + Environment.NewLine
                + Environment.NewLine + UsageText();
            WriteError(text);
            return 2;
        }

        if (cli.ShowHelp) {
            WriteUsage(UsageText());
            return 0;
        }

        AppConfig config = ConfigStore.Load(cli.ExplicitConfigPath);

        // the config file now supplies the language unless --lang was given
        if (cli.Language is null) Strings.Init(config.Language);

        if (cli.HasHotkey) config.Hotkey = overrides.Hotkey;
        if (cli.HasMode) config.Mode = overrides.Mode;
        if (overrides.Debug) config.Debug = true;

        Log.Init(ConfigStore.LogPath, config.Debug);

        using var mutex = new Mutex(true, MutexName, out bool isFirstInstance);
        if (!isFirstInstance) {
            Log.Info("another WindowMinimize is already running, exiting");
            if (!cli.NoTray) {
                MessageBox.Show(Strings.MsgAlreadyRunning, Strings.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return 1;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Log.Info($"WindowMinimize started, version {AppInfo.Version}, hotkey {config.Hotkey}, "
            + $"mode {config.Mode}, config {config.ConfigPath}");
        foreach (string warning in config.Warnings) Log.Error(warning);

        var app = new TrayApp(config, cli.NoTray);

        if (app.Apply(config, initial: true)) {
            string message;
            if (app.IsUsingHookForWindowsCombo) {
                message = Strings.BalloonStartedWindowsOwned(app.ActiveSpecText);
            } else if (app.IsUsingKeyboardHook) {
                message = Strings.BalloonStartedHook(app.ActiveSpecText);
            } else {
                message = Strings.BalloonStarted(app.ActiveSpecText);
            }

            app.Notify(message);
        } else {
            string message = Strings.BalloonHotkeyNotActive(app.LastError);
            Log.Error(message);
            app.Notify(message, ToolTipIcon.Warning);

            // only bother the user when even the fallback to the default hotkey failed, so a broken
            // config does not pop a window at every boot; --no-tray is the headless mode used by
            // scripts, where a failure is only logged
            if (app.ActiveSpec is null && !cli.NoTray) app.ShowSettingsDialog();
        }

        Application.Run(app);
        Log.Info("WindowMinimize exiting");
        return 0;
    }

    /// <summary>
    /// The usage text names the executable that was actually started, so the framework dependent
    /// build does not tell the user to run a file that is not there.
    /// </summary>
    private static string UsageText() {
        string exeName = Path.GetFileName(Environment.ProcessPath ?? "WindowMinimize.exe");
        return Strings.Usage.Replace("WindowMinimize.exe", exeName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Print the usage text. When started by double click there is no console and no redirected
    /// output, so a message box is used instead, otherwise the user would see nothing at all.
    /// </summary>
    private static void WriteUsage(string text) {
        if (!Console.IsOutputRedirected && !HasConsole()) {
            MessageBox.Show(text, Strings.AppName, MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Console.WriteLine(text);
    }

    private static void WriteError(string text) {
        if (!Console.IsOutputRedirected && !HasConsole()) {
            MessageBox.Show(text, Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Console.WriteLine(text);
    }

    private static bool HasConsole() {
        try {
            return GetConsoleWindow() != IntPtr.Zero;
        } catch {
            return false;
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    /// <summary>
    /// Console encoding: the system OEM code page when the output is redirected, UTF-8 when a
    /// console is attached, so the text is never garbled.
    /// </summary>
    private static void SetupConsoleEncoding() {
        try {
            Console.OutputEncoding = Console.IsOutputRedirected
                ? Encoding.GetEncoding(GetOemCodePage())
                : new UTF8Encoding(false);
        } catch {
            // ignore, there is no console or the code page is unavailable
        }
    }

    private static int GetOemCodePage() {
        try {
            uint acp = GetACP();
            return acp is 936 or 950 or 932 or 949 ? (int)acp : 936;
        } catch {
            return 936;
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint GetACP();
}
