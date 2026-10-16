namespace WindowMinimize.Tests;

/// <summary>
/// HotkeySpec.TryParse 是纯函数，这里覆盖写法规范化、修饰键识别和各类拒绝规则。
/// 这些规则直接决定用户写在 hotkey.ini 里的东西能不能生效，改解析逻辑时必须一起看。
/// </summary>
public class HotkeySpecTests
{
    private static HotkeySpec Parse(string text)
    {
        Assert.True(HotkeySpec.TryParse(text, out HotkeySpec? spec, out string error), $"应当能解析 “{text}”，实际报错：{error}");
        return spec!;
    }

    private static string ParseError(string text)
    {
        Assert.False(HotkeySpec.TryParse(text, out HotkeySpec? spec, out string error), $"不该解析成功：{text}");
        Assert.Null(spec);
        Assert.NotEmpty(error);
        return error;
    }

    /// <summary>写法规范化：大小写、分隔符、词序都不影响结果，回显统一成 Win、Ctrl、Alt、Shift 加主键。</summary>
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
    public void NormalizesSpelling(string text, string expected) => Assert.Equal(expected, Parse(text).Text);

    /// <summary>修饰键本身怎么排都认，主键也要落对虚拟键码。</summary>
    [Fact]
    public void ParsesModifiersAndKey()
    {
        HotkeySpec spec = Parse("Alt+M+Ctrl");

        Assert.Equal(Native.MOD_CONTROL | Native.MOD_ALT, spec.Modifiers);
        Assert.Equal((uint)'M', spec.VirtualKey);
    }

    /// <summary>只有 Win 一个修饰键时也要认成修饰键，而不是当成主键。</summary>
    [Fact]
    public void TreatsLoneWinAsModifier()
    {
        HotkeySpec spec = Parse("S+Win");

        Assert.Equal(Native.MOD_WIN, spec.Modifiers);
        Assert.Equal((uint)'S', spec.VirtualKey);
    }

    /// <summary>规范化后的写法再解析一次，必须还是同一个热键。</summary>
    [Theory]
    [InlineData("Win+S")]
    [InlineData("ctrl+alt+shift+f9")]
    [InlineData("Ctrl+-")]
    [InlineData("Ctrl+Num5")]
    public void CanonicalTextRoundTrips(string text)
    {
        HotkeySpec first = Parse(text);

        Assert.Equal(first, Parse(first.Text));
    }

    /// <summary>不该被接受的写法：空、缺主键、多主键、未知键名，以及会抢走正常输入的单键。</summary>
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

    /// <summary>系统保留与系统占用的组合必须拒绝，否则注册不上或抢掉系统功能。</summary>
    [Theory]
    [InlineData("F12")]
    [InlineData("Ctrl+F12")]
    [InlineData("Alt+Tab")]
    [InlineData("Ctrl+Alt+Delete")]
    [InlineData("Win+L")]
    public void RejectsSystemReservedCombo(string text) => ParseError(text);

    /// <summary>单键只放行功能键，避免 Ctrl+C 之外又把普通按键抢走。</summary>
    [Theory]
    [InlineData("F1", true)]
    [InlineData("F24", true)]
    [InlineData("M", false)]
    [InlineData("0", false)]
    public void BareKeyAllowsFunctionKeysOnly(string text, bool allowed)
    {
        bool parsed = HotkeySpec.TryParse(text, out _, out _);

        Assert.Equal(allowed, parsed);
    }

    /// <summary>被 Windows 自己占用的 Win 组合要认出来，注册时会改用键盘钩子并提示接管。</summary>
    [Theory]
    [InlineData("Win+S", true)]
    [InlineData("Win+D", true)]
    [InlineData("Win+E", true)]
    [InlineData("Win+X", true)]
    [InlineData("Win+Q", false)]
    [InlineData("Ctrl+S", false)]
    [InlineData("Win+Alt+S", false)]
    public void DetectsWindowsOwnedWinCombo(string text, bool expected)
    {
        HotkeySpec spec = Parse(text);

        Assert.Equal(expected, HotkeySpec.IsWindowsOwnedWinCombo(spec.Modifiers, spec.VirtualKey));
    }

    /// <summary>同一组合的等价性只按修饰键位与虚拟键码判定。</summary>
    [Fact]
    public void EqualityUsesModifiersAndVirtualKey()
    {
        Assert.Equal(Parse("ctrl+alt+m"), Parse("Ctrl+Alt+M"));
        Assert.NotEqual(Parse("Ctrl+Alt+M"), Parse("Ctrl+Shift+M"));
    }
}
