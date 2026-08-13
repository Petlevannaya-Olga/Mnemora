using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.Navigation;

public interface IPageNavigationService
{
    ViewModelBase? CurrentPageViewModel { get; }

    event EventHandler? CurrentPageViewModelChanged;

    void NavigateTo<TViewModel>()
        where TViewModel : ViewModelBase;
}