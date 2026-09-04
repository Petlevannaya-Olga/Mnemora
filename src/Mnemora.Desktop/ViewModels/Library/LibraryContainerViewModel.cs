using System.Collections.ObjectModel;
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
    private const int MaterialPageSize = 50;
    private const int MixedPageSize = 50;
    private const double DefaultFoldersPaneRatio = 1d / 3d;
    private const double MinFoldersPaneRatio = 0.1;
    private const double MaxFoldersPaneRatio = 0.9;
    private static readonly TimeSpan SearchDelay =
        TimeSpan.FromMilliseconds(350);

    private readonly IQueryDispatcher _queryDispatcher;
    private readonly IPageNavigationService _pageNavigationService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<LibraryContainerViewModel> _logger;

    private CancellationToken _viewCancellationToken;
    private Guid _containerId;
    private int _foldersNextOffset;
    private int _materialsNextOffset;
    private int _foldersLoadVersion;
    private int _materialsLoadVersion;
    private int _searchVersion;
    private int _mixedLoadVersion;
    private int _mixedFoldersNextOffset;
    private int _mixedMaterialsNextOffset;
    private bool _mixedFoldersHasMore;
    private bool _mixedMaterialsHasMore;
    private MixedContentFilter _mixedContentFilter = MixedContentFilter.All;
    private readonly Queue<LibraryContentListItemViewModel> _mixedMaterialBuffer = new();
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
    [NotifyCanExecuteChangedFor(nameof(LoadNextFoldersPageCommand))]
    private bool _isLoadingFolders;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadNextFoldersPageCommand))]
    private bool _isLoadingNextFoldersPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMaterialsEmpty))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextMaterialsPageCommand))]
    private bool _isLoadingMaterials;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadNextMaterialsPageCommand))]
    private bool _isLoadingNextMaterialsPage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadNextFoldersPageCommand))]
    private bool _foldersHasMore;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadNextMaterialsPageCommand))]
    private bool _materialsHasMore;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMixedEmpty))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextMixedPageCommand))]
    private bool _isLoadingMixed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadNextMixedPageCommand))]
    private bool _isLoadingNextMixedPage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadNextMixedPageCommand))]
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
                !string.IsNullOrWhiteSpace(SearchText));
        }
    }

    public string MixedShownCountText
    {
        get
        {
            int visibleCount = Math.Min(
                MixedPageSize,
                Math.Max(0, MixedTotalCount - MixedCurrentPageOffset));

            return LibraryRangeTextFormatter.Format(
                MixedCurrentPageOffset,
                visibleCount,
                MixedTotalCount,
                !string.IsNullOrWhiteSpace(SearchText) || !IsMixedAllFilter);
        }
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

    [RelayCommand(CanExecute = nameof(CanLoadNextMaterialsPage))]
    private Task LoadNextMaterialsPageAsync(CancellationToken cancellationToken)
    {
        return LoadNextMaterialsPageWithLinkedCancellationAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanLoadNextMixedPage))]
    private Task LoadNextMixedPageAsync(CancellationToken cancellationToken)
    {
        return LoadNextMixedPageWithLinkedCancellationAsync(cancellationToken);
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
        _foldersNextOffset = 0;
        FoldersCurrentPageOffset = 0;
        FoldersTotalCount = 0;
        FoldersHasMore = false;
        IsLoadingFolders = false;
        IsLoadingNextFoldersPage = false;
        FoldersErrorMessage = null;
        Folders.Clear();
        NotifyFolderStateChanged();
    }

    private void ResetMaterials()
    {
        Interlocked.Increment(ref _materialsLoadVersion);
        _materialsNextOffset = 0;
        MaterialsCurrentPageOffset = 0;
        MaterialsTotalCount = 0;
        MaterialsHasMore = false;
        IsLoadingMaterials = false;
        IsLoadingNextMaterialsPage = false;
        MaterialsErrorMessage = null;
        Materials.Clear();
        NotifyMaterialStateChanged();
    }

    private void ResetMixed()
    {
        Interlocked.Increment(ref _mixedLoadVersion);
        _mixedFoldersNextOffset = 0;
        _mixedMaterialsNextOffset = 0;
        _mixedFoldersHasMore = false;
        _mixedMaterialsHasMore = false;
        _mixedMaterialBuffer.Clear();
        MixedCurrentPageOffset = 0;
        MixedTotalCount = 0;
        MixedHasMore = false;
        IsLoadingMixed = false;
        IsLoadingNextMixedPage = false;
        MixedErrorMessage = null;
        MixedContent.Clear();
        NotifyMixedStateChanged();
    }

    private async Task ReloadFoldersAsync(CancellationToken cancellationToken)
    {
        int version = Interlocked.Increment(ref _foldersLoadVersion);

        _foldersNextOffset = 0;
        FoldersCurrentPageOffset = 0;
        FoldersTotalCount = 0;
        FoldersHasMore = true;
        IsLoadingNextFoldersPage = false;
        FoldersErrorMessage = null;
        Folders.Clear();
        NotifyFolderStateChanged();

        await LoadFoldersPageAsync(version, cancellationToken);
    }

    private async Task LoadFoldersPageAsync(
        int version,
        CancellationToken cancellationToken)
    {
        if (version != _foldersLoadVersion ||
            !FoldersHasMore ||
            IsLoadingNextFoldersPage ||
            cancellationToken.IsCancellationRequested)
        {
            return;
        }

        int requestedOffset = _foldersNextOffset;
        bool isInitialPage = requestedOffset == 0;

        if (isInitialPage)
        {
            IsLoadingFolders = true;
        }
        else
        {
            IsLoadingNextFoldersPage = true;
        }

        try
        {
            var query = new GetLibraryFoldersPageQuery(
                _containerId,
                SearchText,
                SelectedSortOption.FolderSort,
                requestedOffset,
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

            foreach (LibraryFolderDto folder in result.Value.Items)
            {
                Folders.Add(new LibraryFolderCardViewModel(folder));
            }

            _foldersNextOffset = result.Value.NextOffset;
            FoldersHasMore = result.Value.HasMore;
            FoldersTotalCount = result.Value.TotalCount;
            FoldersCurrentPageOffset = requestedOffset;
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
                "Не удалось загрузить папки контейнера {ContainerId}",
                _containerId);

            FoldersErrorMessage = "Не удалось загрузить папки";
        }
        finally
        {
            if (version == _foldersLoadVersion)
            {
                IsLoadingFolders = false;
                IsLoadingNextFoldersPage = false;
                LoadNextFoldersPageCommand.NotifyCanExecuteChanged();
                NotifyFolderStateChanged();
            }
        }
    }

    private async Task ReloadMaterialsAsync(CancellationToken cancellationToken)
    {
        int version = Interlocked.Increment(ref _materialsLoadVersion);

        _materialsNextOffset = 0;
        MaterialsCurrentPageOffset = 0;
        MaterialsTotalCount = 0;
        MaterialsHasMore = true;
        IsLoadingNextMaterialsPage = false;
        MaterialsErrorMessage = null;
        Materials.Clear();
        NotifyMaterialStateChanged();

        await LoadMaterialsPageAsync(version, cancellationToken);
    }

    private async Task LoadMaterialsPageAsync(
        int version,
        CancellationToken cancellationToken)
    {
        if (version != _materialsLoadVersion ||
            !MaterialsHasMore ||
            IsLoadingNextMaterialsPage ||
            cancellationToken.IsCancellationRequested)
        {
            return;
        }

        int requestedOffset = _materialsNextOffset;
        bool isInitialPage = requestedOffset == 0;

        if (isInitialPage)
        {
            IsLoadingMaterials = true;
        }
        else
        {
            IsLoadingNextMaterialsPage = true;
        }

        try
        {
            var query = new GetLibraryMaterialsPageQuery(
                _containerId,
                SearchText,
                SelectedFilterOption.Filter,
                SelectedSortOption.MaterialSort,
                requestedOffset,
                MaterialPageSize);

            var result = await _queryDispatcher.SendAsync<
                GetLibraryMaterialsPageQuery,
                LibraryMaterialsPageDto>(
                query,
                cancellationToken);

            if (version != _materialsLoadVersion ||
                cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (result.IsFailure)
            {
                MaterialsErrorMessage = result.Error.FirstOrDefault()?.Message
                                        ?? "Не удалось загрузить материалы";
                return;
            }

            foreach (LibraryMaterialDto material in result.Value.Items)
            {
                Materials.Add(new LibraryMaterialListItemViewModel(material));
            }

            _materialsNextOffset = result.Value.NextOffset;
            MaterialsHasMore = result.Value.HasMore;
            MaterialsTotalCount = result.Value.TotalCount;
            MaterialsCurrentPageOffset = requestedOffset;

            _logger.LogInformation(
                "Материалы контейнера {ContainerId}: загружено {ItemsCount}, всего {TotalCount}, offset {Offset}",
                _containerId,
                result.Value.Items.Count,
                result.Value.TotalCount,
                requestedOffset);
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
                "Не удалось загрузить материалы контейнера {ContainerId}",
                _containerId);

            MaterialsErrorMessage = "Не удалось загрузить материалы";
        }
        finally
        {
            if (version == _materialsLoadVersion)
            {
                IsLoadingMaterials = false;
                IsLoadingNextMaterialsPage = false;
                LoadNextMaterialsPageCommand.NotifyCanExecuteChanged();
                NotifyMaterialStateChanged();
            }
        }
    }

    private async Task ReloadMixedAsync(CancellationToken cancellationToken)
    {
        int version = Interlocked.Increment(ref _mixedLoadVersion);

        bool includeFolders =
            _mixedContentFilter is MixedContentFilter.All or MixedContentFilter.Folders;
        bool includeMaterials =
            _mixedContentFilter is MixedContentFilter.All or MixedContentFilter.Articles or MixedContentFilter.Questions;

        _mixedFoldersNextOffset = 0;
        _mixedMaterialsNextOffset = 0;
        _mixedFoldersHasMore = includeFolders;
        _mixedMaterialsHasMore = includeMaterials;
        _mixedMaterialBuffer.Clear();
        MixedCurrentPageOffset = 0;
        MixedTotalCount = 0;
        MixedHasMore = includeFolders || includeMaterials;
        IsLoadingNextMixedPage = false;
        MixedErrorMessage = null;
        MixedContent.Clear();
        NotifyMixedStateChanged();

        IsLoadingMixed = true;

        try
        {
            if (_mixedContentFilter == MixedContentFilter.Folders)
            {
                var foldersResult = await _queryDispatcher.SendAsync<
                    GetLibraryFoldersPageQuery,
                    LibraryFoldersPageDto>(
                    new GetLibraryFoldersPageQuery(
                        _containerId,
                        SearchText,
                        SelectedSortOption.FolderSort,
                        0,
                        MixedPageSize),
                    cancellationToken);

                if (version != _mixedLoadVersion || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (foldersResult.IsFailure)
                {
                    MixedErrorMessage = foldersResult.Error.FirstOrDefault()?.Message
                                        ?? "Не удалось загрузить папки";
                    return;
                }

                LibraryFoldersPageDto page = foldersResult.Value;
                MixedTotalCount = page.TotalCount;
                _mixedFoldersNextOffset = page.NextOffset;
                _mixedFoldersHasMore = page.HasMore;
                _mixedMaterialsHasMore = false;

                foreach (LibraryFolderDto folder in page.Items)
                {
                    MixedContent.Add(
                        new LibraryContentListItemViewModel(
                            new LibraryFolderCardViewModel(folder)));
                }

                UpdateMixedHasMore();
                return;
            }

            if (_mixedContentFilter is MixedContentFilter.Articles or MixedContentFilter.Questions)
            {
                var materialsResult = await _queryDispatcher.SendAsync<
                    GetLibraryMaterialsPageQuery,
                    LibraryMaterialsPageDto>(
                    new GetLibraryMaterialsPageQuery(
                        _containerId,
                        SearchText,
                        GetMixedMaterialFilter(),
                        SelectedSortOption.MaterialSort,
                        0,
                        MixedPageSize),
                    cancellationToken);

                if (version != _mixedLoadVersion || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (materialsResult.IsFailure)
                {
                    MixedErrorMessage = materialsResult.Error.FirstOrDefault()?.Message
                                        ?? "Не удалось загрузить материалы";
                    return;
                }

                LibraryMaterialsPageDto page = materialsResult.Value;
                MixedTotalCount = page.TotalCount;
                _mixedFoldersHasMore = false;
                _mixedMaterialsNextOffset = page.NextOffset;
                _mixedMaterialsHasMore = page.HasMore;

                foreach (LibraryMaterialDto material in page.Items)
                {
                    MixedContent.Add(
                        new LibraryContentListItemViewModel(
                            new LibraryMaterialListItemViewModel(material)));
                }

                UpdateMixedHasMore();
                return;
            }

            var foldersTask = _queryDispatcher.SendAsync<
                GetLibraryFoldersPageQuery,
                LibraryFoldersPageDto>(
                new GetLibraryFoldersPageQuery(
                    _containerId,
                    SearchText,
                    SelectedSortOption.FolderSort,
                    0,
                    MixedPageSize),
                cancellationToken);

            var materialsTask = _queryDispatcher.SendAsync<
                GetLibraryMaterialsPageQuery,
                LibraryMaterialsPageDto>(
                new GetLibraryMaterialsPageQuery(
                    _containerId,
                    SearchText,
                    LibraryMaterialFilter.All,
                    SelectedSortOption.MaterialSort,
                    0,
                    1),
                cancellationToken);

            await Task.WhenAll(foldersTask, materialsTask);

            if (version != _mixedLoadVersion || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var foldersResultAll = await foldersTask;
            var materialsResultAll = await materialsTask;

            if (foldersResultAll.IsFailure || materialsResultAll.IsFailure)
            {
                MixedErrorMessage = foldersResultAll.IsFailure
                    ? foldersResultAll.Error.FirstOrDefault()?.Message ?? "Не удалось загрузить папки"
                    : materialsResultAll.Error.FirstOrDefault()?.Message ?? "Не удалось загрузить материалы";
                return;
            }

            LibraryFoldersPageDto foldersPage = foldersResultAll.Value;
            LibraryMaterialsPageDto materialsPage = materialsResultAll.Value;

            MixedTotalCount = foldersPage.TotalCount + materialsPage.TotalCount;
            _mixedFoldersNextOffset = foldersPage.NextOffset;
            _mixedFoldersHasMore = foldersPage.HasMore;
            _mixedMaterialsNextOffset = materialsPage.NextOffset;
            _mixedMaterialsHasMore = materialsPage.HasMore;

            foreach (LibraryMaterialDto material in materialsPage.Items)
            {
                _mixedMaterialBuffer.Enqueue(
                    new LibraryContentListItemViewModel(
                        new LibraryMaterialListItemViewModel(material)));
            }

            int added = 0;

            foreach (LibraryFolderDto folder in foldersPage.Items)
            {
                MixedContent.Add(
                    new LibraryContentListItemViewModel(
                        new LibraryFolderCardViewModel(folder)));
                added++;
            }

            if (!_mixedFoldersHasMore && added < MixedPageSize)
            {
                added += AppendBufferedMixedMaterials(MixedPageSize - added);

                if (added < MixedPageSize && _mixedMaterialsHasMore)
                {
                    added += await LoadMixedMaterialsChunkAsync(
                        version,
                        MixedPageSize - added,
                        cancellationToken);
                }
            }

            MixedCurrentPageOffset = 0;
            UpdateMixedHasMore();
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
                "Не удалось загрузить смешанное содержимое контейнера {ContainerId}",
                _containerId);

            MixedErrorMessage = "Не удалось загрузить содержимое";
        }
        finally
        {
            if (version == _mixedLoadVersion)
            {
                IsLoadingMixed = false;
                IsLoadingNextMixedPage = false;
                LoadNextMixedPageCommand.NotifyCanExecuteChanged();
                NotifyMixedStateChanged();
            }
        }
    }

    private async Task LoadMixedPageAsync(
        int version,
        CancellationToken cancellationToken)
    {
        if (version != _mixedLoadVersion ||
            !MixedHasMore ||
            IsLoadingMixed ||
            IsLoadingNextMixedPage ||
            cancellationToken.IsCancellationRequested)
        {
            return;
        }

        int requestedOffset = MixedContent.Count;
        int remaining = MixedPageSize;
        IsLoadingNextMixedPage = true;
        MixedErrorMessage = null;

        try
        {
            if (_mixedFoldersHasMore)
            {
                var result = await _queryDispatcher.SendAsync<
                    GetLibraryFoldersPageQuery,
                    LibraryFoldersPageDto>(
                    new GetLibraryFoldersPageQuery(
                        _containerId,
                        SearchText,
                        SelectedSortOption.FolderSort,
                        _mixedFoldersNextOffset,
                        remaining),
                    cancellationToken);

                if (version != _mixedLoadVersion || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (result.IsFailure)
                {
                    MixedErrorMessage = result.Error.FirstOrDefault()?.Message
                                        ?? "Не удалось загрузить папки";
                    return;
                }

                foreach (LibraryFolderDto folder in result.Value.Items)
                {
                    MixedContent.Add(
                        new LibraryContentListItemViewModel(
                            new LibraryFolderCardViewModel(folder)));
                    remaining--;
                }

                _mixedFoldersNextOffset = result.Value.NextOffset;
                _mixedFoldersHasMore = result.Value.HasMore;
            }

            if (!_mixedFoldersHasMore && remaining > 0)
            {
                remaining -= AppendBufferedMixedMaterials(remaining);

                if (remaining > 0 && _mixedMaterialsHasMore)
                {
                    int loaded = await LoadMixedMaterialsChunkAsync(
                        version,
                        remaining,
                        cancellationToken);
                    remaining -= loaded;
                }
            }

            MixedCurrentPageOffset = requestedOffset;
            UpdateMixedHasMore();
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
                "Не удалось догрузить смешанное содержимое контейнера {ContainerId}",
                _containerId);

            MixedErrorMessage = "Не удалось загрузить содержимое";
        }
        finally
        {
            if (version == _mixedLoadVersion)
            {
                IsLoadingNextMixedPage = false;
                LoadNextMixedPageCommand.NotifyCanExecuteChanged();
                NotifyMixedStateChanged();
            }
        }
    }

    private async Task<int> LoadMixedMaterialsChunkAsync(
        int version,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0 || !_mixedMaterialsHasMore)
        {
            return 0;
        }

        var result = await _queryDispatcher.SendAsync<
            GetLibraryMaterialsPageQuery,
            LibraryMaterialsPageDto>(
            new GetLibraryMaterialsPageQuery(
                _containerId,
                SearchText,
                GetMixedMaterialFilter(),
                SelectedSortOption.MaterialSort,
                _mixedMaterialsNextOffset,
                limit),
            cancellationToken);

        if (version != _mixedLoadVersion || cancellationToken.IsCancellationRequested)
        {
            return 0;
        }

        if (result.IsFailure)
        {
            MixedErrorMessage = result.Error.FirstOrDefault()?.Message
                                ?? "Не удалось загрузить материалы";
            _mixedMaterialsHasMore = false;
            return 0;
        }

        foreach (LibraryMaterialDto material in result.Value.Items)
        {
            MixedContent.Add(
                new LibraryContentListItemViewModel(
                    new LibraryMaterialListItemViewModel(material)));
        }

        _mixedMaterialsNextOffset = result.Value.NextOffset;
        _mixedMaterialsHasMore = result.Value.HasMore;
        return result.Value.Items.Count;
    }

    private LibraryMaterialFilter GetMixedMaterialFilter() =>
        _mixedContentFilter switch
        {
            MixedContentFilter.Articles => LibraryMaterialFilter.Articles,
            MixedContentFilter.Questions => LibraryMaterialFilter.Questions,
            _ => LibraryMaterialFilter.All,
        };

    private int AppendBufferedMixedMaterials(int limit)
    {
        int added = 0;

        while (added < limit && _mixedMaterialBuffer.Count > 0)
        {
            MixedContent.Add(_mixedMaterialBuffer.Dequeue());
            added++;
        }

        return added;
    }

    private void UpdateMixedHasMore()
    {
        MixedHasMore =
            _mixedFoldersHasMore ||
            _mixedMaterialBuffer.Count > 0 ||
            _mixedMaterialsHasMore;
    }

    private bool CanLoadNextFoldersPage() =>
        _isLoaded &&
        _containerId != Guid.Empty &&
        FoldersHasMore &&
        !IsLoadingFolders &&
        !IsLoadingNextFoldersPage &&
        !HasFoldersError;

    private bool CanLoadNextMaterialsPage() =>
        _isLoaded &&
        _containerId != Guid.Empty &&
        MaterialsHasMore &&
        !IsLoadingMaterials &&
        !IsLoadingNextMaterialsPage &&
        !HasMaterialsError;

    private bool CanLoadNextMixedPage() =>
        _isLoaded &&
        _containerId != Guid.Empty &&
        IsMixedContent &&
        MixedHasMore &&
        !IsLoadingMixed &&
        !IsLoadingNextMixedPage &&
        !HasMixedError;

    private async Task LoadNextFoldersPageWithLinkedCancellationAsync(
        CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                _viewCancellationToken,
                cancellationToken);

        await LoadFoldersPageAsync(
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

        await LoadMaterialsPageAsync(
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

        await LoadMixedPageAsync(
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
