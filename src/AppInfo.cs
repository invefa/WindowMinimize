using System.Reflection;

namespace WindowMinimize;

/// <summary>
/// The version of this program, read from the assembly metadata so the tray menu, the log and the
/// released tag cannot drift apart.
/// </summary>
internal static class AppInfo {
    /// <summary>The version as the project file writes it, for example 1.0.1.</summary>
    internal static string Version { get; } = ReadVersion();

    private static string ReadVersion() {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // the informational version can carry the source revision behind a plus sign, and the menu
        // wants the plain version
        if (!string.IsNullOrEmpty(informational)) return informational.Split('+')[0];

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
