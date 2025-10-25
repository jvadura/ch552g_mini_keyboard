using System.Windows;
using System.Windows.Controls;
using CH552G_PadConfig_Win.Models;

namespace CH552G_PadConfig_Win.Views;

public partial class ColorPickerDialog : Window
{
    public byte SelectedColor { get; private set; }

    public ColorPickerDialog(byte initialColor)
    {
        InitializeComponent();

        SelectedColor = initialColor;
        UpdateSelectedText();
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tagStr)
        {
            SelectedColor = byte.Parse(tagStr);
            UpdateSelectedText();
        }
    }

    private void UpdateSelectedText()
    {
        var colorName = LedColors.Names.TryGetValue(SelectedColor, out var name) ? name : "Unknown";
        SelectedColorText.Text = $"Selected: {colorName} (index {SelectedColor})";
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
