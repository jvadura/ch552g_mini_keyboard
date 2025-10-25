using System.IO;
using System.Text.Json;
using CH552G_PadConfig_Win.Models;

namespace CH552G_PadConfig_Win.Services;

/// <summary>
/// Manages saving and loading configuration profiles as JSON files
/// </summary>
public class ProfileManager
{
    private readonly DebugLogger? _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ProfileManager(DebugLogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Save configuration to JSON file
    /// </summary>
    public bool SaveProfile(string filePath, DeviceConfiguration config)
    {
        try
        {
            _logger?.Log($"Saving profile to {Path.GetFileName(filePath)}...");

            // Create profile wrapper with metadata
            var profile = new ProfileData
            {
                Name = config.ProfileName,
                Created = config.Created,
                Description = $"Configuration for CH552G keyboard with {config.Slots.Length} slots",
                LedBrightness = config.LedBrightness,
                ActiveSlot = config.ActiveSlot,
                Slots = config.Slots.Select(s => new SlotData
                {
                    Name = s.Name,
                    Actions = s.Actions.Select(a => new ActionData
                    {
                        Type = a.Type.ToString(),
                        Modifiers = GetModifiersList(a.Modifiers),
                        PrimaryValue = a.PrimaryValue,
                        SecondaryValue = a.SecondaryValue,
                        HoldEnabled = a.HoldEnabled,
                        ColorIdle = a.ColorIdle,
                        ColorActive = a.ColorActive,
                        Description = a.GetDescription()
                    }).ToArray()
                }).ToArray()
            };

            var json = JsonSerializer.Serialize(profile, JsonOptions);
            File.WriteAllText(filePath, json);

            _logger?.LogSuccess($"Profile saved: {Path.GetFileName(filePath)}");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to save profile: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Load configuration from JSON file
    /// </summary>
    public DeviceConfiguration? LoadProfile(string filePath)
    {
        try
        {
            _logger?.Log($"Loading profile from {Path.GetFileName(filePath)}...");

            if (!File.Exists(filePath))
            {
                _logger?.LogError($"File not found: {filePath}");
                return null;
            }

            var json = File.ReadAllText(filePath);
            var profile = JsonSerializer.Deserialize<ProfileData>(json, JsonOptions);

            if (profile == null)
            {
                _logger?.LogError("Failed to deserialize profile");
                return null;
            }

            // Convert to DeviceConfiguration
            var config = new DeviceConfiguration
            {
                ProfileName = profile.Name ?? "Unnamed Profile",
                Created = profile.Created,
                LedBrightness = profile.LedBrightness,
                ActiveSlot = profile.ActiveSlot
            };

            // Load slots
            for (int i = 0; i < Math.Min(profile.Slots.Length, 3); i++)
            {
                config.Slots[i].Name = profile.Slots[i].Name;

                for (int j = 0; j < Math.Min(profile.Slots[i].Actions.Length, 5); j++)
                {
                    var actionData = profile.Slots[i].Actions[j];
                    config.Slots[i].Actions[j] = new ActionConfig
                    {
                        Type = Enum.Parse<ActionConfig.ActionType>(actionData.Type),
                        Modifiers = ParseModifiers(actionData.Modifiers),
                        PrimaryValue = actionData.PrimaryValue,
                        SecondaryValue = actionData.SecondaryValue,
                        HoldEnabled = actionData.HoldEnabled,
                        ColorIdle = actionData.ColorIdle,
                        ColorActive = actionData.ColorActive
                    };
                }
            }

            _logger?.LogSuccess($"Profile loaded: {config.ProfileName}");
            _logger?.Log($"  Slots: {profile.Slots.Length}, Brightness: {config.LedBrightness}");

            return config;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to load profile: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Convert modifiers enum to string list for JSON
    /// </summary>
    private string[] GetModifiersList(ActionConfig.ModifierKeys modifiers)
    {
        var list = new List<string>();
        if (modifiers.HasFlag(ActionConfig.ModifierKeys.Ctrl)) list.Add("Ctrl");
        if (modifiers.HasFlag(ActionConfig.ModifierKeys.Shift)) list.Add("Shift");
        if (modifiers.HasFlag(ActionConfig.ModifierKeys.Alt)) list.Add("Alt");
        if (modifiers.HasFlag(ActionConfig.ModifierKeys.Gui)) list.Add("Gui");
        return list.ToArray();
    }

    /// <summary>
    /// Parse modifiers from string list
    /// </summary>
    private ActionConfig.ModifierKeys ParseModifiers(string[] modifiers)
    {
        var result = ActionConfig.ModifierKeys.None;
        foreach (var mod in modifiers)
        {
            result |= mod switch
            {
                "Ctrl" => ActionConfig.ModifierKeys.Ctrl,
                "Shift" => ActionConfig.ModifierKeys.Shift,
                "Alt" => ActionConfig.ModifierKeys.Alt,
                "Gui" => ActionConfig.ModifierKeys.Gui,
                _ => ActionConfig.ModifierKeys.None
            };
        }
        return result;
    }

    // JSON serialization classes
    private class ProfileData
    {
        public string Name { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public string Description { get; set; } = string.Empty;
        public byte LedBrightness { get; set; }
        public byte ActiveSlot { get; set; }
        public SlotData[] Slots { get; set; } = Array.Empty<SlotData>();
    }

    private class SlotData
    {
        public string Name { get; set; } = string.Empty;
        public ActionData[] Actions { get; set; } = Array.Empty<ActionData>();
    }

    private class ActionData
    {
        public string Type { get; set; } = string.Empty;
        public string[] Modifiers { get; set; } = Array.Empty<string>();
        public byte PrimaryValue { get; set; }
        public byte SecondaryValue { get; set; }
        public bool HoldEnabled { get; set; }
        public byte ColorIdle { get; set; }
        public byte ColorActive { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
