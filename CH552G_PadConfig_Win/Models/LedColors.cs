namespace CH552G_PadConfig_Win.Models;

/// <summary>
/// LED color palette definitions matching firmware led_colors.h
/// Fixed 8-color palette with pre-defined RGB values
/// </summary>
public static class LedColors
{
    // Palette indices (0-7)
    public const byte OFF = 0;
    public const byte RED = 1;
    public const byte GREEN = 2;
    public const byte BLUE = 3;
    public const byte YELLOW = 4;
    public const byte CYAN = 5;
    public const byte MAGENTA = 6;
    public const byte WHITE = 7;

    /// <summary>
    /// Color names for UI display
    /// </summary>
    public static readonly Dictionary<byte, string> Names = new()
    {
        { 0, "Off" },
        { 1, "Red" },
        { 2, "Green" },
        { 3, "Blue" },
        { 4, "Yellow" },
        { 5, "Cyan" },
        { 6, "Magenta" },
        { 7, "White" }
    };

    /// <summary>
    /// RGB values for UI preview (from firmware led_colors.h at ~40% base brightness)
    /// Values are R,G,B in range 0-100
    /// </summary>
    public static readonly Dictionary<byte, (byte r, byte g, byte b)> RgbValues = new()
    {
        { 0, (0, 0, 0) },         // Off
        { 1, (100, 0, 0) },       // Red
        { 2, (0, 100, 0) },       // Green
        { 3, (0, 0, 100) },       // Blue
        { 4, (100, 80, 0) },      // Yellow (slightly orange-tinted)
        { 5, (0, 100, 100) },     // Cyan
        { 6, (100, 0, 100) },     // Magenta
        { 7, (100, 100, 100) }    // White
    };

    /// <summary>
    /// Convert palette index to WPF Color for UI display
    /// </summary>
    public static System.Windows.Media.Color ToWpfColor(byte paletteIndex)
    {
        if (!RgbValues.ContainsKey(paletteIndex))
            paletteIndex = OFF;

        var (r, g, b) = RgbValues[paletteIndex];

        // Scale 0-100 to 0-255
        return System.Windows.Media.Color.FromRgb(
            (byte)(r * 255 / 100),
            (byte)(g * 255 / 100),
            (byte)(b * 255 / 100)
        );
    }

    /// <summary>
    /// Validate palette index is in range 0-7
    /// </summary>
    public static bool IsValid(byte paletteIndex)
    {
        return paletteIndex <= 7;
    }
}
