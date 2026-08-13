using Microsoft.Extensions.DependencyInjection;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.Navigation;

public sealed class PageNavigationService(
    IServiceProvider serviceProvider)
    : IPageNavigationService
{
    public ViewModelBase? CurrentPageViewModel
    {
        get;
        private set;
    }

    public event EventHandler?
        CurrentPageViewModelChanged;

    public void NavigateTo<TViewModel>()
        where TViewModel : ViewModelBase
    {
        CurrentPageViewModel =
            serviceProvider
                .GetRequiredService<TViewModel>();

        CurrentPageViewModelChanged?.Invoke(
            this,
            EventArgs.Empty);
    }
}