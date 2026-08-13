using System.ComponentModel;
using System.Windows;
using Mnemora.Desktop.ViewModels.Onboarding;
using Mnemora.Desktop.ViewModels.Shell;
using Mnemora.Desktop.Views.Dialogs;

namespace Mnemora.Desktop;

public partial class MainWindow : Window
{
    private readonly OnboardingState _onboardingState;

    private bool _isCloseConfirmed;

    public MainWindow(
        MainWindowViewModel viewModel,
        OnboardingState onboardingState)
    {
        InitializeComponent();

        DataContext = viewModel;
        _onboardingState = onboardingState;
    }

    protected override void OnClosing(
        CancelEventArgs e)
    {
        if (_isCloseConfirmed ||
            _onboardingState.IsOnboardingCompleted)
        {
            base.OnClosing(e);
            return;
        }

        var dialog = new ExitOnboardingDialog(
            !string.IsNullOrWhiteSpace(
                _onboardingState.PendingApiKey)) { Owner = this, };

        bool? result = dialog.ShowDialog();

        if (result == true)
        {
            _isCloseConfirmed = true;
        }
        else
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }
}