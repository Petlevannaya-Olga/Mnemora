using System.Windows;
using Mnemora.Desktop.ViewModels.Startup;

namespace Mnemora.Desktop.Views.Startup;

public partial class StartupWindow : Window
{
    private readonly StartupViewModel _viewModel;
    private bool _started;

    public StartupWindow(StartupViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += StartupWindow_OnLoaded;
        _viewModel.StartupSucceeded += StartupViewModel_OnStartupSucceeded;
        _viewModel.OnboardingRequested += StartupViewModel_OnOnboardingRequested;
        _viewModel.CloseRequested += StartupViewModel_OnCloseRequested;
    }

    public StartupViewModel ViewModel => _viewModel;

    public bool OpenOnboardingRequested { get; private set; }

    private async void StartupWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        await _viewModel.RunAsync();
    }

    private void StartupViewModel_OnStartupSucceeded(object? sender, EventArgs e)
    {
        DialogResult = true;
    }

    private void StartupViewModel_OnCloseRequested(object? sender, EventArgs e)
    {
        DialogResult = false;
    }

    private void StartupViewModel_OnOnboardingRequested(
        object? sender,
        EventArgs e)
    {
        OpenOnboardingRequested = true;
        DialogResult = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.StartupSucceeded -= StartupViewModel_OnStartupSucceeded;
        _viewModel.OnboardingRequested -= StartupViewModel_OnOnboardingRequested;
        _viewModel.CloseRequested -= StartupViewModel_OnCloseRequested;
        base.OnClosed(e);
    }
}
