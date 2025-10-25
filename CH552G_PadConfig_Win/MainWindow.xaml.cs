using System.Windows;
using System.Windows.Controls;
using CH552G_PadConfig_Win.ViewModels;
using CH552G_PadConfig_Win.Services;
using CH552G_PadConfig_Win.Views;

namespace CH552G_PadConfig_Win
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly DebugLogger _logger;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize logger
            _logger = new DebugLogger(StatusLogTextBox);

            // Initialize services
            var hidCommunicator = new HidCommunicator(_logger);
            var profileManager = new ProfileManager(_logger);
            var settings = AppSettings.Load();

            // Initialize ViewModel
            _viewModel = new MainViewModel(hidCommunicator, profileManager, settings);
            DataContext = _viewModel;

            // Window size from settings
            if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
            {
                Width = settings.WindowWidth;
                Height = settings.WindowHeight;
            }

            // Log startup
            _logger.Log("=".PadRight(50, '='));
            _logger.Log("CH552G Keyboard Configurator v1.0");
            _logger.Log("=".PadRight(50, '='));

            // Initialize device connection
            _viewModel.Initialize();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Save window size
            var settings = AppSettings.Load();
            settings.WindowWidth = (int)Width;
            settings.WindowHeight = (int)Height;
            settings.Save();

            base.OnClosed(e);
        }

        private void EditAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ActionViewModel actionViewModel)
            {
                var dialog = new ActionEditorDialog(actionViewModel.Config, actionViewModel.InputName)
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    actionViewModel.Config = dialog.Result;
                    _logger.Log($"Updated {actionViewModel.InputName}: {actionViewModel.Description}");
                }
            }
        }
    }
}