using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Notifications;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Desktop.ViewModels.Home;
using Mnemora.Desktop.ViewModels.Library;
using Mnemora.Desktop.ViewModels.Pages;

namespace Mnemora.Desktop.ViewModels.Shell;

public sealed partial class AppShellViewModel : ViewModelBase
{
    private readonly IPageNavigationService _pageNavigationService;
    private readonly INotificationService _notificationService;
    private ViewModelBase? _currentPageViewModel;
    private bool _isSidebarExpanded = true;
    private bool _isLibraryMenuExpanded;

    public AppShellViewModel(
        IPageNavigationService pageNavigationService,
        INotificationService notificationService)
    {
        _pageNavigationService = pageNavigationService;
        _notificationService = notificationService;

        _currentPageViewModel = pageNavigationService.CurrentPageViewModel;
        _isLibraryMenuExpanded = IsLibrarySelected;
        _pageNavigationService.CurrentPageViewModelChanged += OnCurrentPageViewModelChanged;

        if (_currentPageViewModel is null)
        {
            _pageNavigationService.NavigateTo<HomeViewModel>();
        }
    }

    public ReadOnlyObservableCollection<NotificationMessage> Notifications => _notificationService.Notifications;
    
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
            IsLibraryMenuExpanded = IsLibrarySelected;
        }
    }

    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        private set
        {
            if (SetProperty(ref _isSidebarExpanded, value))
            {
                OnPropertyChanged(nameof(IsLibrarySubmenuVisible));
            }
        }
    }

    public bool IsLibraryMenuExpanded
    {
        get => _isLibraryMenuExpanded;
        private set
        {
            if (SetProperty(ref _isLibraryMenuExpanded, value))
            {
                OnPropertyChanged(nameof(IsLibrarySubmenuVisible));
            }
        }
    }

    public bool IsLibrarySubmenuVisible => IsSidebarExpanded && IsLibraryMenuExpanded;
    public bool IsHomeSelected => CurrentPageViewModel is HomeViewModel;

    public bool IsLibrarySelected =>
        CurrentPageViewModel is LibraryOverviewViewModel
            or LibrarySectionViewModel
            or LibraryTopicViewModel
            or AllMaterialsViewModel
            or LibraryManagementViewModel;

    public bool IsLibraryOverviewSelected =>
        CurrentPageViewModel is LibraryOverviewViewModel
            or LibrarySectionViewModel
            or LibraryTopicViewModel;
    
    public bool IsAllMaterialsSelected => CurrentPageViewModel is AllMaterialsViewModel;
    public bool IsLibraryManagementSelected => CurrentPageViewModel is LibraryManagementViewModel;
    public bool IsPracticeSelected => CurrentPageViewModel is PracticeViewModel;
    public bool IsTrainingSelected => CurrentPageViewModel is TrainingViewModel;
    public bool IsPlanSelected => CurrentPageViewModel is PlanViewModel;
    public bool IsProgressSelected => CurrentPageViewModel is ProgressViewModel;
    public bool IsSettingsSelected => CurrentPageViewModel is SettingsViewModel;

    [RelayCommand]
    private void DismissNotification(Guid notificationId)
    {
        _notificationService.Dismiss(notificationId);
    }
    
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
        if (!IsSidebarExpanded)
        {
            IsSidebarExpanded = true;
            IsLibraryMenuExpanded = true;

            if (!IsLibrarySelected)
            {
                NavigateTo<LibraryOverviewViewModel>();
            }

            return;
        }

        IsLibraryMenuExpanded = !IsLibraryMenuExpanded;

        if (!IsLibrarySelected)
        {
            NavigateTo<LibraryOverviewViewModel>();
        }
    }

    [RelayCommand]
    private void NavigateLibraryOverview()
    {
        IsLibraryMenuExpanded = true;
        NavigateTo<LibraryOverviewViewModel>();
    }

    [RelayCommand]
    private void NavigateAllMaterials()
    {
        IsLibraryMenuExpanded = true;
        NavigateTo<AllMaterialsViewModel>();
    }

    [RelayCommand]
    private void NavigateLibraryManagement()
    {
        IsLibraryMenuExpanded = true;
        NavigateTo<LibraryManagementViewModel>();
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

    private void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        if (CurrentPageViewModel is TViewModel)
        {
            return;
        }

        _pageNavigationService.NavigateTo<TViewModel>();
    }

    private void OnCurrentPageViewModelChanged(object? sender, EventArgs eventArgs)
    {
        CurrentPageViewModel = _pageNavigationService.CurrentPageViewModel;
    }

    private void NotifySelectedPageChanged()
    {
        OnPropertyChanged(nameof(IsHomeSelected));
        OnPropertyChanged(nameof(IsLibrarySelected));
        OnPropertyChanged(nameof(IsLibraryOverviewSelected));
        OnPropertyChanged(nameof(IsAllMaterialsSelected));
        OnPropertyChanged(nameof(IsLibraryManagementSelected));
        OnPropertyChanged(nameof(IsPracticeSelected));
        OnPropertyChanged(nameof(IsTrainingSelected));
        OnPropertyChanged(nameof(IsPlanSelected));
        OnPropertyChanged(nameof(IsProgressSelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
    }
}