using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Library.GetContainerContents;
using Mnemora.Application.Library.GetFoldersPage;
using Mnemora.Application.Library.GetMaterialsPage;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class LibraryContainerViewModel : ViewModelBase
{
    private enum MixedContentFilter
    {
        All,
        Folders,
        Articles,
        Questions,
    }

    private const int FolderPageSize = LibraryPagingDefaults.PageSize;
    private const int FolderVisiblePageLimit = 7;
    private const int FolderCachePageLimit = 10;
    private const int MaterialPageSize = 50;
    private const int MaterialVisiblePageLimit = 7;
    private const int MaterialCachePageLimit = 10;
    private const int MixedPageSize = 50;
    private const int MixedVisiblePageLimit = 7;
    private const int MixedCachePageLimit = 10;
    private const double DefaultFoldersPaneRatio = 1d / 3d;
    private const double MinFoldersPaneRatio = 0.1;
    private const double MaxFoldersPaneRatio = 0.9;
    private static readonly TimeSpan SearchDelay =
        TimeSpan.FromMilliseconds(350);

    private readonly IQueryDispatcher _queryDispatcher;
    private readonly IPageNavigationService _pageNavigationService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<LibraryContainerViewModel> _logger;

    private readonly BoundedPagedWindow<LibraryFolderDto> _folderWindow =
        new(FolderPageSize, FolderVisiblePageLimit, FolderCachePageLimit);
    private readonly BoundedPagedWindow<LibraryMaterialDto> _materialWindow =
        new(MaterialPageSize, MaterialVisiblePageLimit, MaterialCachePageLimit);
    private readonly BoundedPagedWindow<LibraryContentListItemViewModel> _mixedWindow =
        new(MixedPageSize, MixedVisiblePageLimit, MixedCachePageLimit);

    private CancellationToken _viewCancellationToken;
    private Guid _containerId;
    private int _foldersLoadVersion;
    private int _materialsLoadVersion;
    private int _searchVersion;
    private int _mixedLoadVersion;
    private int _mixedFoldersTotalCount = -1;
    private int _mixedMaterialsTotalCount = -1;
    private MixedContentFilter _mixedContentFilter = MixedContentFilter.All;
    private bool _isLoaded;
    private bool _isViewModeLoaded;

    public LibraryContainerViewModel(
        IQueryDispatcher queryDispatcher,
        IPageNavigationService pageNavigationService,
        ISettingsService settingsService,
        ILogger<LibraryContainerViewModel> logger)
    {
        _queryDispatcher = queryDispatcher;
        _pageNavigationService = pageNavigationService;
        _settingsService = settingsService;
        _logger = logger;

        FilterOptions =
        [
            new("Все", LibraryMaterialFilter.All),
            new("Статьи", LibraryMaterialFilter.Articles),
            new("Вопросы", LibraryMaterialFilter.Questions),
        ];

        SortOptions =
        [
            new(
                "Мой порядок",
                LibraryFolderSort.Custom,
                LibraryMaterialSort.Custom),
            new(
                "Недавно изменённые",
                LibraryFolderSort.RecentlyUpdated,
                LibraryMaterialSort.RecentlyUpdated),
            new(
                "По названию",
                LibraryFolderSort.Name,
                LibraryMaterialSort.Name),
            new(
                "Сначала новые",
                LibraryFolderSort.Newest,
                LibraryMaterialSort.Newest),
        ];

        _selectedFilterOption = FilterOptions[0];
        _selectedSortOption = SortOptions[0];
    }

    public ObservableCollection<LibraryBreadcrumbItemViewModel> Breadcrumbs { get; } = [];
    public ObservableCollection<LibraryFolderCardViewModel> Folders { get; } = [];
    public ObservableCollection<LibraryMaterialListItemViewModel> Materials { get; } = [];
    public ObservableCollection<LibraryContentListItemViewModel> MixedContent { get; } = [];

    public IReadOnlyList<LibraryMaterialFilterOption> FilterOptions { get; }
    public IReadOnlyList<LibraryContainerSortOption> SortOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContainerTitle))]
    [NotifyPropertyChangedFor(nameof(ContainerSubtitle))]
    [NotifyPropertyChangedFor(nameof(IsRoot))]
    [NotifyPropertyChangedFor(nameof(CurrentContainerColor))]
    [NotifyPropertyChangedFor(nameof(CurrentContainerIcon))]
    [NotifyPropertyChangedFor(nameof(HasFolderContent))]
    [NotifyPropertyChangedFor(nameof(HasMaterialContent))]
    [NotifyPropertyChangedFor(nameof(IsMixedContent))]
    [NotifyPropertyChangedFor(nameof(IsFoldersOnly))]
    [NotifyPropertyChangedFor(nameof(IsMaterialsOnly))]
    [NotifyPropertyChangedFor(nameof(HasAnyContent))]
    [NotifyPropertyChangedFor(nameof(IsContainerEmpty))]
    [NotifyPropertyChangedFor(nameof(EmptyStateTitle))]
    private LibraryContainerContentsDto? _contents;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isLoadingMetadata;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFoldersEmpty))]
    [NotifyPropertyChangedFor(nameof(IsFoldersPaging))]
    [NotifyPropertyChangedFor(nameof(FoldersHasPrevious))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextFoldersPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousFoldersPageCommand))]
    private bool _isLoadingFolders;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFoldersPaging))]
    [NotifyPropertyChangedFor(nameof(FoldersHasPrevious))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextFoldersPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousFoldersPageCommand))]
    private bool _isLoadingNextFoldersPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFoldersPaging))]
    [NotifyPropertyChangedFor(nameof(FoldersHasPrevious))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextFoldersPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousFoldersPageCommand))]
    private bool _isLoadingPreviousFoldersPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFoldersEmpty))]
    [NotifyPropertyChangedFor(nameof(FoldersShownCountText))]
    private bool _hasCompletedInitialFoldersLoad;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMaterialsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsMaterialsPaging))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextMaterialsPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousMaterialsPageCommand))]
    private bool _isLoadingMaterials;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMaterialsPaging))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextMaterialsPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousMaterialsPageCommand))]
    private bool _isLoadingNextMaterialsPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMaterialsPaging))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextMaterialsPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousMaterialsPageCommand))]
    private bool _isLoadingPreviousMaterialsPage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadNextFoldersPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousFoldersPageCommand))]
    private bool _foldersHasMore;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadNextMaterialsPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousMaterialsPageCommand))]
    private bool _materialsHasMore;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMixedEmpty))]
    [NotifyPropertyChangedFor(nameof(IsMixedPaging))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextMixedPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousMixedPageCommand))]
    private bool _isLoadingMixed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMixedPaging))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextMixedPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousMixedPageCommand))]
    private bool _isLoadingNextMixedPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMixedPaging))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextMixedPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousMixedPageCommand))]
    private bool _isLoadingPreviousMixedPage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadNextMixedPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousMixedPageCommand))]
    private bool _mixedHasMore;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMixedError))]
    [NotifyPropertyChangedFor(nameof(IsMixedEmpty))]
    private string? _mixedErrorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MixedShownCountText))]
    private int _mixedTotalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MixedShownCountText))]
    private int _mixedCurrentPageOffset;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFoldersError))]
    [NotifyPropertyChangedFor(nameof(FoldersHasPrevious))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextFoldersPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousFoldersPageCommand))]
    private string? _foldersErrorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMaterialsError))]
    private string? _materialsErrorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFoldersEmpty))]
    [NotifyPropertyChangedFor(nameof(IsMaterialsEmpty))]
    [NotifyPropertyChangedFor(nameof(FoldersShownCountText))]
    [NotifyPropertyChangedFor(nameof(MaterialsShownCountText))]
    [NotifyPropertyChangedFor(nameof(MixedShownCountText))]
    [NotifyPropertyChangedFor(nameof(FoldersEmptyMessage))]
    [NotifyPropertyChangedFor(nameof(MixedEmptyMessage))]
    [NotifyPropertyChangedFor(nameof(MaterialsEmptyMessage))]
    private string? _searchText;

    [ObservableProperty]
    private LibraryMaterialFilterOption _selectedFilterOption;

    [ObservableProperty]
    private LibraryContainerSortOption _selectedSortOption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FoldersShownCountText))]
    private int _foldersTotalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FoldersShownCountText))]
    private int _foldersCurrentPageOffset;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaterialsShownCountText))]
    private int _materialsTotalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaterialsShownCountText))]
    private int _materialsCurrentPageOffset;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTableView))]
    [NotifyPropertyChangedFor(nameof(IsTilesView))]
    [NotifyPropertyChangedFor(nameof(IsCompactTilesView))]
    [NotifyPropertyChangedFor(nameof(IsTilesPerRowSelectorVisible))]
    private LibraryTopicsViewMode _viewMode = LibraryTopicsViewMode.CompactTiles;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DesiredTilesPerRow))]
    private LibraryTilesPerRowOption _selectedTilesPerRowOption =
        LibraryTilesPerRowOptions.Auto;

    [ObservableProperty]
    private double _foldersPaneRatio = DefaultFoldersPaneRatio;

    [ObservableProperty]
    private int _actualFolderTilesPerRow = 1;

    [ObservableProperty]
    private int _actualFolderCompactTilesPerRow = 1;

    [ObservableProperty]
    private int _actualMaterialTilesPerRow = 1;

    [ObservableProperty]
    private int _actualMaterialCompactTilesPerRow = 1;

    [ObservableProperty]
    private int _actualMixedTilesPerRow = 1;

    [ObservableProperty]
    private int _actualMixedCompactTilesPerRow = 1;

    public string ContainerTitle =>
        Contents?.Container.Name ?? "Библиотека";

    public string ContainerSubtitle
    {
        get
        {
            if (Contents is null)
                return "Папки и материалы";

            if (!HasAnyContent)
                return IsRoot ? "Раздел пока пуст" : "Папка пока пуста";

            string content = (HasFolderContent, HasMaterialContent) switch
            {
                (true, true) => "Папки и материалы",
                (true, false) => "Папки",
                (false, true) => "Материалы",
                _ => string.Empty,
            };

            return IsRoot ? $"{content} раздела" : content;
        }
    }

    public bool IsRoot =>
        Contents?.Container.IsRoot == true;

    public string? CurrentContainerColor =>
        Contents?.Container.Color;

    public string? CurrentContainerIcon =>
        Contents?.Container.Icon;

    public bool IsBusy => IsLoadingMetadata;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasFoldersError => !string.IsNullOrWhiteSpace(FoldersErrorMessage);
    public bool HasMaterialsError => !string.IsNullOrWhiteSpace(MaterialsErrorMessage);
    public bool HasMixedError => !string.IsNullOrWhiteSpace(MixedErrorMessage);
    public bool HasFolders => Folders.Count > 0;
    public bool FoldersHasPrevious =>
        _folderWindow.HasPrevious &&
        !IsLoadingFolders &&
        !IsLoadingNextFoldersPage &&
        !IsLoadingPreviousFoldersPage &&
        !HasFoldersError;

    public bool IsFoldersPaging =>
        IsLoadingNextFoldersPage ||
        IsLoadingPreviousFoldersPage;

    public int FolderWindowStartOffset => _folderWindow.WindowStartOffset;
    public int FolderWindowEndOffset => _folderWindow.WindowEndOffset;
    public int FolderCachedPageCount => _folderWindow.CachedPageCount;
    public int FolderCachedItemUpperBound => _folderWindow.CachedItemUpperBound;

    public bool MaterialsHasPrevious =>
        _materialWindow.HasPrevious &&
        !IsLoadingMaterials &&
        !IsLoadingNextMaterialsPage &&
        !IsLoadingPreviousMaterialsPage &&
        !HasMaterialsError;
    public bool IsMaterialsPaging =>
        IsLoadingNextMaterialsPage || IsLoadingPreviousMaterialsPage;
    public int MaterialWindowStartOffset => _materialWindow.WindowStartOffset;
    public int MaterialWindowEndOffset => _materialWindow.WindowEndOffset;

    public bool MixedHasPrevious =>
        _mixedWindow.HasPrevious &&
        !IsLoadingMixed &&
        !IsLoadingNextMixedPage &&
        !IsLoadingPreviousMixedPage &&
        !HasMixedError;
    public bool IsMixedPaging =>
        IsLoadingNextMixedPage || IsLoadingPreviousMixedPage;
    public int MixedWindowStartOffset => _mixedWindow.WindowStartOffset;
    public int MixedWindowEndOffset => _mixedWindow.WindowEndOffset;

    public bool HasMaterials => Materials.Count > 0;
    public bool HasMixedItems => MixedContent.Count > 0;
    public bool HasFolderContent => Contents?.FoldersCount > 0;
    public bool HasMaterialContent => Contents?.MaterialsCount > 0;
    public bool IsMixedContent => HasFolderContent && HasMaterialContent;
    public bool IsFoldersOnly => HasFolderContent && !HasMaterialContent;
    public bool IsMaterialsOnly => HasMaterialContent && !HasFolderContent;
    public bool HasAnyContent => HasFolderContent || HasMaterialContent;
    public bool IsContainerEmpty => Contents is not null && !HasAnyContent;
    public string EmptyStateTitle => IsRoot ? "В разделе пока ничего нет" : "В этой папке пока ничего нет";

    public bool IsFoldersEmpty =>
        HasCompletedInitialFoldersLoad &&
        !IsLoadingFolders &&
        !HasFoldersError &&
        !HasFolders;

    public bool IsMaterialsEmpty =>
        !IsLoadingMaterials &&
        !HasMaterialsError &&
        !HasMaterials;

    public bool IsMixedEmpty =>
        !IsLoadingMixed &&
        !HasMixedError &&
        !HasMixedItems;

    public string FoldersEmptyMessage =>
        string.IsNullOrWhiteSpace(SearchText)
            ? "Папки не найдены"
            : "По запросу папки не найдены";

    public string MaterialsEmptyMessage
    {
        get
        {
            bool hasSearch = !string.IsNullOrWhiteSpace(SearchText);
            bool hasFilter = !IsAllFilter;

            return (hasSearch, hasFilter) switch
            {
                (true, true) => "По запросу и выбранному фильтру ничего не найдено",
                (true, false) => "По запросу ничего не найдено",
                (false, true) => "По выбранному фильтру ничего не найдено",
                _ => "Материалы не найдены",
            };
        }
    }

    public string MixedEmptyMessage
    {
        get
        {
            bool hasSearch = !string.IsNullOrWhiteSpace(SearchText);
            bool hasFilter = !IsMixedAllFilter;

            return (hasSearch, hasFilter) switch
            {
                (true, true) => "По запросу и выбранному фильтру ничего не найдено",
                (true, false) => "По запросу ничего не найдено",
                (false, true) => "По выбранному фильтру ничего не найдено",
                _ => "Содержимое не найдено",
            };
        }
    }

    public bool IsMixedAllFilter => _mixedContentFilter == MixedContentFilter.All;
    public bool IsMixedFoldersFilter => _mixedContentFilter == MixedContentFilter.Folders;
    public bool IsMixedArticlesFilter => _mixedContentFilter == MixedContentFilter.Articles;
    public bool IsMixedQuestionsFilter => _mixedContentFilter == MixedContentFilter.Questions;

    public bool IsAllFilter =>
        SelectedFilterOption.Filter == LibraryMaterialFilter.All;

    public bool IsArticlesFilter =>
        SelectedFilterOption.Filter == LibraryMaterialFilter.Articles;

    public bool IsQuestionsFilter =>
        SelectedFilterOption.Filter == LibraryMaterialFilter.Questions;

    public bool IsTableView => ViewMode == LibraryTopicsViewMode.Table;
    public bool IsTilesView => ViewMode == LibraryTopicsViewMode.Tiles;
    public bool IsCompactTilesView => ViewMode == LibraryTopicsViewMode.CompactTiles;

    public bool IsTilesPerRowSelectorVisible => !IsTableView;

    public IReadOnlyList<LibraryTilesPerRowOption> TilesPerRowOptions =>
        LibraryTilesPerRowOptions.All;

    public int DesiredTilesPerRow =>
        SelectedTilesPerRowOption.Value ?? 0;

    public string FoldersShownCountText
    {
        get
        {
            if (!HasCompletedInitialFoldersLoad)
            {
                return string.Empty;
            }

            int visibleCount = Math.Min(
                FolderPageSize,
                Math.Max(0, FoldersTotalCount - FoldersCurrentPageOffset));

            return LibraryRangeTextFormatter.FormatEntity(
                "Папки",
                "Папки не найдены",
                FoldersCurrentPageOffset,
                visibleCount,
                FoldersTotalCount,
                !string.IsNullOrWhiteSpace(SearchText));
        }
    }

    public void UpdateFoldersViewport(double logicalItemOffset)
    {
        if (!_folderWindow.UpdateViewport(logicalItemOffset))
        {
            return;
        }

        FoldersCurrentPageOffset = _folderWindow.CurrentPageOffset;
    }

    public string MaterialsShownCountText
    {
        get
        {
            int visibleCount = Math.Min(
                MaterialPageSize,
                Math.Max(0, MaterialsTotalCount - MaterialsCurrentPageOffset));

            return LibraryRangeTextFormatter.Format(
                MaterialsCurrentPageOffset,
                visibleCount,
                MaterialsTotalCount,
                !string.IsNullOrWhiteSpace(SearchText) || !IsAllFilter);
        }
    }

    public void UpdateMaterialsViewport(double logicalItemOffset)
    {
        if (!_materialWindow.UpdateViewport(logicalItemOffset))
        {
            return;
        }

        MaterialsCurrentPageOffset = _materialWindow.CurrentPageOffset;
    }

    public string MixedShownCountText
    {
        get
        {
            int visibleCount = Math.Min(
                MixedPageSize,
                Math.Max(0, MixedTotalCount - MixedCurrentPageOffset));

            return LibraryRangeTextFormatter.FormatEntity(
                "Элементы",
                "Содержимое не найдено",
                MixedCurrentPageOffset,
                visibleCount,
                MixedTotalCount,
                !string.IsNullOrWhiteSpace(SearchText) || !IsMixedAllFilter);
        }
    }

    public void UpdateMixedViewport(double logicalItemOffset)
    {
        if (!_mixedWindow.UpdateViewport(logicalItemOffset))
        {
            return;
        }

        MixedCurrentPageOffset = _mixedWindow.CurrentPageOffset;
    }

    public void Initialize(Guid containerId)
    {
        if (containerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Идентификатор контейнера не может быть пустым.",
                nameof(containerId));
        }

        if (_isLoaded)
        {
            throw new InvalidOperationException(
                "Нельзя изменить контейнер после начала загрузки.");
        }

        _containerId = containerId;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_containerId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Контейнер библиотеки не был инициализирован.");
        }

        _viewCancellationToken = cancellationToken;
        _isLoaded = true;

        _logger.LogInformation("Открываем контейнер библиотеки {ContainerId}", _containerId);
        await EnsureViewModeLoadedAsync(cancellationToken);

        if (!await LoadMetadataAndBreadcrumbsAsync(cancellationToken))
        {
            return;
        }

        await ReloadAvailableContentAsync(cancellationToken);
    }

    [RelayCommand]
    private void NavigateLibrary()
    {
        _pageNavigationService.NavigateTo<LibraryOverviewViewModel>();
    }

    [RelayCommand]
    private void NavigateBack()
    {
        Guid? parentId = Contents?.Container.ParentId;

        if (parentId is null)
        {
            NavigateLibrary();
            return;
        }

        NavigateToContainer(parentId.Value);
    }

    [RelayCommand]
    private void NavigateBreadcrumb(LibraryBreadcrumbItemViewModel? item)
    {
        if (item is null ||
            item.IsCurrent ||
            item.ContainerId == _containerId)
        {
            return;
        }

        NavigateToContainer(item.ContainerId);
    }

    [RelayCommand]
    private void OpenFolder(LibraryFolderCardViewModel? folder)
    {
        if (folder is null)
        {
            return;
        }

        NavigateToContainer(folder.Id);
    }

    [RelayCommand]
    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                _viewCancellationToken,
                cancellationToken);

        CancellationToken token = linkedCancellationTokenSource.Token;

        if (!await LoadMetadataAndBreadcrumbsAsync(token))
        {
            return;
        }

        await ReloadAvailableContentAsync(token);
    }

    [RelayCommand(CanExecute = nameof(CanLoadNextFoldersPage))]
    private Task LoadNextFoldersPageAsync(CancellationToken cancellationToken)
    {
        return LoadNextFoldersPageWithLinkedCancellationAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanLoadPreviousFoldersPage))]
    private Task LoadPreviousFoldersPageAsync(CancellationToken cancellationToken)
    {
        return LoadPreviousFoldersPageWithLinkedCancellationAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanLoadNextMaterialsPage))]
    private Task LoadNextMaterialsPageAsync(CancellationToken cancellationToken)
    {
        return LoadNextMaterialsPageWithLinkedCancellationAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanLoadPreviousMaterialsPage))]
    private Task LoadPreviousMaterialsPageAsync(CancellationToken cancellationToken)
    {
        return LoadPreviousMaterialsPageWithLinkedCancellationAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanLoadNextMixedPage))]
    private Task LoadNextMixedPageAsync(CancellationToken cancellationToken)
    {
        return LoadNextMixedPageWithLinkedCancellationAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanLoadPreviousMixedPage))]
    private Task LoadPreviousMixedPageAsync(CancellationToken cancellationToken)
    {
        return LoadPreviousMixedPageWithLinkedCancellationAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task RetryFoldersAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                _viewCancellationToken,
                cancellationToken);

        await ReloadFoldersAsync(linkedCancellationTokenSource.Token);
    }

    [RelayCommand]
    private async Task RetryMaterialsAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                _viewCancellationToken,
                cancellationToken);

        await ReloadMaterialsAsync(linkedCancellationTokenSource.Token);
    }

    [RelayCommand]
    private void SelectAllMaterials()
    {
        SelectedFilterOption = FilterOptions[0];
    }

    [RelayCommand]
    private void SelectArticles()
    {
        SelectedFilterOption = FilterOptions[1];
    }

    [RelayCommand]
    private void SelectQuestions()
    {
        SelectedFilterOption = FilterOptions[2];
    }

    [RelayCommand]
    private void SelectAllContent() =>
        SetMixedContentFilter(MixedContentFilter.All);

    [RelayCommand]
    private void SelectFoldersContent() =>
        SetMixedContentFilter(MixedContentFilter.Folders);

    [RelayCommand]
    private void SelectArticlesContent() =>
        SetMixedContentFilter(MixedContentFilter.Articles);

    [RelayCommand]
    private void SelectQuestionsContent() =>
        SetMixedContentFilter(MixedContentFilter.Questions);

    private void SetMixedContentFilter(MixedContentFilter filter)
    {
        if (_mixedContentFilter == filter)
        {
            return;
        }

        _mixedContentFilter = filter;
        OnPropertyChanged(nameof(IsMixedAllFilter));
        OnPropertyChanged(nameof(IsMixedFoldersFilter));
        OnPropertyChanged(nameof(IsMixedArticlesFilter));
        OnPropertyChanged(nameof(IsMixedQuestionsFilter));
        OnPropertyChanged(nameof(MixedEmptyMessage));
        OnPropertyChanged(nameof(MixedShownCountText));

        if (_isLoaded && IsMixedContent)
        {
            _ = ReloadMixedFromSelectionChangeAsync();
        }
    }

    [RelayCommand]
    private Task ShowTilesViewAsync(CancellationToken cancellationToken) =>
        SetViewModeAsync(LibraryTopicsViewMode.Tiles, cancellationToken);

    [RelayCommand]
    private Task ShowCompactTilesViewAsync(CancellationToken cancellationToken) =>
        SetViewModeAsync(LibraryTopicsViewMode.CompactTiles, cancellationToken);

    [RelayCommand]
    private Task ShowTableViewAsync(CancellationToken cancellationToken) =>
        SetViewModeAsync(LibraryTopicsViewMode.Table, cancellationToken);

    partial void OnSearchTextChanged(string? value)
    {
        int searchVersion = Interlocked.Increment(ref _searchVersion);

        if (_isLoaded)
        {
            _ = ReloadAfterSearchDelayAsync(searchVersion);
        }
    }

    partial void OnSelectedFilterOptionChanged(LibraryMaterialFilterOption value)
    {
        OnPropertyChanged(nameof(IsAllFilter));
        OnPropertyChanged(nameof(IsArticlesFilter));
        OnPropertyChanged(nameof(IsQuestionsFilter));
        OnPropertyChanged(nameof(MaterialsEmptyMessage));

        if (_isLoaded && HasMaterialContent && !IsMixedContent)
        {
            _ = ReloadMaterialsFromSelectionChangeAsync();
        }
    }

    partial void OnSelectedSortOptionChanged(LibraryContainerSortOption value)
    {
        if (_isLoaded)
        {
            _ = ReloadCollectionsFromSelectionChangeAsync();
        }
    }

    partial void OnSelectedTilesPerRowOptionChanged(
        LibraryTilesPerRowOption value)
    {
        if (_isViewModeLoaded)
        {
            _ = SaveTilesPerRowAsync(value.Value);
        }
    }

    private async Task<bool> LoadMetadataAndBreadcrumbsAsync(
        CancellationToken cancellationToken)
    {
        IsLoadingMetadata = true;
        ErrorMessage = null;

        try
        {
            var result = await _queryDispatcher.SendAsync<
                GetLibraryContainerContentsQuery,
                LibraryContainerContentsDto>(
                new GetLibraryContainerContentsQuery(_containerId),
                cancellationToken);

            if (result.IsFailure)
            {
                ErrorMessage = result.Error.FirstOrDefault()?.Message
                               ?? "Не удалось загрузить папку библиотеки";
                return false;
            }

            Contents = result.Value;
            _logger.LogInformation(
                "Контейнер {ContainerId} найден: {FoldersCount} папок, {MaterialsCount} материалов",
                _containerId,
                result.Value.FoldersCount,
                result.Value.MaterialsCount);
            await BuildBreadcrumbsAsync(result.Value, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Не удалось загрузить контейнер библиотеки {ContainerId}",
                _containerId);

            ErrorMessage = "Не удалось загрузить папку библиотеки";
            return false;
        }
        finally
        {
            IsLoadingMetadata = false;
        }
    }

    private async Task BuildBreadcrumbsAsync(
        LibraryContainerContentsDto current,
        CancellationToken cancellationToken)
    {
        var path = new List<LibraryContainerContentsDto> { current };
        var visited = new HashSet<Guid> { current.Container.Id };
        Guid? parentId = current.Container.ParentId;

        while (parentId is { } id && visited.Add(id))
        {
            var result = await _queryDispatcher.SendAsync<
                GetLibraryContainerContentsQuery,
                LibraryContainerContentsDto>(
                new GetLibraryContainerContentsQuery(id),
                cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogWarning(
                    "Не удалось получить родительский контейнер {ContainerId} для breadcrumbs",
                    id);
                break;
            }

            LibraryContainerContentsDto parent = result.Value;
            path.Add(parent);
            parentId = parent.Container.ParentId;
        }

        path.Reverse();
        Breadcrumbs.Clear();

        foreach (LibraryContainerContentsDto pathItem in path)
        {
            LibraryContainerHeaderDto item = pathItem.Container;

            Breadcrumbs.Add(
                new LibraryBreadcrumbItemViewModel(
                    item.Id,
                    item.Name,
                    item.Depth,
                    item.Id == current.Container.Id));
        }
    }

    private async Task ReloadAvailableContentAsync(CancellationToken cancellationToken)
    {
        if (IsMixedContent)
        {
            ResetFolders();
            ResetMaterials();
            await ReloadMixedAsync(cancellationToken);
            return;
        }

        ResetMixed();

        if (!HasFolderContent)
            ResetFolders();
        if (!HasMaterialContent)
            ResetMaterials();

        Task foldersTask = HasFolderContent ? ReloadFoldersAsync(cancellationToken) : Task.CompletedTask;
        Task materialsTask = HasMaterialContent ? ReloadMaterialsAsync(cancellationToken) : Task.CompletedTask;
        await Task.WhenAll(foldersTask, materialsTask);
    }

    private void ResetFolders()
    {
        Interlocked.Increment(ref _foldersLoadVersion);
        _folderWindow.Reset();
        FoldersCurrentPageOffset = 0;
        FoldersTotalCount = 0;
        FoldersHasMore = false;
        HasCompletedInitialFoldersLoad = false;
        IsLoadingFolders = false;
        IsLoadingNextFoldersPage = false;
        IsLoadingPreviousFoldersPage = false;
        FoldersErrorMessage = null;
        Folders.Clear();
        SyncFolderWindowState();
    }

    private void ResetMaterials()
    {
        Interlocked.Increment(ref _materialsLoadVersion);
        _materialWindow.Reset();
        MaterialsCurrentPageOffset = 0;
        MaterialsTotalCount = 0;
        MaterialsHasMore = false;
        IsLoadingMaterials = false;
        IsLoadingNextMaterialsPage = false;
        IsLoadingPreviousMaterialsPage = false;
        MaterialsErrorMessage = null;
        Materials.Clear();
        SyncMaterialWindowState();
    }

    private void ResetMixed()
    {
        Interlocked.Increment(ref _mixedLoadVersion);
        _mixedWindow.Reset();
        _mixedFoldersTotalCount = -1;
        _mixedMaterialsTotalCount = -1;
        MixedCurrentPageOffset = 0;
        MixedTotalCount = 0;
        MixedHasMore = false;
        IsLoadingMixed = false;
        IsLoadingNextMixedPage = false;
        IsLoadingPreviousMixedPage = false;
        MixedErrorMessage = null;
        MixedContent.Clear();
        SyncMixedWindowState();
    }

    private async Task ReloadFoldersAsync(CancellationToken cancellationToken)
    {
        int version = Interlocked.Increment(ref _foldersLoadVersion);

        _folderWindow.Reset();
        FoldersCurrentPageOffset = 0;
        FoldersTotalCount = 0;
        FoldersHasMore = false;
        IsLoadingNextFoldersPage = false;
        IsLoadingPreviousFoldersPage = false;
        FoldersErrorMessage = null;
        Folders.Clear();
        SyncFolderWindowState();

        await ShowFolderPageAsync(
            0,
            PageWindowInsert.Append,
            version,
            cancellationToken,
            isInitialPage: true,
            isPreviousPage: false);
    }

    private Task LoadNextFoldersWindowAsync(
        int version,
        CancellationToken cancellationToken)
    {
        if (version != _foldersLoadVersion ||
            !_folderWindow.HasNext ||
            IsLoadingFolders ||
            IsLoadingNextFoldersPage ||
            IsLoadingPreviousFoldersPage ||
            cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        return ShowFolderPageAsync(
            _folderWindow.NextOffset,
            PageWindowInsert.Append,
            version,
            cancellationToken,
            isInitialPage: false,
            isPreviousPage: false);
    }

    private Task LoadPreviousFoldersWindowAsync(
        int version,
        CancellationToken cancellationToken)
    {
        if (version != _foldersLoadVersion ||
            !_folderWindow.HasPrevious ||
            IsLoadingFolders ||
            IsLoadingNextFoldersPage ||
            IsLoadingPreviousFoldersPage ||
            cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        return ShowFolderPageAsync(
            _folderWindow.PreviousOffset,
            PageWindowInsert.Prepend,
            version,
            cancellationToken,
            isInitialPage: false,
            isPreviousPage: true);
    }

    private async Task ShowFolderPageAsync(
        int offset,
        PageWindowInsert insert,
        int version,
        CancellationToken cancellationToken,
        bool isInitialPage,
        bool isPreviousPage)
    {
        if (version != _foldersLoadVersion ||
            cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_folderWindow.TryGetCachedPage(
                offset,
                out IReadOnlyList<LibraryFolderDto> cached))
        {
            _folderWindow.ShowPage(offset, cached, insert);
            RebuildFolders();
            SyncFolderWindowState();

            if (isInitialPage)
            {
                HasCompletedInitialFoldersLoad = true;
            }

            return;
        }

        if (isInitialPage)
        {
            IsLoadingFolders = true;
        }
        else if (isPreviousPage)
        {
            IsLoadingPreviousFoldersPage = true;
        }
        else
        {
            IsLoadingNextFoldersPage = true;
        }

        FoldersErrorMessage = null;

        if (!isInitialPage)
        {
            await YieldForPagingLoaderAsync(cancellationToken);
        }

        try
        {
            var query = new GetLibraryFoldersPageQuery(
                _containerId,
                SearchText,
                SelectedSortOption.FolderSort,
                offset,
                FolderPageSize);

            var result = await _queryDispatcher.SendAsync<
                GetLibraryFoldersPageQuery,
                LibraryFoldersPageDto>(
                query,
                cancellationToken);

            if (version != _foldersLoadVersion ||
                cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (result.IsFailure)
            {
                FoldersErrorMessage = result.Error.FirstOrDefault()?.Message
                                      ?? "Не удалось загрузить папки";
                return;
            }

            _folderWindow.SetTotalCount(result.Value.TotalCount);
            _folderWindow.ShowPage(offset, result.Value.Items, insert);
            RebuildFolders();
            SyncFolderWindowState();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Смена страницы или контекста.
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Не удалось загрузить папки контейнера {ContainerId}, offset {Offset}",
                _containerId,
                offset);

            FoldersErrorMessage = "Не удалось загрузить папки";
        }
        finally
        {
            if (version == _foldersLoadVersion)
            {
                if (isInitialPage)
                {
                    HasCompletedInitialFoldersLoad = true;
                }

                IsLoadingFolders = false;
                IsLoadingNextFoldersPage = false;
                IsLoadingPreviousFoldersPage = false;
                SyncFolderWindowState();
            }
        }
    }

    private static async Task YieldForPagingLoaderAsync(
        CancellationToken cancellationToken)
    {
        Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;

        if (dispatcher is null || dispatcher.HasShutdownStarted)
        {
            await Task.Yield();
            return;
        }

        await dispatcher.InvokeAsync(
            static () => { },
            DispatcherPriority.Background,
            cancellationToken);
    }

    private void RebuildFolders()
    {
        Folders.Clear();

        foreach (int offset in _folderWindow.VisibleOffsets)
        {
            if (!_folderWindow.TryGetCachedPage(
                    offset,
                    out IReadOnlyList<LibraryFolderDto> page))
            {
                continue;
            }

            foreach (LibraryFolderDto folder in page)
            {
                Folders.Add(new LibraryFolderCardViewModel(folder));
            }
        }
    }

    private void SyncFolderWindowState()
    {
        FoldersTotalCount = _folderWindow.TotalCount;
        FoldersCurrentPageOffset = _folderWindow.CurrentPageOffset;
        FoldersHasMore = _folderWindow.HasNext;

        OnPropertyChanged(nameof(FoldersHasPrevious));
        OnPropertyChanged(nameof(IsFoldersPaging));
        OnPropertyChanged(nameof(FolderWindowStartOffset));
        OnPropertyChanged(nameof(FolderWindowEndOffset));
        OnPropertyChanged(nameof(FolderCachedPageCount));
        OnPropertyChanged(nameof(FolderCachedItemUpperBound));

        LoadNextFoldersPageCommand.NotifyCanExecuteChanged();
        LoadPreviousFoldersPageCommand.NotifyCanExecuteChanged();
        NotifyFolderStateChanged();
    }

    private async Task ReloadMaterialsAsync(CancellationToken cancellationToken)
    {
        int version = Interlocked.Increment(ref _materialsLoadVersion);

        _materialWindow.Reset();
        MaterialsCurrentPageOffset = 0;
        MaterialsTotalCount = 0;
        MaterialsHasMore = false;
        IsLoadingNextMaterialsPage = false;
        IsLoadingPreviousMaterialsPage = false;
        MaterialsErrorMessage = null;
        Materials.Clear();
        SyncMaterialWindowState();

        await ShowMaterialPageAsync(
            0,
            PageWindowInsert.Append,
            version,
            cancellationToken,
            isInitialPage: true,
            isPreviousPage: false);
    }

    private Task LoadNextMaterialsWindowAsync(
        int version,
        CancellationToken cancellationToken)
    {
        if (version != _materialsLoadVersion ||
            !_materialWindow.HasNext ||
            IsLoadingMaterials ||
            IsLoadingNextMaterialsPage ||
            IsLoadingPreviousMaterialsPage ||
            cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        return ShowMaterialPageAsync(
            _materialWindow.NextOffset,
            PageWindowInsert.Append,
            version,
            cancellationToken,
            isInitialPage: false,
            isPreviousPage: false);
    }

    private Task LoadPreviousMaterialsWindowAsync(
        int version,
        CancellationToken cancellationToken)
    {
        if (version != _materialsLoadVersion ||
            !_materialWindow.HasPrevious ||
            IsLoadingMaterials ||
            IsLoadingNextMaterialsPage ||
            IsLoadingPreviousMaterialsPage ||
            cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        return ShowMaterialPageAsync(
            _materialWindow.PreviousOffset,
            PageWindowInsert.Prepend,
            version,
            cancellationToken,
            isInitialPage: false,
            isPreviousPage: true);
    }

    private async Task ShowMaterialPageAsync(
        int offset,
        PageWindowInsert insert,
        int version,
        CancellationToken cancellationToken,
        bool isInitialPage,
        bool isPreviousPage)
    {
        if (version != _materialsLoadVersion || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_materialWindow.TryGetCachedPage(
                offset,
                out IReadOnlyList<LibraryMaterialDto> cached))
        {
            _materialWindow.ShowPage(offset, cached, insert);
            RebuildMaterials();
            SyncMaterialWindowState();
            return;
        }

        if (isInitialPage)
        {
            IsLoadingMaterials = true;
        }
        else if (isPreviousPage)
        {
            IsLoadingPreviousMaterialsPage = true;
        }
        else
        {
            IsLoadingNextMaterialsPage = true;
        }

        MaterialsErrorMessage = null;

        if (!isInitialPage)
        {
            await YieldForPagingLoaderAsync(cancellationToken);
        }

        try
        {
            var result = await _queryDispatcher.SendAsync<
                GetLibraryMaterialsPageQuery,
                LibraryMaterialsPageDto>(
                new GetLibraryMaterialsPageQuery(
                    _containerId,
                    SearchText,
                    SelectedFilterOption.Filter,
                    SelectedSortOption.MaterialSort,
                    offset,
                    MaterialPageSize),
                cancellationToken);

            if (version != _materialsLoadVersion || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (result.IsFailure)
            {
                MaterialsErrorMessage = result.Error.FirstOrDefault()?.Message
                                        ?? "Не удалось загрузить материалы";
                return;
            }

            _materialWindow.SetTotalCount(result.Value.TotalCount);
            _materialWindow.ShowPage(offset, result.Value.Items, insert);
            RebuildMaterials();
            SyncMaterialWindowState();

            _logger.LogInformation(
                "Материалы контейнера {ContainerId}: загружено {ItemsCount}, всего {TotalCount}, offset {Offset}",
                _containerId,
                result.Value.Items.Count,
                result.Value.TotalCount,
                offset);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Смена страницы или контекста.
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Не удалось загрузить материалы контейнера {ContainerId}, offset {Offset}",
                _containerId,
                offset);

            MaterialsErrorMessage = "Не удалось загрузить материалы";
        }
        finally
        {
            if (version == _materialsLoadVersion)
            {
                IsLoadingMaterials = false;
                IsLoadingNextMaterialsPage = false;
                IsLoadingPreviousMaterialsPage = false;
                SyncMaterialWindowState();
            }
        }
    }

    private void RebuildMaterials()
    {
        Materials.Clear();

        foreach (int offset in _materialWindow.VisibleOffsets)
        {
            if (!_materialWindow.TryGetCachedPage(
                    offset,
                    out IReadOnlyList<LibraryMaterialDto> page))
            {
                continue;
            }

            foreach (LibraryMaterialDto material in page)
            {
                Materials.Add(new LibraryMaterialListItemViewModel(material));
            }
        }
    }

    private void SyncMaterialWindowState()
    {
        MaterialsTotalCount = _materialWindow.TotalCount;
        MaterialsCurrentPageOffset = _materialWindow.CurrentPageOffset;
        MaterialsHasMore = _materialWindow.HasNext;

        OnPropertyChanged(nameof(MaterialsHasPrevious));
        OnPropertyChanged(nameof(IsMaterialsPaging));
        OnPropertyChanged(nameof(MaterialWindowStartOffset));
        OnPropertyChanged(nameof(MaterialWindowEndOffset));

        LoadNextMaterialsPageCommand.NotifyCanExecuteChanged();
        LoadPreviousMaterialsPageCommand.NotifyCanExecuteChanged();
        NotifyMaterialStateChanged();
    }

    private async Task ReloadMixedAsync(CancellationToken cancellationToken)
    {
        int version = Interlocked.Increment(ref _mixedLoadVersion);

        _mixedWindow.Reset();
        _mixedFoldersTotalCount = -1;
        _mixedMaterialsTotalCount = -1;
        MixedCurrentPageOffset = 0;
        MixedTotalCount = 0;
        MixedHasMore = false;
        IsLoadingNextMixedPage = false;
        IsLoadingPreviousMixedPage = false;
        MixedErrorMessage = null;
        MixedContent.Clear();
        SyncMixedWindowState();

        await ShowMixedPageAsync(
            0,
            PageWindowInsert.Append,
            version,
            cancellationToken,
            isInitialPage: true,
            isPreviousPage: false);
    }

    private Task LoadNextMixedWindowAsync(
        int version,
        CancellationToken cancellationToken)
    {
        if (version != _mixedLoadVersion ||
            !_mixedWindow.HasNext ||
            IsLoadingMixed ||
            IsLoadingNextMixedPage ||
            IsLoadingPreviousMixedPage ||
            cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        return ShowMixedPageAsync(
            _mixedWindow.NextOffset,
            PageWindowInsert.Append,
            version,
            cancellationToken,
            isInitialPage: false,
            isPreviousPage: false);
    }

    private Task LoadPreviousMixedWindowAsync(
        int version,
        CancellationToken cancellationToken)
    {
        if (version != _mixedLoadVersion ||
            !_mixedWindow.HasPrevious ||
            IsLoadingMixed ||
            IsLoadingNextMixedPage ||
            IsLoadingPreviousMixedPage ||
            cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        return ShowMixedPageAsync(
            _mixedWindow.PreviousOffset,
            PageWindowInsert.Prepend,
            version,
            cancellationToken,
            isInitialPage: false,
            isPreviousPage: true);
    }

    private async Task ShowMixedPageAsync(
        int offset,
        PageWindowInsert insert,
        int version,
        CancellationToken cancellationToken,
        bool isInitialPage,
        bool isPreviousPage)
    {
        if (version != _mixedLoadVersion || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_mixedWindow.TryGetCachedPage(
                offset,
                out IReadOnlyList<LibraryContentListItemViewModel> cached))
        {
            _mixedWindow.ShowPage(offset, cached, insert);
            RebuildMixedContent();
            SyncMixedWindowState();
            return;
        }

        if (isInitialPage)
        {
            IsLoadingMixed = true;
        }
        else if (isPreviousPage)
        {
            IsLoadingPreviousMixedPage = true;
        }
        else
        {
            IsLoadingNextMixedPage = true;
        }

        MixedErrorMessage = null;

        if (!isInitialPage)
        {
            await YieldForPagingLoaderAsync(cancellationToken);
        }

        try
        {
            (IReadOnlyList<LibraryContentListItemViewModel> items, int totalCount) =
                await LoadMixedPageDataAsync(version, offset, cancellationToken);

            if (version != _mixedLoadVersion || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _mixedWindow.SetTotalCount(totalCount);
            _mixedWindow.ShowPage(offset, items, insert);
            RebuildMixedContent();
            SyncMixedWindowState();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Смена страницы или контекста.
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Не удалось загрузить смешанное содержимое контейнера {ContainerId}, offset {Offset}",
                _containerId,
                offset);

            MixedErrorMessage = exception.Message.StartsWith("Не удалось", StringComparison.Ordinal)
                ? exception.Message
                : "Не удалось загрузить содержимое";
        }
        finally
        {
            if (version == _mixedLoadVersion)
            {
                IsLoadingMixed = false;
                IsLoadingNextMixedPage = false;
                IsLoadingPreviousMixedPage = false;
                SyncMixedWindowState();
            }
        }
    }

    private async Task<(IReadOnlyList<LibraryContentListItemViewModel> Items, int TotalCount)>
        LoadMixedPageDataAsync(
            int version,
            int offset,
            CancellationToken cancellationToken)
    {
        if (_mixedContentFilter == MixedContentFilter.Folders)
        {
            LibraryFoldersPageDto page = await GetFoldersPageForMixedAsync(
                version, offset, MixedPageSize, cancellationToken);

            return (
                page.Items
                    .Select(folder => new LibraryContentListItemViewModel(
                        new LibraryFolderCardViewModel(folder)))
                    .ToArray(),
                page.TotalCount);
        }

        if (_mixedContentFilter is MixedContentFilter.Articles or MixedContentFilter.Questions)
        {
            LibraryMaterialsPageDto page = await GetMaterialsPageForMixedAsync(
                version, offset, MixedPageSize, cancellationToken);

            return (
                page.Items
                    .Select(material => new LibraryContentListItemViewModel(
                        new LibraryMaterialListItemViewModel(material)))
                    .ToArray(),
                page.TotalCount);
        }

        await EnsureMixedTotalsAsync(version, cancellationToken);

        int totalCount = _mixedFoldersTotalCount + _mixedMaterialsTotalCount;
        var result = new List<LibraryContentListItemViewModel>(MixedPageSize);
        int remaining = MixedPageSize;

        if (offset < _mixedFoldersTotalCount)
        {
            int folderTake = Math.Min(remaining, _mixedFoldersTotalCount - offset);
            LibraryFoldersPageDto foldersPage = await GetFoldersPageForMixedAsync(
                version, offset, folderTake, cancellationToken);

            result.AddRange(
                foldersPage.Items.Select(folder =>
                    new LibraryContentListItemViewModel(
                        new LibraryFolderCardViewModel(folder))));

            remaining -= foldersPage.Items.Count;
        }

        if (remaining > 0)
        {
            int materialOffset = Math.Max(0, offset + result.Count - _mixedFoldersTotalCount);

            if (materialOffset < _mixedMaterialsTotalCount)
            {
                LibraryMaterialsPageDto materialsPage = await GetMaterialsPageForMixedAsync(
                    version, materialOffset, remaining, cancellationToken);

                result.AddRange(
                    materialsPage.Items.Select(material =>
                        new LibraryContentListItemViewModel(
                            new LibraryMaterialListItemViewModel(material))));
            }
        }

        return (result, totalCount);
    }

    private async Task EnsureMixedTotalsAsync(
        int version,
        CancellationToken cancellationToken)
    {
        if (_mixedFoldersTotalCount >= 0 && _mixedMaterialsTotalCount >= 0)
        {
            return;
        }

        Task<LibraryFoldersPageDto> foldersTask = GetFoldersPageForMixedAsync(
            version, 0, 1, cancellationToken);
        Task<LibraryMaterialsPageDto> materialsTask = GetMaterialsPageForMixedAsync(
            version, 0, 1, cancellationToken);

        await Task.WhenAll(foldersTask, materialsTask);

        _mixedFoldersTotalCount = (await foldersTask).TotalCount;
        _mixedMaterialsTotalCount = (await materialsTask).TotalCount;
    }

    private async Task<LibraryFoldersPageDto> GetFoldersPageForMixedAsync(
        int version,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var result = await _queryDispatcher.SendAsync<
            GetLibraryFoldersPageQuery,
            LibraryFoldersPageDto>(
            new GetLibraryFoldersPageQuery(
                _containerId,
                SearchText,
                SelectedSortOption.FolderSort,
                offset,
                Math.Max(1, limit)),
            cancellationToken);

        if (version != _mixedLoadVersion || cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                result.Error.FirstOrDefault()?.Message ?? "Не удалось загрузить папки");
        }

        return result.Value;
    }

    private async Task<LibraryMaterialsPageDto> GetMaterialsPageForMixedAsync(
        int version,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var result = await _queryDispatcher.SendAsync<
            GetLibraryMaterialsPageQuery,
            LibraryMaterialsPageDto>(
            new GetLibraryMaterialsPageQuery(
                _containerId,
                SearchText,
                GetMixedMaterialFilter(),
                SelectedSortOption.MaterialSort,
                offset,
                Math.Max(1, limit)),
            cancellationToken);

        if (version != _mixedLoadVersion || cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                result.Error.FirstOrDefault()?.Message ?? "Не удалось загрузить материалы");
        }

        return result.Value;
    }

    private LibraryMaterialFilter GetMixedMaterialFilter() =>
        _mixedContentFilter switch
        {
            MixedContentFilter.Articles => LibraryMaterialFilter.Articles,
            MixedContentFilter.Questions => LibraryMaterialFilter.Questions,
            _ => LibraryMaterialFilter.All,
        };

    private void RebuildMixedContent()
    {
        MixedContent.Clear();

        foreach (int offset in _mixedWindow.VisibleOffsets)
        {
            if (!_mixedWindow.TryGetCachedPage(
                    offset,
                    out IReadOnlyList<LibraryContentListItemViewModel> page))
            {
                continue;
            }

            foreach (LibraryContentListItemViewModel item in page)
            {
                MixedContent.Add(item);
            }
        }
    }

    private void SyncMixedWindowState()
    {
        MixedTotalCount = _mixedWindow.TotalCount;
        MixedCurrentPageOffset = _mixedWindow.CurrentPageOffset;
        MixedHasMore = _mixedWindow.HasNext;

        OnPropertyChanged(nameof(MixedHasPrevious));
        OnPropertyChanged(nameof(IsMixedPaging));
        OnPropertyChanged(nameof(MixedWindowStartOffset));
        OnPropertyChanged(nameof(MixedWindowEndOffset));

        LoadNextMixedPageCommand.NotifyCanExecuteChanged();
        LoadPreviousMixedPageCommand.NotifyCanExecuteChanged();
        NotifyMixedStateChanged();
    }

    private bool CanLoadNextFoldersPage() =>
        _isLoaded &&
        _containerId != Guid.Empty &&
        _folderWindow.HasNext &&
        !IsLoadingFolders &&
        !IsLoadingNextFoldersPage &&
        !IsLoadingPreviousFoldersPage &&
        !HasFoldersError;

    private bool CanLoadPreviousFoldersPage() =>
        _isLoaded &&
        _containerId != Guid.Empty &&
        _folderWindow.HasPrevious &&
        !IsLoadingFolders &&
        !IsLoadingNextFoldersPage &&
        !IsLoadingPreviousFoldersPage &&
        !HasFoldersError;

    private bool CanLoadNextMaterialsPage() =>
        _isLoaded &&
        _containerId != Guid.Empty &&
        _materialWindow.HasNext &&
        !IsLoadingMaterials &&
        !IsLoadingNextMaterialsPage &&
        !IsLoadingPreviousMaterialsPage &&
        !HasMaterialsError;

    private bool CanLoadPreviousMaterialsPage() =>
        _isLoaded &&
        _containerId != Guid.Empty &&
        _materialWindow.HasPrevious &&
        !IsLoadingMaterials &&
        !IsLoadingNextMaterialsPage &&
        !IsLoadingPreviousMaterialsPage &&
        !HasMaterialsError;

    private bool CanLoadNextMixedPage() =>
        _isLoaded &&
        _containerId != Guid.Empty &&
        IsMixedContent &&
        _mixedWindow.HasNext &&
        !IsLoadingMixed &&
        !IsLoadingNextMixedPage &&
        !IsLoadingPreviousMixedPage &&
        !HasMixedError;

    private bool CanLoadPreviousMixedPage() =>
        _isLoaded &&
        _containerId != Guid.Empty &&
        IsMixedContent &&
        _mixedWindow.HasPrevious &&
        !IsLoadingMixed &&
        !IsLoadingNextMixedPage &&
        !IsLoadingPreviousMixedPage &&
        !HasMixedError;

    private async Task LoadNextFoldersPageWithLinkedCancellationAsync(
        CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                _viewCancellationToken,
                cancellationToken);

        await LoadNextFoldersWindowAsync(
            _foldersLoadVersion,
            linkedCancellationTokenSource.Token);
    }

    private async Task LoadPreviousFoldersPageWithLinkedCancellationAsync(
        CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                _viewCancellationToken,
                cancellationToken);

        await LoadPreviousFoldersWindowAsync(
            _foldersLoadVersion,
            linkedCancellationTokenSource.Token);
    }

    private async Task LoadNextMaterialsPageWithLinkedCancellationAsync(
        CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                _viewCancellationToken,
                cancellationToken);

        await LoadNextMaterialsWindowAsync(
            _materialsLoadVersion,
            linkedCancellationTokenSource.Token);
    }

    private async Task LoadPreviousMaterialsPageWithLinkedCancellationAsync(
        CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                _viewCancellationToken,
                cancellationToken);

        await LoadPreviousMaterialsWindowAsync(
            _materialsLoadVersion,
            linkedCancellationTokenSource.Token);
    }

    private async Task LoadNextMixedPageWithLinkedCancellationAsync(
        CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                _viewCancellationToken,
                cancellationToken);

        await LoadNextMixedWindowAsync(
            _mixedLoadVersion,
            linkedCancellationTokenSource.Token);
    }

    private async Task LoadPreviousMixedPageWithLinkedCancellationAsync(
        CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                _viewCancellationToken,
                cancellationToken);

        await LoadPreviousMixedWindowAsync(
            _mixedLoadVersion,
            linkedCancellationTokenSource.Token);
    }

    private async Task ReloadAfterSearchDelayAsync(int searchVersion)
    {
        try
        {
            await Task.Delay(SearchDelay, _viewCancellationToken);

            if (searchVersion == Volatile.Read(ref _searchVersion))
            {
                await ReloadAvailableContentAsync(_viewCancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (_viewCancellationToken.IsCancellationRequested)
        {
            // Закрытие страницы.
        }
    }

    private async Task ReloadCollectionsFromSelectionChangeAsync()
    {
        try
        {
            await ReloadAvailableContentAsync(_viewCancellationToken);
        }
        catch (OperationCanceledException)
            when (_viewCancellationToken.IsCancellationRequested)
        {
            // Закрытие страницы.
        }
    }

    private async Task ReloadMaterialsFromSelectionChangeAsync()
    {
        try
        {
            await ReloadMaterialsAsync(_viewCancellationToken);
        }
        catch (OperationCanceledException)
            when (_viewCancellationToken.IsCancellationRequested)
        {
            // Закрытие страницы.
        }
    }

    private async Task ReloadMixedFromSelectionChangeAsync()
    {
        try
        {
            await ReloadMixedAsync(_viewCancellationToken);
        }
        catch (OperationCanceledException)
            when (_viewCancellationToken.IsCancellationRequested)
        {
            // Закрытие страницы.
        }
    }

    private void NavigateToContainer(Guid containerId)
    {
        _pageNavigationService.NavigateTo<LibraryContainerViewModel>(
            viewModel => viewModel.Initialize(containerId));
    }

    private async Task EnsureViewModeLoadedAsync(
        CancellationToken cancellationToken)
    {
        if (_isViewModeLoaded)
        {
            return;
        }

        try
        {
            AppSettings settings =
                await _settingsService.LoadAsync(cancellationToken);

            ViewMode = settings.LibraryTopicsViewMode;
            SelectedTilesPerRowOption =
                LibraryTilesPerRowOptions.Resolve(
                    settings.LibraryTilesPerRow);
            FoldersPaneRatio = NormalizeFoldersPaneRatio(
                settings.LibraryContainerFoldersPaneRatio);
            _isViewModeLoaded = true;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Не удалось загрузить режим просмотра содержимого библиотеки");

            ViewMode = LibraryTopicsViewMode.CompactTiles;
            FoldersPaneRatio = DefaultFoldersPaneRatio;
            _isViewModeLoaded = true;
        }
    }

    private async Task SetViewModeAsync(
        LibraryTopicsViewMode viewMode,
        CancellationToken cancellationToken)
    {
        if (ViewMode == viewMode)
        {
            return;
        }

        ViewMode = viewMode;

        try
        {
            await _settingsService.SaveLibraryTopicsViewModeAsync(
                viewMode,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Не удалось сохранить режим просмотра содержимого библиотеки {ViewMode}",
                viewMode);
        }
    }

    private async Task SaveTilesPerRowAsync(int? tilesPerRow)
    {
        try
        {
            await _settingsService.SaveLibraryTilesPerRowAsync(
                tilesPerRow,
                _viewCancellationToken);
        }
        catch (OperationCanceledException)
            when (_viewCancellationToken.IsCancellationRequested)
        {
            // Закрытие страницы.
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Не удалось сохранить количество плиток в строке {TilesPerRow}",
                tilesPerRow);
        }
    }

    public async Task SaveFoldersPaneRatioAsync(double foldersPaneRatio)
    {
        double normalizedRatio = NormalizeFoldersPaneRatio(foldersPaneRatio);

        if (Math.Abs(FoldersPaneRatio - normalizedRatio) < 0.001)
        {
            return;
        }

        FoldersPaneRatio = normalizedRatio;

        try
        {
            await _settingsService.SaveLibraryContainerFoldersPaneRatioAsync(
                normalizedRatio,
                _viewCancellationToken);
        }
        catch (OperationCanceledException)
            when (_viewCancellationToken.IsCancellationRequested)
        {
            // Закрытие страницы.
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Не удалось сохранить положение разделителя папок и материалов {FoldersPaneRatio}",
                normalizedRatio);
        }
    }

    private static double NormalizeFoldersPaneRatio(double foldersPaneRatio)
    {
        if (!double.IsFinite(foldersPaneRatio))
        {
            return DefaultFoldersPaneRatio;
        }

        return Math.Clamp(
            foldersPaneRatio,
            MinFoldersPaneRatio,
            MaxFoldersPaneRatio);
    }

    private void NotifyFolderStateChanged()
    {
        OnPropertyChanged(nameof(HasFolders));
        OnPropertyChanged(nameof(IsFoldersEmpty));
    }

    private void NotifyMaterialStateChanged()
    {
        OnPropertyChanged(nameof(HasMaterials));
        OnPropertyChanged(nameof(IsMaterialsEmpty));
    }

    private void NotifyMixedStateChanged()
    {
        OnPropertyChanged(nameof(HasMixedItems));
        OnPropertyChanged(nameof(IsMixedEmpty));
        OnPropertyChanged(nameof(MixedShownCountText));
    }

}

public sealed record LibraryContainerSortOption(
    string Name,
    LibraryFolderSort FolderSort,
    LibraryMaterialSort MaterialSort);
