using System.ComponentModel;
using System.Runtime.CompilerServices;
using CH552G_PadConfig_Win.Models;

namespace CH552G_PadConfig_Win.ViewModels;

/// <summary>
/// ViewModel for a single action configuration
/// </summary>
public class ActionViewModel : INotifyPropertyChanged
{
    private ActionConfig _config;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ActionConfig Config
    {
        get => _config;
        set
        {
            _config = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(IdleColorBrush));
            OnPropertyChanged(nameof(ActiveColorBrush));
        }
    }

    public string InputName { get; }
    public int InputIndex { get; }

    public string Description => Config.GetDescription();

    public System.Windows.Media.SolidColorBrush IdleColorBrush =>
        new(LedColors.ToWpfColor(Config.ColorIdle));

    public System.Windows.Media.SolidColorBrush ActiveColorBrush =>
        new(LedColors.ToWpfColor(Config.ColorActive));

    public ActionViewModel(int inputIndex, ActionConfig config)
    {
        InputIndex = inputIndex;
        InputName = SlotConfig.GetInputName(inputIndex);
        _config = config;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
