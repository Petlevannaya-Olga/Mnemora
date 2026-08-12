using Microsoft.Extensions.DependencyInjection;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.Navigation;

public sealed class NavigationService(IServiceProvider serviceProvider) : INavigationService
{
    public ViewModelBase? CurrentViewModel { get; private set; }

    public event EventHandler? CurrentViewModelChanged;

    public void NavigateTo<TViewModel>()
        where TViewModel : ViewModelBase
    {
        CurrentViewModel =
            serviceProvider.GetRequiredService<TViewModel>();

        CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
    }
}