using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CH552G_PadConfig_Win.Models;

namespace CH552G_PadConfig_Win.Views;

public partial class ActionEditorDialog : Window
{
    private ActionConfig _action;
    private byte _selectedIdleColor;
    private byte _selectedActiveColor;

    public ActionConfig Result { get; private set; }

    // Media keys (consumer codes)
    private static readonly Dictionary<string, ushort> MediaKeys = new()
    {
        { "Play", 0x00B0 },
        { "Pause", 0x00B1 },
        { "Play/Pause", 0x00CD },
        { "Next Track", 0x00B5 },
        { "Previous Track", 0x00B6 },
        { "Stop", 0x00B7 },
        { "Mute", 0x00E2 },
        { "Volume Up", 0x00E9 },
        { "Volume Down", 0x00EA },
        { "Media Player", 0x0183 },
        { "Calculator", 0x0192 },
        { "File Explorer", 0x0194 },
        { "Search", 0x0221 },
        { "Home", 0x0223 },
        { "Back", 0x0224 },
        { "Forward", 0x0225 },
        { "Refresh", 0x0227 }
    };

    public ActionEditorDialog(ActionConfig action, string inputName)
    {
        InitializeComponent();

        _action = action.Clone();
        _selectedIdleColor = action.ColorIdle;
        _selectedActiveColor = action.ColorActive;

        HeaderText.Text = $"Configure {inputName}";

        InitializeControls();
        LoadActionData();
    }

    private void InitializeControls()
    {
        // Action types
        ActionTypeCombo.Items.Add("None");
        ActionTypeCombo.Items.Add("Keyboard");
        ActionTypeCombo.Items.Add("Media");
        ActionTypeCombo.Items.Add("Mouse");
        ActionTypeCombo.Items.Add("Scroll");
    }

    private void LoadActionData()
    {
        ActionTypeCombo.SelectedIndex = (int)_action.Type;

        CtrlCheck.IsChecked = _action.Modifiers.HasFlag(ActionConfig.ModifierKeys.Ctrl);
        ShiftCheck.IsChecked = _action.Modifiers.HasFlag(ActionConfig.ModifierKeys.Shift);
        AltCheck.IsChecked = _action.Modifiers.HasFlag(ActionConfig.ModifierKeys.Alt);
        GuiCheck.IsChecked = _action.Modifiers.HasFlag(ActionConfig.ModifierKeys.Gui);

        HoldCheck.IsChecked = _action.HoldEnabled;

        UpdateColorPreviews();
        UpdatePrimaryControls();
        UpdatePreview();
    }

