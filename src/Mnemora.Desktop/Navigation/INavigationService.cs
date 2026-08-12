using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.Navigation;

public interface INavigationService
{
    ViewModelBase? CurrentViewModel { get; }

    event EventHandler? CurrentViewModelChanged;

    void NavigateTo<TViewModel>()
        where TViewModel : ViewModelBase;
}