using System.Runtime.InteropServices;
using System.Text;

namespace WindowMinimize;

/// <summary>
/// A parsed global hotkey: the modifier bits plus one virtual key code.
/// </summary>
internal sealed class HotkeySpec : IEquatable<HotkeySpec> {
    internal HotkeySpec(uint modifiers, uint virtualKey, string text) {
        Modifiers = modifiers;
        VirtualKey = virtualKey;
        Text = text;
    }

    /// <summary>MOD_* bit flags.</summary>
    internal uint Modifiers { get; }

    /// <summary>Virtual key code.</summary>
    internal uint VirtualKey { get; }

    /// <summary>Canonical readable form, for example Win+S.</summary>
    internal string Text { get; }

    public bool Equals(HotkeySpec? other) =>
        other is not null && other.Modifiers == Modifiers && other.VirtualKey == VirtualKey;

    public override bool Equals(object? obj) => Equals(obj as HotkeySpec);

    public override int GetHashCode() => HashCode.Combine(Modifiers, VirtualKey);

    public override string ToString() => Text;

    /// <summary>
    /// Modifier aliases. Single letters (C, A, S) must never be accepted here, otherwise the main
    /// key of Win+S or Ctrl+C would be read as a modifier.
    /// </summary>
    private static readonly Dictionary<string, uint> ModifierAliases = new(
        StringComparer.OrdinalIgnoreCase
    ) {
        ["CTRL"]    = Native.MOD_CONTROL,
        ["CONTROL"] = Native.MOD_CONTROL,
        ["ALT"]     = Native.MOD_ALT,
        ["SHIFT"]   = Native.MOD_SHIFT,
        ["WIN"]     = Native.MOD_WIN,
        ["WINDOWS"] = Native.MOD_WIN,
        ["SUPER"]   = Native.MOD_WIN,
        ["META"]    = Native.MOD_WIN,
    };

    private static readonly Dictionary<string, uint> KeyAliases = new(
        StringComparer.OrdinalIgnoreCase
    ) {
        ["ESC"]         = 0x1B,
        ["ESCAPE"]      = 0x1B,
        ["SPACE"]       = 0x20,
        ["SPACEBAR"]    = 0x20,
        ["TAB"]         = 0x09,
        ["ENTER"]       = 0x0D,
        ["RETURN"]      = 0x0D,
        ["BACKSPACE"]   = 0x08,
        ["BACK"]        = 0x08,
        ["DELETE"]      = 0x2E,
        ["DEL"]         = 0x2E,
        ["INSERT"]      = 0x2D,
        ["INS"]         = 0x2D,
        ["HOME"]        = 0x24,
        ["END"]         = 0x23,
        ["PAGEUP"]      = 0x21,
        ["PGUP"]        = 0x21,
        ["PAGEDOWN"]    = 0x22,
        ["PGDN"]        = 0x22,
        ["UP"]          = 0x26,
        ["DOWN"]        = 0x28,
        ["LEFT"]        = 0x25,
        ["RIGHT"]       = 0x27,
        ["PRINTSCREEN"] = 0x2C,
        ["SNAPSHOT"]    = 0x2C,
        ["PAUSE"]       = 0x13,
        ["CAPSLOCK"]    = 0x14,
        ["NUMLOCK"]     = 0x90,
        ["SCROLLLOCK"]  = 0x91,
        ["MINUS"]       = 0xBD,
        ["EQUAL"]       = 0xBB,
        ["PLUS"]        = 0xBB,
        ["COMMA"]       = 0xBC,
        ["PERIOD"]      = 0xBE,
        ["DOT"]         = 0xBE,
        ["SLASH"]       = 0xBF,
        ["BACKSLASH"]   = 0xDC,
        ["SEMICOLON"]   = 0xBA,
        ["QUOTE"]       = 0xDE,
        ["APOSTROPHE"]  = 0xDE,
        ["LBRACKET"]    = 0xDB,
        ["RBRACKET"]    = 0xDD,
        ["GRAVE"]       = 0xC0,
        ["BACKTICK"]    = 0xC0,
        ["TILDE"]       = 0xC0,
        ["MULTIPLY"]    = 0x6A,
        ["ADD"]         = 0x6B,
        ["SUBTRACT"]    = 0x6D,
        ["DECIMAL"]     = 0x6E,
        ["DIVIDE"]      = 0x6F,
    };

