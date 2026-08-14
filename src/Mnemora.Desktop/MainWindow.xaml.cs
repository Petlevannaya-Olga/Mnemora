using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.ViewModels.Onboarding;
using Mnemora.Desktop.ViewModels.Shell;
using Mnemora.Desktop.Views.Dialogs;

namespace Mnemora.Desktop;

public partial class MainWindow : Window, IDialogOverlayHost
{
    private readonly OnboardingState _onboardingState;

    private bool _isCloseConfirmed;
    private int _openedDialogsCount;

    public MainWindow(
        MainWindowViewModel viewModel,
        OnboardingState onboardingState)
    {
        InitializeComponent();

        DataContext = viewModel;
        _onboardingState = onboardingState;
    }

    public void ShowDialogOverlay()
    {
        _openedDialogsCount++;

        if (_openedDialogsCount > 1)
        {
            return;
        }

        DialogOverlay.Visibility = Visibility.Visible;

        AnimateDialogOverlay(
            targetOpacity: 1,
            duration: TimeSpan.FromMilliseconds(260),
            easingFunction: new QuadraticEase { EasingMode = EasingMode.EaseOut });
    }

    public void HideDialogOverlay()
    {
        if (_openedDialogsCount == 0)
        {
            return;
        }

        _openedDialogsCount--;

        if (_openedDialogsCount > 0)
        {
            return;
        }

        AnimateDialogOverlay(
            targetOpacity: 0,
            duration: TimeSpan.FromMilliseconds(220),
            easingFunction: new QuadraticEase { EasingMode = EasingMode.EaseIn },
            completed: () =>
            {
                if (_openedDialogsCount == 0)
                {
                    DialogOverlay.Visibility = Visibility.Collapsed;
                }
            });
    }

    private void AnimateDialogOverlay(
        double targetOpacity,
        TimeSpan duration,
        IEasingFunction easingFunction,
        Action? completed = null)
    {
        var currentOpacity = DialogOverlay.Opacity;

        DialogOverlay.BeginAnimation(
            OpacityProperty,
            null);

        DialogOverlay.Opacity = currentOpacity;

        var animation = new DoubleAnimation
        {
            From = currentOpacity, To = targetOpacity, Duration = duration, EasingFunction = easingFunction
        };

        animation.Completed += (_, _) =>
        {
            DialogOverlay.BeginAnimation(
                OpacityProperty,
                null);

            DialogOverlay.Opacity = targetOpacity;

            completed?.Invoke();
        };

        DialogOverlay.BeginAnimation(
            OpacityProperty,
            animation);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_isCloseConfirmed || _onboardingState.IsOnboardingCompleted)
        {
            base.OnClosing(e);
            return;
        }

        var dialog = new ExitOnboardingDialog(
            !string.IsNullOrWhiteSpace(_onboardingState.PendingApiKey)) { Owner = this };

        ShowDialogOverlay();

        try
        {
            var result = dialog.ShowDialog();

            if (result == true)
            {
                _isCloseConfirmed = true;
            }
            else
            {
                e.Cancel = true;
            }
        }
        finally
        {
            HideDialogOverlay();
        }

        base.OnClosing(e);
    }
}