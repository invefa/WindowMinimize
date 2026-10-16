using System.Globalization;
using System.Reflection;
using System.Resources;

namespace WindowMinimize;

/// <summary>
/// Every string the user can see. English is the neutral language and lives in
/// Resources\Strings.resx, the Chinese translation ships as a zh-Hans satellite assembly, so the
/// interface follows the system language on its own.
/// </summary>
internal static class Strings {
    private const string ResourceBaseName = "WindowMinimize.Resources.Strings";

    private static readonly ResourceManager Resources =
        new(ResourceBaseName, Assembly.GetExecutingAssembly());

    /// <summary>Language of the interface, from the system unless --lang overrides it.</summary>
    internal static CultureInfo Culture { get; private set; } = SystemCulture();

    /// <summary>Pick the language of this run, empty or unknown keeps the system one.</summary>
    internal static void Init(string? language) => Culture = Requested(language) ?? SystemCulture();

    // the tray icon and its menu
    internal static string AppName        => Get(nameof(AppName));
    internal static string MenuMinimize   => Get(nameof(MenuMinimize));
    internal static string MenuRestore    => Get(nameof(MenuRestore));
    internal static string MenuSuspend    => Get(nameof(MenuSuspend));
    internal static string MenuResume     => Get(nameof(MenuResume));
    internal static string MenuOpenConfig => Get(nameof(MenuOpenConfig));
    internal static string MenuReload     => Get(nameof(MenuReload));
    internal static string MenuAutoStart  => Get(nameof(MenuAutoStart));
    internal static string MenuExit       => Get(nameof(MenuExit));
    internal static string HotkeyNone     => Get(nameof(HotkeyNone));
    internal static string HotkeyNotSet   => Get(nameof(HotkeyNotSet));

    internal static string HotkeyMenuItem(string hotkey) =>
        Format(nameof(HotkeyMenuItem), hotkey);

    internal static string HotkeyMenuItemSuspended(string hotkey) =>
        Format(nameof(HotkeyMenuItemSuspended), hotkey);

    internal static string TrayTooltip(string hotkey) => Format(nameof(TrayTooltip), hotkey);

    // the settings dialog, the auto start check box reuses the menu text
    internal static string DialogTitle      => Get(nameof(DialogTitle));
    internal static string DialogHotkey     => Get(nameof(DialogHotkey));
    internal static string DialogSyntaxHint => Get(nameof(DialogSyntaxHint));
    internal static string DialogMode       => Get(nameof(DialogMode));
    internal static string DialogMinimize   => Get(nameof(DialogMinimize));
    internal static string DialogHide       => Get(nameof(DialogHide));
    internal static string DialogSave       => Get(nameof(DialogSave));
    internal static string DialogCancel     => Get(nameof(DialogCancel));

    // tray balloons
    internal static string BalloonStarted(string hotkey) =>
        Format(nameof(BalloonStarted), hotkey);

    internal static string BalloonStartedWindowsOwned(string hotkey) =>
        Format(nameof(BalloonStartedWindowsOwned), hotkey);

    internal static string BalloonStartedHook(string hotkey) =>
        Format(nameof(BalloonStartedHook), hotkey);

    internal static string BalloonHotkeyNotActive(string reason) =>
        Format(nameof(BalloonHotkeyNotActive), reason);

    internal static string BalloonAutoStartFailed(string reason) =>
        Format(nameof(BalloonAutoStartFailed), reason);

    // message boxes
    internal static string MsgAlreadyRunning => Get(nameof(MsgAlreadyRunning));

    internal static string MsgBadArguments(string reason) =>
        Format(nameof(MsgBadArguments), reason);

    internal static string MsgUnknownArgument(string argument) =>
        Format(nameof(MsgUnknownArgument), argument);

    internal static string MsgArgumentNeedsValue(string argument) =>
        Format(nameof(MsgArgumentNeedsValue), argument);

    internal static string Usage => Get(nameof(Usage));

    // hotkey parsing and registration, shown in the dialog, in a balloon and in the log
    internal static string ErrorHotkeyEmpty => Get(nameof(ErrorHotkeyEmpty));

    internal static string ErrorOnlyOneMainKey(string first, string second) =>
        Format(nameof(ErrorOnlyOneMainKey), first, second);

    internal static string ErrorUnknownKeyName(string keyName) =>
        Format(nameof(ErrorUnknownKeyName), keyName);

    internal static string ErrorMainKeyMissing      => Get(nameof(ErrorMainKeyMissing));
    internal static string ErrorF12Reserved         => Get(nameof(ErrorF12Reserved));
    internal static string ErrorBareKeyOnlyFunction => Get(nameof(ErrorBareKeyOnlyFunction));
    internal static string ErrorReservedCombo       => Get(nameof(ErrorReservedCombo));

    internal static string ErrorComboTaken(string hotkey) =>
        Format(nameof(ErrorComboTaken), hotkey);

    internal static string ErrorRegisterFailed(string hotkey, string code) =>
        Format(nameof(ErrorRegisterFailed), hotkey, code);

    internal static string ErrorFallbackUsed(string reason, string hotkey) =>
        Format(nameof(ErrorFallbackUsed), reason, hotkey);

    internal static string ErrorHookInstallFailed(string code) =>
        Format(nameof(ErrorHookInstallFailed), code);

    internal static string ErrorHookFailed(string reason, string hookError) =>
        Format(nameof(ErrorHookFailed), reason, hookError);

    internal static string Get(string key) => Resources.GetString(key, Culture) ?? key;

    internal static string Format(string key, params object?[] args) =>
        string.Format(Culture, Get(key), args);

    /// <summary>
    /// The culture of a --lang value. "zh" and "en" are the short forms the usage text documents,
    /// anything else is taken as a culture name such as zh-Hant.
    /// </summary>
    private static CultureInfo? Requested(string? language) {
        if (string.IsNullOrWhiteSpace(language)) return null;

        string name = language.Trim().ToLowerInvariant();
        if (name is "zh" or "cn" or "chinese") name = "zh-Hans";
        if (name is "en" or "english") name = "en";

        try {
            return CultureInfo.GetCultureInfo(name);
        } catch (ArgumentException) {
            return null;
        }
    }

    private static CultureInfo SystemCulture() => CultureInfo.CurrentUICulture;
}