    /// <summary>
    /// Modifiers are printed in the order Windows itself uses: Win, Ctrl, Alt, Shift, so Win+Alt+M
    /// comes back as Win+Alt+M instead of Alt+Win+M.
    /// </summary>
    private static readonly (uint Bit, string Name)[] ModifierNames = [
        (Native.MOD_WIN, "Win"),
        (Native.MOD_CONTROL, "Ctrl"),
        (Native.MOD_ALT, "Alt"),
        (Native.MOD_SHIFT, "Shift"),
    ];

    /// <summary>
    /// Parse a hotkey text. Returns false and sets error when the text cannot be used.
    /// Supports Win+Alt+M, Ctrl+Shift+F12, Alt+Space, Ctrl+- and so on, with + or - as separator.
    /// </summary>
    internal static bool TryParse(string? text, out HotkeySpec? spec, out string error) {
        spec = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text)) {
            error = Strings.ErrorHotkeyEmpty;
            return false;
        }

        // a trailing separator means the last key is the minus or plus key itself, Ctrl+- or Ctrl++
        string trimmed = text.TrimEnd();
        bool trailingMinus      = trimmed.EndsWith('-');
        bool trailingPlus       = !trailingMinus && trimmed.EndsWith('+');
        bool trailingDoublePlus = trimmed.EndsWith("++", StringComparison.Ordinal);

        string[] parts = trimmed.Split(['+', '-'], StringSplitOptions.TrimEntries);
        var tokens = new List<string>();
        foreach (string part in parts) {
            if (part.Length > 0) tokens.Add(part);
        }

        if (trailingMinus) {
            tokens.Add("Minus");
        } else if (trailingPlus) {
            // in Ctrl++ only the trailing plus is the key, the plus before it is the separator
            tokens.Add(trailingDoublePlus ? "Plus" : "Minus");
        }

        if (tokens.Count == 0) {
            error = Strings.ErrorHotkeyEmpty;
            return false;
        }

        // every token is either a modifier or the single main key, the main key may be anywhere
        uint modifiers = 0;
        uint keyCode = 0;
        string? keyName = null;

        foreach (string token in tokens) {
            if (ModifierAliases.TryGetValue(token, out uint bit)) {
                modifiers |= bit;
                continue;
            }

            if (keyName is not null) {
                error = Strings.ErrorOnlyOneMainKey(keyName, token);
                return false;
            }

            if (!TryParseKey(token, out keyCode)) {
                error = Strings.ErrorUnknownKeyName(token);
                return false;
            }

            keyName = token;
        }

        if (keyName is null) {
            error = Strings.ErrorMainKeyMissing;
            return false;
        }

        uint vk = keyCode;

        // F12 is reserved by Windows, and a bare key of any kind would steal normal typing
        if (vk == 0x7B) {
            error = Strings.ErrorF12Reserved;
            return false;
        }

        if (modifiers == 0) {
            if (!IsSafeBareKey(vk)) {
                error = Strings.ErrorBareKeyOnlyFunction;
                return false;
            }
        } else if (IsReservedCombo(modifiers, vk)) {
            error = Strings.ErrorReservedCombo;
            return false;
        }

        spec = new HotkeySpec(modifiers, vk, Format(modifiers, vk));
        Log.Debug($"hotkey {text} parsed as {spec.Text}");
        return true;
    }

    private static bool TryParseKey(string token, out uint vk) {
        vk = 0;

        if (token.Length == 1) {
            char ch = char.ToUpperInvariant(token[0]);
            if (ch >= 'A' && ch <= 'Z') {
                vk = ch;
                return true;
            }

            if (ch >= '0' && ch <= '9') {
                vk = ch;
                return true;
            }
        }

        if (token.Length >= 2 && (token[0] == 'F' || token[0] == 'f')
            && int.TryParse(token.AsSpan(1), out int fn) && fn >= 1 && fn <= 24) {
            vk = (uint)(0x70 + fn - 1);
            return true;
        }

        if (token.StartsWith("NUM", StringComparison.OrdinalIgnoreCase) && token.Length == 4
            && token[3] >= '0' && token[3] <= '9') {
            vk = (uint)(0x60 + (token[3] - '0'));
            return true;
        }

        if (KeyAliases.TryGetValue(token, out uint alias)) {
            vk = alias;
            return true;
        }

        return false;
    }

    private static bool IsSafeBareKey(uint vk) => vk >= 0x70 && vk <= 0x87;

    private static bool IsReservedCombo(uint modifiers, uint vk) {
        // Alt+Tab
        if (modifiers == Native.MOD_ALT && vk == 0x09) return true;
        // Ctrl+Alt+Del
        if (modifiers == (Native.MOD_ALT | Native.MOD_CONTROL) && vk == 0x2E) return true;
        // Win+L and the Win+Alt combinations
        if (modifiers == Native.MOD_WIN && vk is 0x4C or 0x12) return true;

        return false;
    }

    /// <summary>
    /// The Win combinations Windows itself owns, for example Win+S to search or Win+D to show the
    /// desktop. RegisterHotKey always fails for them, so only the keyboard hook can take them over,
    /// and the system feature stops working once it does.
    /// </summary>
    internal static bool IsWindowsOwnedWinCombo(uint modifiers, uint vk) =>
        modifiers == Native.MOD_WIN
        && vk is 0x41 or 0x44 or 0x45 or 0x49 or 0x4B or 0x53 or 0x56 or 0x58;

    private static string Format(uint modifiers, uint vk) {
        var sb = new StringBuilder();
        foreach ((uint bit, string name) in ModifierNames) {
            if ((modifiers & bit) != 0) sb.Append(name).Append('+');
        }

        sb.Append(KeyName(vk));
        return sb.ToString();
    }

    private static string KeyName(uint vk) {
        switch (vk) {
            case >= 0x41 and <= 0x5A: return ((char)vk).ToString();
            case >= 0x30 and <= 0x39: return ((char)vk).ToString();
            case >= 0x60 and <= 0x69: return "Num" + (vk - 0x60);
            case >= 0x70 and <= 0x87: return "F" + (vk - 0x70 + 1);
            case 0x1B: return "Esc";
            case 0x20: return "Space";
            case 0x09: return "Tab";
            case 0x0D: return "Enter";
            case 0x08: return "Backspace";
            case 0x2E: return "Delete";
            case 0x2D: return "Insert";
            case 0x24: return "Home";
            case 0x23: return "End";
            case 0x21: return "PageUp";
            case 0x22: return "PageDown";
            case 0x26: return "Up";
            case 0x28: return "Down";
            case 0x25: return "Left";
            case 0x27: return "Right";
            case 0x2C: return "PrintScreen";
            case 0x13: return "Pause";
            case 0x14: return "CapsLock";
            case 0x90: return "NumLock";
            case 0x91: return "ScrollLock";
            case 0xBD: return "Minus";
            case 0xBB: return "Plus";
            case 0xBC: return "Comma";
            case 0xBE: return "Period";
            case 0xBF: return "Slash";
            case 0xDC: return "Backslash";
            case 0xBA: return "Semicolon";
            case 0xDE: return "Quote";
            case 0xDB: return "Lbracket";
            case 0xDD: return "Rbracket";
            case 0xC0: return "Grave";
            case 0x6A: return "Multiply";
            case 0x6B: return "Add";
            case 0x6D: return "Subtract";
            case 0x6E: return "Decimal";
            case 0x6F: return "Divide";
            default: return "VK" + vk.ToString("X2");
        }
    }
}
