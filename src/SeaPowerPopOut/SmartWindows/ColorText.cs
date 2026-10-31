using System.Globalization;

namespace SeaPowerPopOut.SmartWindows;

/// <summary>Colour as written in the config: <c>#RRGGBB</c> or <c>#AARRGGBB</c> (AA = opacity, 00 transparent).</summary>
internal static class ColorText
{
    public static bool TryParse(string text, out byte a, out byte r, out byte g, out byte b)
    {
        a = 255;
        r = g = b = 0;
        if (string.IsNullOrEmpty(text))
            return false;
        string hex = text.Trim().TrimStart('#');
        if ((hex.Length != 6 && hex.Length != 8)
            || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
            return false;

        if (hex.Length == 8)
            a = (byte)(value >> 24);
        r = (byte)(value >> 16);
        g = (byte)(value >> 8);
        b = (byte)value;
        return true;
    }

    public static string Format(byte a, byte r, byte g, byte b) => $"#{a:X2}{r:X2}{g:X2}{b:X2}";
}
