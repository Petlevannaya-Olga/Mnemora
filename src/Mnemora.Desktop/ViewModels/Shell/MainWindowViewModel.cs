using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Shell;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private ViewModelBase? _currentViewModel;

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

    private void OnCurrentViewModelChanged(
        object? sender,
        EventArgs eventArgs)
    {
        CurrentViewModel = _navigationService.CurrentViewModel;
    }
}