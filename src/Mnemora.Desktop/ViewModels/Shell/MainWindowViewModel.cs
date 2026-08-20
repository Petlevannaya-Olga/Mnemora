using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Shell;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private ViewModelBase? _currentViewModel;
    private bool _isInitializing = true;

    public MainWindowViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _currentViewModel = _navigationService.CurrentViewModel;
        _navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;
    }

    public ViewModelBase? CurrentViewModel
    {
        get => _currentViewModel;
        private set =>
            SetProperty(ref _currentViewModel, value);
    }

    public bool IsInitializing
    {
        get => _isInitializing;
        private set =>
            SetProperty(ref _isInitializing, value);
    }

    public void CompleteInitialization()
    {
        IsInitializing = false;
    }

    private void OnCurrentViewModelChanged(
        object? sender,
        EventArgs eventArgs)
    {
        CurrentViewModel = _navigationService.CurrentViewModel;
    }
}
