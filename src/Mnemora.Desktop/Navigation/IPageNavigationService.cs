using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.Navigation;

public interface IPageNavigationService
{
    ViewModelBase? CurrentPageViewModel { get; }

    bool CanNavigateBack { get; }

    event EventHandler? CurrentPageViewModelChanged;

    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;

    void NavigateTo<TViewModel>(Action<TViewModel> initialize) where TViewModel : ViewModelBase;

    void NavigateBack();
}
