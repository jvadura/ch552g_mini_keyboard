using System.Text.Json.Serialization;

namespace CH552G_PadConfig_Win.Models;

/// <summary>
/// Complete device configuration (3 slots + metadata)
/// Matches firmware Configuration structure (128 bytes in DataFlash)
/// </summary>
public class DeviceConfiguration
{
    /// <summary>
    /// 3 configuration slots
    /// </summary>
    public SlotConfig[] Slots { get; set; }

    /// <summary>
    /// Currently active slot (0-2)
    /// </summary>
    public byte ActiveSlot { get; set; } = 0;

    /// <summary>
    /// Global LED brightness (0-255, default 100 = ~39%)
    /// </summary>
    public byte LedBrightness { get; set; } = 100;

    /// <summary>
    /// Profile metadata (not sent to device)
    /// </summary>
    [JsonIgnore]
    public string ProfileName { get; set; } = "Default Profile";

    [JsonIgnore]
    public DateTime Created { get; set; } = DateTime.Now;

    public DeviceConfiguration()
    {
        Slots = new SlotConfig[3];
        for (int i = 0; i < 3; i++)
        {
            Slots[i] = new SlotConfig { Name = $"Slot {i}" };
        }
    }

    /// <summary>
    /// Get all 15 actions in linear order for USB transfer
    /// Order: Slot0[0-4], Slot1[0-4], Slot2[0-4]
    /// </summary>
    public ActionConfig[] GetAllActions()
    {
        var actions = new ActionConfig[15];
        int index = 0;

        for (int slot = 0; slot < 3; slot++)
        {
            for (int input = 0; input < 5; input++)
            {
                actions[index++] = Slots[slot].Actions[input];
            }
        }

        return actions;
    }

    /// <summary>
    /// Set all actions from linear array
    /// </summary>
    public void SetAllActions(ActionConfig[] actions)
    {
        if (actions.Length != 15)
            throw new ArgumentException("Must provide exactly 15 actions");

        int index = 0;
        for (int slot = 0; slot < 3; slot++)
        {
            for (int input = 0; input < 5; input++)
            {
                Slots[slot].Actions[input] = actions[index++];
            }
        }
    }

