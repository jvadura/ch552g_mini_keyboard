using System.Text.Json.Serialization;

namespace CH552G_PadConfig_Win.Models;

/// <summary>
/// Represents a single action (8 bytes) matching firmware Action structure
/// Maps to ch552g_keyboard_v2.ino Action typedef
/// </summary>
public class ActionConfig
{
    // Action types (bits 0-2 of control byte)
    public enum ActionType : byte
    {
        None = 0x0,
        Keyboard = 0x1,
        Media = 0x2,
        Mouse = 0x3,
        Scroll = 0x4
    }

    // Modifiers (bits 4-7 of control byte)
    [Flags]
    public enum ModifierKeys : byte
    {
        None = 0x00,
        Ctrl = 0x10,
        Shift = 0x40,
        Alt = 0x20,
        Gui = 0x80  // Windows/Command key
    }

    public ActionType Type { get; set; } = ActionType.None;
    public ModifierKeys Modifiers { get; set; } = ModifierKeys.None;
    public bool HoldEnabled { get; set; } = false;

    /// <summary>
    /// Primary value: ASCII char for Keyboard, button mask for Mouse, direction for Scroll,
    /// low byte of consumer code for Media
    /// </summary>
    public byte PrimaryValue { get; set; } = 0;

    /// <summary>
    /// Secondary value: High byte of consumer code for Media, click count for Mouse,
    /// scroll amount for Scroll
    /// </summary>
    public byte SecondaryValue { get; set; } = 0;

    /// <summary>
    /// LED color when idle (palette index 0-7)
    /// </summary>
    public byte ColorIdle { get; set; } = LedColors.OFF;

    /// <summary>
    /// LED color when active (palette index 0-7)
    /// </summary>
    public byte ColorActive { get; set; } = LedColors.OFF;

    /// <summary>
    /// Serialize action to 8-byte firmware format
    /// Byte 0: control (modifiers|hold|type)
    /// Byte 1: primary value
    /// Byte 2: secondary value
    /// Byte 3: color_idle
    /// Byte 4: color_active
    /// Bytes 5-7: reserved (0x00)
    /// </summary>
    public byte[] ToBytes()
    {
        var result = new byte[8];

        // Control byte: bits 7-4=modifiers, bit 3=hold, bits 2-0=type
        result[0] = (byte)(
            ((byte)Modifiers) |
            (HoldEnabled ? 0x08 : 0x00) |
            ((byte)Type & 0x07)
        );

        result[1] = PrimaryValue;
        result[2] = SecondaryValue;
        result[3] = ColorIdle;
        result[4] = ColorActive;
        // Bytes 5-7 remain 0x00 (reserved)

        return result;
    }

    /// <summary>
    /// Deserialize action from 8-byte firmware format
    /// </summary>
    public static ActionConfig FromBytes(byte[] data)
    {
        if (data.Length < 8)
            throw new ArgumentException("Action data must be 8 bytes");

        return new ActionConfig
        {
            Type = (ActionType)(data[0] & 0x07),
            Modifiers = (ModifierKeys)(data[0] & 0xF0),
            HoldEnabled = (data[0] & 0x08) != 0,
            PrimaryValue = data[1],
            SecondaryValue = data[2],
            ColorIdle = data[3],
            ColorActive = data[4]
        };
    }

    /// <summary>
    /// Get human-readable description of this action
    /// </summary>
    public string GetDescription()
    {
        if (Type == ActionType.None)
            return "None";

        var modStr = GetModifierString();
        var baseStr = Type switch
        {
            ActionType.Keyboard => GetKeyboardDescription(),
            ActionType.Media => GetMediaDescription(),
            ActionType.Mouse => GetMouseDescription(),
            ActionType.Scroll => GetScrollDescription(),
            _ => "Unknown"
        };

        var holdStr = HoldEnabled ? " (Hold)" : "";

        return string.IsNullOrEmpty(modStr)
            ? $"{baseStr}{holdStr}"
            : $"{modStr}+{baseStr}{holdStr}";
    }

    private string GetModifierString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Gui)) parts.Add("Win");
        return string.Join("+", parts);
    }

    private string GetKeyboardDescription()
    {
        // ASCII printable characters
        if (PrimaryValue >= 32 && PrimaryValue <= 126)
            return $"'{(char)PrimaryValue}'";

        // Special keys
        return PrimaryValue switch
        {
            0x0A => "Enter",
            0x08 => "Backspace",
            0x09 => "Tab",
            0x1B => "Escape",
            0x20 => "Space",
            _ => $"Key(0x{PrimaryValue:X2})"
        };
    }

    private string GetMediaDescription()
    {
        // Reconstruct 16-bit consumer code from primary (low) and secondary (high)
        ushort consumerCode = (ushort)((SecondaryValue << 8) | PrimaryValue);

        return consumerCode switch
        {
            0x00B0 => "Play",
            0x00B1 => "Pause",
            0x00B5 => "Next Track",
            0x00B6 => "Previous Track",
            0x00B7 => "Stop",
            0x00CD => "Play/Pause",
            0x00E2 => "Mute",
            0x00E9 => "Volume Up",
            0x00EA => "Volume Down",
            0x0183 => "Media Player",
            0x0192 => "Calculator",
            0x0194 => "File Explorer",
            0x0221 => "Search",
            0x0223 => "Home",
            0x0224 => "Back",
            0x0225 => "Forward",
            0x0226 => "Stop",
            0x0227 => "Refresh",
            _ => $"Media(0x{consumerCode:X4})"
        };
    }

    private string GetMouseDescription()
    {
        var button = PrimaryValue switch
        {
            0x01 => "Left Click",
            0x02 => "Right Click",
            0x04 => "Middle Click",
            _ => "Unknown Button"
        };

        var count = SecondaryValue > 1 ? $" x{SecondaryValue}" : "";
        return $"{button}{count}";
    }

    private string GetScrollDescription()
    {
        var direction = PrimaryValue switch
        {
            1 => "Scroll Up",
            2 => "Scroll Down",
            _ => "Scroll"
        };

        return SecondaryValue > 1 ? $"{direction} ({SecondaryValue})" : direction;
    }
}
