using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Desktop.ViewModels.Home;

namespace Mnemora.Desktop.ViewModels.Shell;

public sealed partial class AppShellViewModel
    : ViewModelBase
{
    private const double ExpandedSidebarWidth = 230;

    private const double CollapsedSidebarWidth = 80;

    private readonly IPageNavigationService
        _pageNavigationService;

    private ViewModelBase?
        _currentPageViewModel;

    private bool _isSidebarExpanded = true;

    public AppShellViewModel(
        IPageNavigationService pageNavigationService)
    {
        _pageNavigationService =
            pageNavigationService;

        _currentPageViewModel =
            _pageNavigationService
                .CurrentPageViewModel;

        _pageNavigationService
                .CurrentPageViewModelChanged +=
            OnCurrentPageViewModelChanged;

        if (_currentPageViewModel is null)
        {
            _pageNavigationService
                .NavigateTo<HomeViewModel>();
        }
    }

    public ViewModelBase? CurrentPageViewModel
    {
        get => _currentPageViewModel;

        private set =>
            SetProperty(
                ref _currentPageViewModel,
                value);
    }

    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;

        private set
        {
            if (!SetProperty(
                    ref _isSidebarExpanded,
                    value))
            {
                return;
            }

            OnPropertyChanged(
                nameof(SidebarWidth));
        }
    }

    public double SidebarWidth =>
        IsSidebarExpanded
            ? ExpandedSidebarWidth
            : CollapsedSidebarWidth;

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarExpanded =
            !IsSidebarExpanded;
    }

    [RelayCommand]
    private void NavigateHome()
    {
        if (CurrentPageViewModel
            is HomeViewModel)
        {
            return;
        }

        _pageNavigationService
            .NavigateTo<HomeViewModel>();
    }

    private void OnCurrentPageViewModelChanged(
        object? sender,
        EventArgs eventArgs)
    {
        CurrentPageViewModel =
            _pageNavigationService
                .CurrentPageViewModel;
    }
}