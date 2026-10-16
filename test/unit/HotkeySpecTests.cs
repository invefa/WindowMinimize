namespace WindowMinimize.Tests;

/// <summary>
/// HotkeySpec.TryParse is a pure function, so this covers spelling normalization, modifier
/// recognition and every rejection rule. Those rules decide whether what the user writes into
/// hotkey.ini works at all, so this file has to be read along with any change to the parsing.
/// </summary>
public class HotkeySpecTests {
    private static HotkeySpec Parse(string text) {
        bool ok = HotkeySpec.TryParse(text, out HotkeySpec? spec, out string error);
        Assert.True(ok, $"should have parsed {text}, but it reported: {error}");
        return spec!;
    }

    private static string ParseError(string text) {
        bool ok = HotkeySpec.TryParse(text, out HotkeySpec? spec, out string error);
        Assert.False(ok, $"should not have parsed: {text}");
        Assert.Null(spec);
        Assert.NotEmpty(error);
        return error;
    }

    /// <summary>
    /// Spelling: case, separator and word order do not matter, the result always reads Win, Ctrl,
    /// Alt, Shift plus the main key.
    /// </summary>
    [Theory]
    [InlineData("Ctrl+Alt+M", "Ctrl+Alt+M")]
    [InlineData("ctrl+alt+m", "Ctrl+Alt+M")]
    [InlineData("Ctrl-Alt-M", "Ctrl+Alt+M")]
    [InlineData("Alt+Ctrl+M", "Ctrl+Alt+M")]
    [InlineData("M+Ctrl+Alt", "Ctrl+Alt+M")]
    [InlineData("Ctrl+Ctrl+M", "Ctrl+M")]
    [InlineData("Win+Alt+M", "Win+Alt+M")]
    [InlineData("Alt+Win+M", "Win+Alt+M")]
    [InlineData("Ctrl+Alt+Win+M", "Win+Ctrl+Alt+M")]
    [InlineData("win+s", "Win+S")]
    [InlineData("F9", "F9")]
    [InlineData("f24", "F24")]
    [InlineData("Ctrl+F9", "Ctrl+F9")]
    [InlineData("Alt+Space", "Alt+Space")]
    [InlineData("Ctrl+Esc", "Ctrl+Esc")]
    [InlineData("Ctrl+PageUp", "Ctrl+PageUp")]
    [InlineData("Ctrl+Left", "Ctrl+Left")]
    [InlineData("Ctrl+Num5", "Ctrl+Num5")]
    [InlineData("Ctrl+Minus", "Ctrl+Minus")]
    [InlineData("Ctrl+-", "Ctrl+Minus")]
    [InlineData("Ctrl++", "Ctrl+Plus")]
    [InlineData("Ctrl+Grave", "Ctrl+Grave")]
    [InlineData("Ctrl+Semicolon", "Ctrl+Semicolon")]
    public void NormalizesSpelling(string text, string expected) =>
        Assert.Equal(expected, Parse(text).Text);

    /// <summary>
    /// The modifiers may be written in any order, and the main key still lands on the right virtual
    /// key code.
    /// </summary>
    [Fact]
    public void ParsesModifiersAndKey() {
        HotkeySpec spec = Parse("Alt+M+Ctrl");

        Assert.Equal(Native.MOD_CONTROL | Native.MOD_ALT, spec.Modifiers);
        Assert.Equal((uint)'M', spec.VirtualKey);
    }

    /// <summary>A lone Win is a modifier too, not the main key.</summary>
    [Fact]
    public void TreatsLoneWinAsModifier() {
        HotkeySpec spec = Parse("S+Win");

        Assert.Equal(Native.MOD_WIN, spec.Modifiers);
        Assert.Equal((uint)'S', spec.VirtualKey);
    }

    /// <summary>Parsing the canonical text again has to give the same hotkey.</summary>
    [Theory]
    [InlineData("Win+S")]
    [InlineData("ctrl+alt+shift+f9")]
    [InlineData("Ctrl+-")]
    [InlineData("Ctrl+Num5")]
    public void CanonicalTextRoundTrips(string text) {
        HotkeySpec first = Parse(text);

        Assert.Equal(first, Parse(first.Text));
    }

    /// <summary>
    /// Spellings that must not be accepted: empty, no main key, two main keys, an unknown key name
    /// and bare keys that would steal normal typing.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("M")]
    [InlineData("1")]
    [InlineData("Space")]
    [InlineData("M+N")]
    [InlineData("Ctrl+M+N")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Foo")]
    [InlineData("Ctrl+Num99")]
    public void RejectsInvalidSpelling(string text) => ParseError(text);

    /// <summary>
    /// Combinations that Windows reserves or owns have to be rejected, they cannot be registered
    /// or would steal a system feature.
    /// </summary>
    [Theory]
    [InlineData("F12")]
    [InlineData("Ctrl+F12")]
    [InlineData("Alt+Tab")]
    [InlineData("Ctrl+Alt+Delete")]
    [InlineData("Win+L")]
    public void RejectsSystemReservedCombo(string text) => ParseError(text);

    /// <summary>
    /// A bare key only passes for function keys, so anything else keeps its normal typing.
    /// </summary>
    [Theory]
    [InlineData("F1", true)]
    [InlineData("F24", true)]
    [InlineData("M", false)]
    [InlineData("0", false)]
    public void BareKeyAllowsFunctionKeysOnly(string text, bool allowed) {
        bool parsed = HotkeySpec.TryParse(text, out _, out _);

        Assert.Equal(allowed, parsed);
    }

    /// <summary>
    /// Win combinations owned by Windows have to be recognized, registering them switches to the
    /// keyboard hook and the message says so.
    /// </summary>
    [Theory]
    [InlineData("Win+S", true)]
    [InlineData("Win+D", true)]
    [InlineData("Win+E", true)]
    [InlineData("Win+X", true)]
    [InlineData("Win+Q", false)]
    [InlineData("Ctrl+S", false)]
    [InlineData("Win+Alt+S", false)]
    public void DetectsWindowsOwnedWinCombo(string text, bool expected) {
        HotkeySpec spec = Parse(text);

        Assert.Equal(expected, HotkeySpec.IsWindowsOwnedWinCombo(spec.Modifiers, spec.VirtualKey));
    }

    /// <summary>
    /// Two hotkeys are the same when the modifier bits and the virtual key match.
    /// </summary>
    [Fact]
    public void EqualityUsesModifiersAndVirtualKey() {
        Assert.Equal(Parse("ctrl+alt+m"), Parse("Ctrl+Alt+M"));
        Assert.NotEqual(Parse("Ctrl+Alt+M"), Parse("Ctrl+Shift+M"));
    }
}
