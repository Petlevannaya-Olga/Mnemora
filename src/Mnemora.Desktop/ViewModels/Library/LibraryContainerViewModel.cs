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
    private const int FolderPageSize = LibraryPagingDefaults.PageSize;
    private const int MaterialPageSize = 50;
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
    [NotifyPropertyChangedFor(nameof(FoldersEmptyMessage))]
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
    private LibraryTopicsViewMode _viewMode = LibraryTopicsViewMode.CompactTiles;

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
    public bool HasFolders => Folders.Count > 0;
    public bool HasMaterials => Materials.Count > 0;
    public bool HasFolderContent => Contents?.FoldersCount > 0;
    public bool HasMaterialContent => Contents?.MaterialsCount > 0;
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

    public bool IsAllFilter =>
        SelectedFilterOption.Filter == LibraryMaterialFilter.All;

    public bool IsArticlesFilter =>
        SelectedFilterOption.Filter == LibraryMaterialFilter.Articles;

    public bool IsQuestionsFilter =>
        SelectedFilterOption.Filter == LibraryMaterialFilter.Questions;

    public bool IsTableView => ViewMode == LibraryTopicsViewMode.Table;
    public bool IsTilesView => ViewMode == LibraryTopicsViewMode.Tiles;
    public bool IsCompactTilesView => ViewMode == LibraryTopicsViewMode.CompactTiles;

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

        if (_isLoaded && HasMaterialContent)
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
            await BuildBreadcrumbsAsync(result.Value.Container, cancellationToken);
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
        LibraryContainerHeaderDto current,
        CancellationToken cancellationToken)
    {
        var path = new List<LibraryContainerHeaderDto> { current };
        var visited = new HashSet<Guid> { current.Id };
        Guid? parentId = current.ParentId;

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

            LibraryContainerHeaderDto parent = result.Value.Container;
            path.Add(parent);
            parentId = parent.ParentId;
        }

        path.Reverse();
        Breadcrumbs.Clear();

        foreach (LibraryContainerHeaderDto item in path)
        {
            Breadcrumbs.Add(
                new LibraryBreadcrumbItemViewModel(
                    item.Id,
                    item.Name,
                    item.Depth,
                    item.Id == current.Id));
        }
    }

    private async Task ReloadAvailableContentAsync(CancellationToken cancellationToken)
    {
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
}

public sealed record LibraryContainerSortOption(
    string Name,
    LibraryFolderSort FolderSort,
    LibraryMaterialSort MaterialSort);
