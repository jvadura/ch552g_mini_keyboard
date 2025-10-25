using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using CH552G_PadConfig_Win.Models;
using CH552G_PadConfig_Win.Services;
using Microsoft.Win32;

namespace CH552G_PadConfig_Win.ViewModels;

/// <summary>
/// Main window ViewModel - coordinates all application logic
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly HidCommunicator _hidCommunicator;
    private readonly ProfileManager _profileManager;
    private readonly AppSettings _settings;
    private int _selectedSlotIndex;
    private byte _ledBrightness;
    private bool _isConnected;
    private string _statusMessage;
    private DeviceInfo? _deviceInfo;

    public event PropertyChangedEventHandler? PropertyChanged;

    // Collections
    public ObservableCollection<SlotViewModel> Slots { get; }

    // Properties
    public int SelectedSlotIndex
    {
        get => _selectedSlotIndex;
        set
        {
            _selectedSlotIndex = value;
            OnPropertyChanged();
        }
    }

    public byte LedBrightness
    {
        get => _ledBrightness;
        set
        {
            _ledBrightness = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LedBrightnessPercent));
        }
    }

    public int LedBrightnessPercent => (int)((LedBrightness / 255.0) * 100);

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            _isConnected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public string StatusText => IsConnected
        ? $"✓ Connected - {_deviceInfo?.FirmwareVersion ?? "Unknown"}"
        : "✗ Not Connected";

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    // Commands
    public RelayCommand RefreshDeviceCommand { get; }
    public RelayCommand ApplyToDeviceCommand { get; }
    public RelayCommand SetActiveSlotCommand { get; }
    public RelayCommand LoadProfileCommand { get; }
    public RelayCommand SaveProfileCommand { get; }
    public RelayCommand SaveAsProfileCommand { get; }
    public RelayCommand ResetToDefaultsCommand { get; }

    public MainViewModel(HidCommunicator hidCommunicator, ProfileManager profileManager, AppSettings settings)
    {
        _hidCommunicator = hidCommunicator;
        _profileManager = profileManager;
        _settings = settings;
        _statusMessage = "Ready";
        _ledBrightness = 100;

        // Initialize slots
        Slots = new ObservableCollection<SlotViewModel>();
        var defaultConfig = DeviceConfiguration.CreateDefault();
        for (int i = 0; i < defaultConfig.Slots.Length; i++)
        {
            Slots.Add(new SlotViewModel(i, defaultConfig.Slots[i]));
        }

        LedBrightness = defaultConfig.LedBrightness;

        // Commands
        RefreshDeviceCommand = new RelayCommand(RefreshDevice);
        ApplyToDeviceCommand = new RelayCommand(ApplyToDevice, () => IsConnected);
        SetActiveSlotCommand = new RelayCommand(SetActiveSlot, () => IsConnected);
        LoadProfileCommand = new RelayCommand(LoadProfile);
        SaveProfileCommand = new RelayCommand(SaveProfile);
        SaveAsProfileCommand = new RelayCommand(SaveAsProfile);
        ResetToDefaultsCommand = new RelayCommand(ResetToDefaults);
    }

    /// <summary>
    /// Initialize: search for device and auto-load last profile
    /// </summary>
    public void Initialize()
    {
        RefreshDevice();

        // Auto-load last profile if configured
        if (_settings.AutoLoadLastProfile && !string.IsNullOrEmpty(_settings.LastProfilePath))
        {
            if (System.IO.File.Exists(_settings.LastProfilePath))
            {
                LoadProfileFromPath(_settings.LastProfilePath);
            }
        }
    }

    private void RefreshDevice()
    {
        IsConnected = _hidCommunicator.FindDevice();

        if (IsConnected)
        {
            _deviceInfo = _hidCommunicator.GetDeviceInfo();
        }

        RefreshDeviceCommand.RaiseCanExecuteChanged();
        ApplyToDeviceCommand.RaiseCanExecuteChanged();
        SetActiveSlotCommand.RaiseCanExecuteChanged();
    }

    private async void ApplyToDevice()
    {
        if (!IsConnected)
            return;

        var result = MessageBox.Show(
            "This will write the current configuration to the device.\nContinue?",
            "Apply Configuration",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (result != MessageBoxResult.Yes)
            return;

        StatusMessage = "Writing configuration...";

        var config = GetCurrentConfiguration();

        await Task.Run(() =>
        {
            if (_hidCommunicator.WriteAllConfig(config))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = "Configuration applied successfully";
                    MessageBox.Show(
                        "Configuration written to device successfully!",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = "Failed to apply configuration";
                    MessageBox.Show(
                        "Failed to write configuration to device. Check the status log for details.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                });
            }
        });
    }

    private async void SetActiveSlot()
    {
        if (!IsConnected)
            return;

        byte slotIndex = (byte)SelectedSlotIndex;

        StatusMessage = $"Setting active slot to {slotIndex}...";

        await Task.Run(() =>
        {
            if (_hidCommunicator.SetActiveSlot(slotIndex))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Active slot set to {slotIndex}";
                });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = "Failed to set active slot";
                });
            }
        });
    }

    private void LoadProfile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load Configuration Profile",
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadProfileFromPath(dialog.FileName);
        }
    }

    private void LoadProfileFromPath(string filePath)
    {
        var config = _profileManager.LoadProfile(filePath);

        if (config != null)
        {
            // Update ViewModels
            Slots.Clear();
            for (int i = 0; i < config.Slots.Length; i++)
            {
                Slots.Add(new SlotViewModel(i, config.Slots[i]));
            }

            LedBrightness = config.LedBrightness;
            SelectedSlotIndex = config.ActiveSlot;

            _settings.LastProfilePath = filePath;
            _settings.Save();

            StatusMessage = $"Loaded profile: {System.IO.Path.GetFileName(filePath)}";
        }
    }

    private void SaveProfile()
    {
        if (!string.IsNullOrEmpty(_settings.LastProfilePath))
        {
            SaveProfileToPath(_settings.LastProfilePath);
        }
        else
        {
            SaveAsProfile();
        }
    }

    private void SaveAsProfile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Configuration Profile",
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            DefaultExt = ".json",
            FileName = "ch552g_config.json"
        };

        if (dialog.ShowDialog() == true)
        {
            SaveProfileToPath(dialog.FileName);
        }
    }

    private void SaveProfileToPath(string filePath)
    {
        var config = GetCurrentConfiguration();
        config.ProfileName = System.IO.Path.GetFileNameWithoutExtension(filePath);

        if (_profileManager.SaveProfile(filePath, config))
        {
            _settings.LastProfilePath = filePath;
            _settings.Save();

            StatusMessage = $"Saved profile: {System.IO.Path.GetFileName(filePath)}";
        }
    }

    private void ResetToDefaults()
    {
        var result = MessageBox.Show(
            "Reset all configuration to factory defaults?",
            "Reset to Defaults",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        if (result != MessageBoxResult.Yes)
            return;

        var defaultConfig = DeviceConfiguration.CreateDefault();

        Slots.Clear();
        for (int i = 0; i < defaultConfig.Slots.Length; i++)
        {
            Slots.Add(new SlotViewModel(i, defaultConfig.Slots[i]));
        }

        LedBrightness = defaultConfig.LedBrightness;
        SelectedSlotIndex = 0;

        StatusMessage = "Configuration reset to defaults";
    }

    private DeviceConfiguration GetCurrentConfiguration()
    {
        var config = new DeviceConfiguration
        {
            ActiveSlot = (byte)SelectedSlotIndex,
            LedBrightness = LedBrightness
        };

        for (int i = 0; i < Slots.Count; i++)
        {
            config.Slots[i] = Slots[i].GetModel();
        }

        return config;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
