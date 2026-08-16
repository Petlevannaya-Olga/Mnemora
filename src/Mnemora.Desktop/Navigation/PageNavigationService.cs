using Microsoft.Extensions.DependencyInjection;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.Navigation;

public sealed class PageNavigationService(IServiceProvider serviceProvider) : IPageNavigationService
{
    public ViewModelBase? CurrentPageViewModel { get; private set; }

    public event EventHandler? CurrentPageViewModelChanged;

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        NavigateTo<TViewModel>(_ => { });
    }

    public void NavigateTo<TViewModel>(Action<TViewModel> initialize) where TViewModel : ViewModelBase
    {
        ArgumentNullException.ThrowIfNull(initialize);

        var viewModel = serviceProvider.GetRequiredService<TViewModel>();

        initialize(viewModel);

        CurrentPageViewModel = viewModel;
        CurrentPageViewModelChanged?.Invoke(this, EventArgs.Empty);
    }
}