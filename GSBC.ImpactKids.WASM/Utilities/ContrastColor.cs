namespace GSBC.ImpactKids.WASM.Utilities;

using System;
using System.Globalization;

public static class ContrastColor
{
    /// <summary>
    /// Returns a high-contrast text color ("#000000" or "#FFFFFF") for a given background hex.
    /// Accepts #RGB, #RRGGBB, or #AARRGGBB. Alpha (if present) is ignored.
    /// </summary>
    public static string GetAccessibleTextHex(string backgroundHex)
    {
        Console.WriteLine(backgroundHex);
        (byte r, byte g, byte b) = ParseHex(backgroundHex);

        // Relative luminance per WCAG 2.1
        double luminance = RelativeLuminance(r, g, b);

        // Contrast ratios against black (#000000) and white (#FFFFFF)
        double contrastWithBlack = ContrastRatio(luminance, 0.0); // L_text_black = 0
        double contrastWithWhite = ContrastRatio(1.0, luminance); // L_text_white = 1

        // Pick the higher contrast
        return (contrastWithBlack >= contrastWithWhite) ? "#000000" : "#FFFFFF";
    }

    /// <summary>
    /// Returns the chosen text color as System.Drawing.Color (if you prefer not to work with hex).
    /// </summary>
    public static (byte R, byte G, byte B) GetAccessibleTextRgb(string backgroundHex)
    {
        string hex = GetAccessibleTextHex(backgroundHex);
        return hex == "#000000" ? ((byte)0, (byte)0, (byte)0) : ((byte)255, (byte)255, (byte)255);
    }

    // ---- Helpers ----

    // Parses #RGB, #RRGGBB, #AARRGGBB. Ignores alpha.
    private static (byte R, byte G, byte B) ParseHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            throw new ArgumentException("Hex color must be non-empty.", nameof(hex));

        hex = hex.Trim();
        if (hex[0] == '#') hex = hex[1..];

        if (hex.Length == 3) // #RGB -> #RRGGBB
        {
            string rr = new string(hex[0], 2);
            string gg = new string(hex[1], 2);
            string bb = new string(hex[2], 2);
            return (byte.Parse(rr, NumberStyles.HexNumber),
                byte.Parse(gg, NumberStyles.HexNumber),
                byte.Parse(bb, NumberStyles.HexNumber));
        }
        else if (hex.Length == 6) // #RRGGBB
        {
            return (byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber),
                byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber),
                byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber));
        }
        else if (hex.Length == 8) // #AARRGGBB -> ignore AA
        {
            return (byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber),
                byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber),
                byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber));
        }

        throw new ArgumentException("Hex must be #RGB, #RRGGBB, or #AARRGGBB.", nameof(hex));
    }

    // WCAG 2.1 relative luminance calculation (sRGB to linear)
    private static double RelativeLuminance(byte r, byte g, byte b)
    {
        double rs = r / 255.0;
        double gs = g / 255.0;
        double bs = b / 255.0;

        double linearR = SrgbToLinear(rs);
        double linearG = SrgbToLinear(gs);
        double linearB = SrgbToLinear(bs);

        // L = 0.2126*R + 0.7152*G + 0.0722*B
        return 0.2126 * linearR + 0.7152 * linearG + 0.0722 * linearB;
    }

    private static double SrgbToLinear(double c)
    {
        return (c <= 0.03928) ? (c / 12.92) : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    // Contrast ratio: (L1 + 0.05) / (L2 + 0.05), where L1 >= L2
    private static double ContrastRatio(double l1, double l2)
    {
        if (l1 < l2) (l1, l2) = (l2, l1);
        return (l1 + 0.05) / (l2 + 0.05);
    }
}