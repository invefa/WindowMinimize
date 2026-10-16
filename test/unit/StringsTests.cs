using System.Collections;
using System.Globalization;
using System.Resources;

namespace WindowMinimize.Tests;

/// <summary>
/// The interface follows the system language, so the English and the Chinese resource file have to
/// stay in step. The checks compare the two languages against each other, no translation is spelled
/// out here, otherwise a wording change would break the test for no reason.
/// </summary>
public class StringsTests {
    private const string ResourceBaseName = "WindowMinimize.Resources.Strings";

    private static readonly ResourceManager Resources =
        new(ResourceBaseName, typeof(HotkeySpec).Assembly);

    private static readonly CultureInfo English = CultureInfo.InvariantCulture;
    private static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-Hans");

    private static List<string> Keys(CultureInfo culture) {
        ResourceSet? set = Resources.GetResourceSet(culture, true, false);
        Assert.NotNull(set);

        var keys = new List<string>();
        foreach (DictionaryEntry entry in set) keys.Add((string)entry.Key);
        return keys;
    }

    /// <summary>A key in one language only would silently fall back to English.</summary>
    [Fact]
    public void BothLanguagesUseTheSameKeys() {
        List<string> english = Keys(English);
        List<string> chinese = Keys(Chinese);

        Assert.NotEmpty(english);
        Assert.Equal(english.OrderBy(key => key), chinese.OrderBy(key => key));
    }

    /// <summary>Every key has to carry text in both languages.</summary>
    [Fact]
    public void EveryKeyIsTranslated() {
        foreach (string key in Keys(English)) {
            string? english = Resources.GetString(key, English);
            string? chinese = Resources.GetString(key, Chinese);

            Assert.False(string.IsNullOrWhiteSpace(english), $"{key} has no English text");
            Assert.False(string.IsNullOrWhiteSpace(chinese), $"{key} has no Chinese text");

            // the product name is the same in both languages, everything else has to differ
            if (key == nameof(Strings.AppName)) continue;
            Assert.NotEqual(english, chinese);
        }
    }

    /// <summary>The same key has to keep the same placeholders in both languages.</summary>
    [Fact]
    public void PlaceholdersMatch() {
        foreach (string key in Keys(English)) {
            Assert.Equal(FormatHolders(Resources.GetString(key, English)),
                FormatHolders(Resources.GetString(key, Chinese)));
        }
    }

    /// <summary>--lang picks the language, an unknown value keeps the system one.</summary>
    [Fact]
    public void InitFollowsTheRequestedLanguage() {
        try {
            Strings.Init("zh");
            Assert.Equal(Resources.GetString(nameof(Strings.MenuExit), Chinese), Strings.MenuExit);

            Strings.Init("en");
            Assert.Equal(
                Resources.GetString(nameof(Strings.MenuExit), English), Strings.MenuExit
            );
        } finally {
            Strings.Init(null);
        }
    }

    /// <summary>Formatting fills in the arguments of the selected language.</summary>
    [Fact]
    public void FormatUsesTheSelectedLanguage() {
        try {
            Strings.Init("zh");
            string chinese = Strings.ErrorUnknownKeyName("Ctrl+Foo");

            Strings.Init("en");
            string english = Strings.ErrorUnknownKeyName("Ctrl+Foo");

            Assert.NotEqual(english, chinese);
            Assert.Contains("Ctrl+Foo", english);
            Assert.Contains("Ctrl+Foo", chinese);
        } finally {
            Strings.Init(null);
        }
    }

    private static string FormatHolders(string? text) {
        if (text is null) return string.Empty;

        var holders = new List<string>();
        for (int i = 0; i < text.Length; i++) {
            if (text[i] != '{' || i + 2 >= text.Length || text[i + 2] != '}') continue;

            holders.Add(text.Substring(i, 3));
        }

        return string.Join(',', holders);
    }
}
