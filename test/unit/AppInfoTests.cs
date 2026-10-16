using System.Reflection;

namespace WindowMinimize.Tests;

/// <summary>
/// The version in the tray menu and in the log comes from the assembly metadata, so it always says
/// which file is running.
/// </summary>
public class AppInfoTests {
    [Fact]
    public void VersionIsFilledAndHasNoSourceRevision() {
        Assert.False(string.IsNullOrWhiteSpace(AppInfo.Version));
        Assert.DoesNotContain('+', AppInfo.Version);
    }

    /// <summary>The source revision that the SDK appends has to be cut off.</summary>
    [Fact]
    public void VersionIsTheInformationalVersionWithoutTheRevision() {
        string informational = typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

        Assert.Equal(informational.Split('+')[0], AppInfo.Version);
    }
}
