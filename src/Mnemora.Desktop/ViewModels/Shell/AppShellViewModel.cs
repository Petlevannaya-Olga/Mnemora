using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Desktop.ViewModels.Home;
using Mnemora.Desktop.ViewModels.Library;
using Mnemora.Desktop.ViewModels.Pages;

namespace Mnemora.Desktop.ViewModels.Shell;

public sealed partial class AppShellViewModel : ViewModelBase
{
    private readonly IPageNavigationService _pageNavigationService;
    private ViewModelBase? _currentPageViewModel;
    private bool _isSidebarExpanded = true;

    public AppShellViewModel(IPageNavigationService pageNavigationService)
    {
        _pageNavigationService = pageNavigationService;
        _currentPageViewModel = pageNavigationService.CurrentPageViewModel;

        _pageNavigationService.CurrentPageViewModelChanged +=
            OnCurrentPageViewModelChanged;

        if (_currentPageViewModel is null)
        {
            _pageNavigationService.NavigateTo<HomeViewModel>();
        }
    }

    public ViewModelBase? CurrentPageViewModel
    {
        get => _currentPageViewModel;
        private set
        {
            if (!SetProperty(ref _currentPageViewModel, value))
            {
                return;
            }

            NotifySelectedPageChanged();
        }
    }

    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        private set => SetProperty(ref _isSidebarExpanded, value);
    }

    public bool IsHomeSelected =>
        CurrentPageViewModel is HomeViewModel;

    public bool IsLibrarySelected =>
        CurrentPageViewModel is LibraryViewModel;

    public bool IsPracticeSelected =>
        CurrentPageViewModel is PracticeViewModel;

    public bool IsTrainingSelected =>
        CurrentPageViewModel is TrainingViewModel;

    public bool IsPlanSelected =>
        CurrentPageViewModel is PlanViewModel;

    public bool IsProgressSelected =>
        CurrentPageViewModel is ProgressViewModel;

    public bool IsSettingsSelected =>
        CurrentPageViewModel is SettingsViewModel;

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
    }

    [RelayCommand]
    private void NavigateHome()
    {
        NavigateTo<HomeViewModel>();
    }

    [RelayCommand]
    private void NavigateLibrary()
    {
        NavigateTo<LibraryViewModel>();
    }

    [RelayCommand]
    private void NavigatePractice()
    {
        NavigateTo<PracticeViewModel>();
    }

    [RelayCommand]
    private void NavigateTraining()
    {
        NavigateTo<TrainingViewModel>();
    }

    [RelayCommand]
    private void NavigatePlan()
    {
        NavigateTo<PlanViewModel>();
    }

    [RelayCommand]
    private void NavigateProgress()
    {
        NavigateTo<ProgressViewModel>();
    }

    [RelayCommand]
    private void NavigateSettings()
    {
        NavigateTo<SettingsViewModel>();
    }

    private void NavigateTo<TViewModel>()
        where TViewModel : ViewModelBase
    {
        if (CurrentPageViewModel is TViewModel)
        {
            return;
        }

        _pageNavigationService.NavigateTo<TViewModel>();
    }

    private void OnCurrentPageViewModelChanged(
        object? sender,
        EventArgs eventArgs)
    {
        CurrentPageViewModel =
            _pageNavigationService.CurrentPageViewModel;
    }

    private void NotifySelectedPageChanged()
    {
        OnPropertyChanged(nameof(IsHomeSelected));
        OnPropertyChanged(nameof(IsLibrarySelected));
        OnPropertyChanged(nameof(IsPracticeSelected));
        OnPropertyChanged(nameof(IsTrainingSelected));
        OnPropertyChanged(nameof(IsPlanSelected));
        OnPropertyChanged(nameof(IsProgressSelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
    }
}