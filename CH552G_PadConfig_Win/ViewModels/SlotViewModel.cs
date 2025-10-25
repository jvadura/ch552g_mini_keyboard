using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CH552G_PadConfig_Win.Models;

namespace CH552G_PadConfig_Win.ViewModels;

/// <summary>
/// ViewModel for a single slot (5 actions)
/// </summary>
public class SlotViewModel : INotifyPropertyChanged
{
    private string _name;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int SlotIndex { get; }

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ActionViewModel> Actions { get; }

    public SlotViewModel(int slotIndex, SlotConfig slot)
    {
        SlotIndex = slotIndex;
        _name = slot.Name;

        Actions = new ObservableCollection<ActionViewModel>();
        for (int i = 0; i < slot.Actions.Length; i++)
        {
            Actions.Add(new ActionViewModel(i, slot.Actions[i]));
        }
    }

    /// <summary>
    /// Get the underlying SlotConfig model
    /// </summary>
    public SlotConfig GetModel()
    {
        return new SlotConfig
        {
            Name = Name,
            Actions = Actions.Select(a => a.Config).ToArray()
        };
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
