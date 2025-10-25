namespace CH552G_PadConfig_Win.Models;

/// <summary>
/// Configuration for a single slot (5 actions)
/// Each slot corresponds to a different usage mode (e.g., Teams, Media, VS Code)
/// </summary>
public class SlotConfig
{
    /// <summary>
    /// User-friendly name for this slot
    /// </summary>
    public string Name { get; set; } = "Unnamed Slot";

    /// <summary>
    /// 5 actions: BTN_1, BTN_2, BTN_3, ENC_CW, ENC_CCW
    /// </summary>
    public ActionConfig[] Actions { get; set; }

    // Input indices matching firmware
    public const int INPUT_BTN_1 = 0;
    public const int INPUT_BTN_2 = 1;
    public const int INPUT_BTN_3 = 2;
    public const int INPUT_ENC_CW = 3;
    public const int INPUT_ENC_CCW = 4;

    public SlotConfig()
    {
        Actions = new ActionConfig[5];
        for (int i = 0; i < 5; i++)
        {
            Actions[i] = new ActionConfig();
        }
    }

    /// <summary>
    /// Get input name for display
    /// </summary>
    public static string GetInputName(int index)
    {
        return index switch
        {
            INPUT_BTN_1 => "BTN 1",
            INPUT_BTN_2 => "BTN 2",
            INPUT_BTN_3 => "BTN 3",
            INPUT_ENC_CW => "ENC CW",
            INPUT_ENC_CCW => "ENC CCW",
            _ => $"Input {index}"
        };
    }
}
