using Microsoft.Extensions.DependencyInjection;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.Navigation;

public sealed class PageNavigationService(IServiceProvider serviceProvider) : IPageNavigationService
{
    private ViewModelBase? _previousPageViewModel;

    public ViewModelBase? CurrentPageViewModel { get; private set; }

    public bool CanNavigateBack => _previousPageViewModel is not null;

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

        _previousPageViewModel = CurrentPageViewModel;
        CurrentPageViewModel = viewModel;
        CurrentPageViewModelChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NavigateBack()
    {
        if (_previousPageViewModel is null) return;

        CurrentPageViewModel = _previousPageViewModel;
        _previousPageViewModel = null;
        CurrentPageViewModelChanged?.Invoke(this, EventArgs.Empty);
    }
}