    /// <summary>
    /// Create default configuration matching firmware defaults
    /// </summary>
    public static DeviceConfiguration CreateDefault()
    {
        var config = new DeviceConfiguration
        {
            ActiveSlot = 0,
            LedBrightness = 100,
            ProfileName = "Default Configuration"
        };

        // Slot 0: Microsoft Teams (Blue LEDs)
        config.Slots[0].Name = "MS Teams";
        config.Slots[0].Actions[SlotConfig.INPUT_BTN_1] = new ActionConfig
        {
            Type = ActionConfig.ActionType.Keyboard,
            Modifiers = ActionConfig.ModifierKeys.Ctrl,
            PrimaryValue = (byte)' ', // Ctrl+Space (PTT)
            HoldEnabled = true,
            ColorIdle = LedColors.BLUE,
            ColorActive = LedColors.BLUE
        };
        config.Slots[0].Actions[SlotConfig.INPUT_BTN_2] = new ActionConfig
        {
            Type = ActionConfig.ActionType.Keyboard,
            Modifiers = ActionConfig.ModifierKeys.Ctrl | ActionConfig.ModifierKeys.Shift,
            PrimaryValue = (byte)'m', // Ctrl+Shift+M (Mic Toggle)
            ColorIdle = LedColors.BLUE,
            ColorActive = LedColors.BLUE
        };
        config.Slots[0].Actions[SlotConfig.INPUT_BTN_3] = new ActionConfig
        {
            Type = ActionConfig.ActionType.Keyboard,
            Modifiers = ActionConfig.ModifierKeys.Ctrl | ActionConfig.ModifierKeys.Shift,
            PrimaryValue = (byte)'o', // Ctrl+Shift+O (Camera Toggle)
            ColorIdle = LedColors.BLUE,
            ColorActive = LedColors.BLUE
        };
        config.Slots[0].Actions[SlotConfig.INPUT_ENC_CW] = new ActionConfig
        {
            Type = ActionConfig.ActionType.Media,
            PrimaryValue = 0xE9, SecondaryValue = 0x00, // Volume Up
            ColorIdle = LedColors.OFF,
            ColorActive = LedColors.OFF
        };
        config.Slots[0].Actions[SlotConfig.INPUT_ENC_CCW] = new ActionConfig
        {
            Type = ActionConfig.ActionType.Media,
            PrimaryValue = 0xEA, SecondaryValue = 0x00, // Volume Down
            ColorIdle = LedColors.OFF,
            ColorActive = LedColors.OFF
        };

        // Slot 1: Media Control (Red LEDs)
        config.Slots[1].Name = "Media";
        config.Slots[1].Actions[SlotConfig.INPUT_BTN_1] = new ActionConfig
        {
            Type = ActionConfig.ActionType.Keyboard,
            PrimaryValue = (byte)'l', // 'L' key (YouTube seek forward)
            ColorIdle = LedColors.RED,
            ColorActive = LedColors.RED
        };
        config.Slots[1].Actions[SlotConfig.INPUT_BTN_2] = new ActionConfig
        {
            Type = ActionConfig.ActionType.Keyboard,
            PrimaryValue = (byte)'j', // 'J' key (YouTube seek backward)
            ColorIdle = LedColors.RED,
            ColorActive = LedColors.RED
        };
        config.Slots[1].Actions[SlotConfig.INPUT_BTN_3] = new ActionConfig
        {
            Type = ActionConfig.ActionType.Media,
            PrimaryValue = 0xCD, SecondaryValue = 0x00, // Play/Pause
            ColorIdle = LedColors.RED,
            ColorActive = LedColors.RED
        };
        config.Slots[1].Actions[SlotConfig.INPUT_ENC_CW] = new ActionConfig
        {
            Type = ActionConfig.ActionType.Media,
            PrimaryValue = 0xE9, SecondaryValue = 0x00, // Volume Up (copied from Slot 0)
            ColorIdle = LedColors.OFF,
            ColorActive = LedColors.OFF
        };
        config.Slots[1].Actions[SlotConfig.INPUT_ENC_CCW] = new ActionConfig
        {
            Type = ActionConfig.ActionType.Media,
            PrimaryValue = 0xEA, SecondaryValue = 0x00, // Volume Down (copied from Slot 0)
            ColorIdle = LedColors.OFF,
            ColorActive = LedColors.OFF
        };

        // Slot 2: Visual Studio 2022 (Green LEDs)
        config.Slots[2].Name = "VS 2022";
        config.Slots[2].Actions[SlotConfig.INPUT_BTN_1] = new ActionConfig
        {
            Type = ActionConfig.ActionType.Keyboard,
            Modifiers = ActionConfig.ModifierKeys.Ctrl | ActionConfig.ModifierKeys.Shift,
            PrimaryValue = (byte)'f', // Ctrl+Shift+F (Find in Files)
            ColorIdle = LedColors.GREEN,
            ColorActive = LedColors.GREEN
        };
        config.Slots[2].Actions[SlotConfig.INPUT_BTN_2] = new ActionConfig
        {
            Type = ActionConfig.ActionType.Keyboard,
            Modifiers = ActionConfig.ModifierKeys.Ctrl,
            PrimaryValue = (byte)'m', // Ctrl+M (Collapse/Expand)
            ColorIdle = LedColors.GREEN,
            ColorActive = LedColors.GREEN
        };
        config.Slots[2].Actions[SlotConfig.INPUT_BTN_3] = new ActionConfig
        {
            Type = ActionConfig.ActionType.Keyboard,
            Modifiers = ActionConfig.ModifierKeys.Ctrl | ActionConfig.ModifierKeys.Shift,
            PrimaryValue = (byte)'b', // Ctrl+Shift+B (Build Solution)
            ColorIdle = LedColors.GREEN,
            ColorActive = LedColors.GREEN
        };
        config.Slots[2].Actions[SlotConfig.INPUT_ENC_CW] = new ActionConfig
        {
            Type = ActionConfig.ActionType.Media,
            PrimaryValue = 0xE9, SecondaryValue = 0x00, // Volume Up (copied from Slot 0)
            ColorIdle = LedColors.OFF,
            ColorActive = LedColors.OFF
        };
        config.Slots[2].Actions[SlotConfig.INPUT_ENC_CCW] = new ActionConfig
        {
            Type = ActionConfig.ActionType.Media,
            PrimaryValue = 0xEA, SecondaryValue = 0x00, // Volume Down (copied from Slot 0)
            ColorIdle = LedColors.OFF,
            ColorActive = LedColors.OFF
        };

        return config;
    }
}