    private void ActionTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePrimaryControls();
        UpdatePreview();
    }

    private void UpdatePrimaryControls()
    {
        var selectedType = (ActionConfig.ActionType)ActionTypeCombo.SelectedIndex;

        // Hide all
        PrimaryCombo.Visibility = Visibility.Collapsed;
        PrimaryTextBox.Visibility = Visibility.Collapsed;
        SecondaryPanel.Visibility = Visibility.Collapsed;

        switch (selectedType)
        {
            case ActionConfig.ActionType.Keyboard:
                PrimaryLabel.Text = "Key (single character):";
                PrimaryTextBox.Visibility = Visibility.Visible;
                if (_action.PrimaryValue >= 32 && _action.PrimaryValue <= 126)
                    PrimaryTextBox.Text = ((char)_action.PrimaryValue).ToString();
                break;

            case ActionConfig.ActionType.Media:
                PrimaryLabel.Text = "Media Key:";
                PrimaryCombo.Visibility = Visibility.Visible;
                PrimaryCombo.Items.Clear();
                foreach (var key in MediaKeys.Keys)
                    PrimaryCombo.Items.Add(key);

                // Select current media key
                ushort currentCode = (ushort)((_action.SecondaryValue << 8) | _action.PrimaryValue);
                var match = MediaKeys.FirstOrDefault(k => k.Value == currentCode);
                PrimaryCombo.SelectedItem = match.Key ?? "Volume Up";
                break;

            case ActionConfig.ActionType.Mouse:
                PrimaryLabel.Text = "Mouse Button:";
                PrimaryCombo.Visibility = Visibility.Visible;
                PrimaryCombo.Items.Clear();
                PrimaryCombo.Items.Add("Left Click");
                PrimaryCombo.Items.Add("Right Click");
                PrimaryCombo.Items.Add("Middle Click");
                PrimaryCombo.SelectedIndex = _action.PrimaryValue == 1 ? 0 : _action.PrimaryValue == 2 ? 1 : 2;

                SecondaryPanel.Visibility = Visibility.Visible;
                SecondaryLabel.Text = "Click Count:";
                SecondaryTextBox.Text = Math.Max((byte)1, _action.SecondaryValue).ToString();
                break;

            case ActionConfig.ActionType.Scroll:
                PrimaryLabel.Text = "Direction:";
                PrimaryCombo.Visibility = Visibility.Visible;
                PrimaryCombo.Items.Clear();
                PrimaryCombo.Items.Add("Scroll Up");
                PrimaryCombo.Items.Add("Scroll Down");
                PrimaryCombo.SelectedIndex = _action.PrimaryValue == 1 ? 0 : 1;

                SecondaryPanel.Visibility = Visibility.Visible;
                SecondaryLabel.Text = "Scroll Amount:";
                SecondaryTextBox.Text = Math.Max((byte)1, _action.SecondaryValue).ToString();
                break;
        }
    }

    private void UpdateColorPreviews()
    {
        IdleColorPreview.Background = new SolidColorBrush(LedColors.ToWpfColor(_selectedIdleColor));
        ActiveColorPreview.Background = new SolidColorBrush(LedColors.ToWpfColor(_selectedActiveColor));
    }

    private void UpdatePreview()
    {
        var tempAction = BuildActionConfig();
        PreviewText.Text = tempAction.GetDescription();
    }

    private void SelectIdleColor_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerDialog(_selectedIdleColor);
        if (dialog.ShowDialog() == true)
        {
            _selectedIdleColor = dialog.SelectedColor;
            UpdateColorPreviews();
            UpdatePreview();
        }
    }

    private void SelectActiveColor_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ColorPickerDialog(_selectedActiveColor);
        if (dialog.ShowDialog() == true)
        {
            _selectedActiveColor = dialog.SelectedColor;
            UpdateColorPreviews();
            UpdatePreview();
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        Result = BuildActionConfig();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private ActionConfig BuildActionConfig()
    {
        var config = new ActionConfig
        {
            Type = (ActionConfig.ActionType)ActionTypeCombo.SelectedIndex,
            ColorIdle = _selectedIdleColor,
            ColorActive = _selectedActiveColor,
            HoldEnabled = HoldCheck.IsChecked == true
        };

        // Modifiers
        if (CtrlCheck.IsChecked == true) config.Modifiers |= ActionConfig.ModifierKeys.Ctrl;
        if (ShiftCheck.IsChecked == true) config.Modifiers |= ActionConfig.ModifierKeys.Shift;
        if (AltCheck.IsChecked == true) config.Modifiers |= ActionConfig.ModifierKeys.Alt;
        if (GuiCheck.IsChecked == true) config.Modifiers |= ActionConfig.ModifierKeys.Gui;

        // Type-specific values
        switch (config.Type)
        {
            case ActionConfig.ActionType.Keyboard:
                if (!string.IsNullOrEmpty(PrimaryTextBox.Text))
                    config.PrimaryValue = (byte)PrimaryTextBox.Text[0];
                break;

            case ActionConfig.ActionType.Media:
                if (PrimaryCombo.SelectedItem is string mediaKey && MediaKeys.TryGetValue(mediaKey, out ushort code))
                {
                    config.PrimaryValue = (byte)(code & 0xFF);
                    config.SecondaryValue = (byte)((code >> 8) & 0xFF);
                }
                break;

            case ActionConfig.ActionType.Mouse:
                config.PrimaryValue = PrimaryCombo.SelectedIndex switch
                {
                    0 => 0x01, // Left
                    1 => 0x02, // Right
                    2 => 0x04, // Middle
                    _ => 0x01
                };
                if (byte.TryParse(SecondaryTextBox.Text, out byte clicks))
                    config.SecondaryValue = clicks > 0 ? clicks : (byte)1;
                break;

            case ActionConfig.ActionType.Scroll:
                config.PrimaryValue = (byte)(PrimaryCombo.SelectedIndex == 0 ? 1 : 2);
                if (byte.TryParse(SecondaryTextBox.Text, out byte amount))
                    config.SecondaryValue = amount > 0 ? amount : (byte)1;
                break;
        }

        return config;
    }
}

// Extension method for cloning
internal static class ActionConfigExtensions
{
    public static ActionConfig Clone(this ActionConfig source)
    {
        return new ActionConfig
        {
            Type = source.Type,
            Modifiers = source.Modifiers,
            HoldEnabled = source.HoldEnabled,
            PrimaryValue = source.PrimaryValue,
            SecondaryValue = source.SecondaryValue,
            ColorIdle = source.ColorIdle,
            ColorActive = source.ColorActive
        };
    }
}
