using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Library.GetSectionRoot;
using Mnemora.Application.Library.GetSectionsPage;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class LibraryOverviewViewModel : ViewModelBase
{
    private const int PageSize = LibraryPagingDefaults.PageSize;
    private const int VisiblePageLimit = 7;
    private const int CachePageLimit = 10;
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(350);

    private readonly IQueryDispatcher _queryDispatcher;
    private readonly ISettingsService _settingsService;
    private readonly IPageNavigationService _pageNavigationService;
    private readonly ILogger<LibraryOverviewViewModel> _logger;
    private readonly BoundedPagedWindow<LibrarySectionOverviewDto> _sectionWindow =
        new(PageSize, VisiblePageLimit, CachePageLimit);

    private CancellationToken _viewCancellationToken;
    private int _loadVersion;
    private int _searchVersion;
    private bool _isLoaded;
    private bool _isViewModeLoaded;

    public ObservableCollection<LibrarySectionRowViewModel> CompactSectionRows { get; } = [];

    public LibraryOverviewViewModel(
        IQueryDispatcher queryDispatcher,
        IPageNavigationService pageNavigationService,
        ISettingsService settingsService,
        ILogger<LibraryOverviewViewModel> logger)
    {
        _queryDispatcher = queryDispatcher;
        _pageNavigationService = pageNavigationService;
        _settingsService = settingsService;
        _logger = logger;

        SortOptions =
        [
            new("Последняя активность", LibrarySectionSort.RecentActivity),
            new("По названию", LibrarySectionSort.Name),
            new("Сначала новые", LibrarySectionSort.Newest)
        ];

        _selectedSortOption = SortOptions[0];
    }

    public ObservableCollection<LibrarySectionCardViewModel> Sections { get; } = [];

    public ObservableCollection<LibrarySectionRowViewModel> SectionRows { get; } = [];

    public IReadOnlyList<LibrarySectionSortOption> SortOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSearchResults))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousPageCommand))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPaging))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousPageCommand))]
    private bool _isLoadingNextPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPaging))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousPageCommand))]
    private bool _isLoadingPreviousPage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadNextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousPageCommand))]
    private bool _hasMore = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSearchResults))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadPreviousPageCommand))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNextPageError))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextPageCommand))]
    private string? _nextPageErrorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSearchResults))]
    private string? _searchText;

    [ObservableProperty] private LibrarySectionSortOption _selectedSortOption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SectionsShownCountText))]
    private int _totalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SectionsShownCountText))]
    private int _currentPageOffset;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTableView))]
    [NotifyPropertyChangedFor(nameof(IsTilesView))]
    [NotifyPropertyChangedFor(nameof(IsCompactTilesView))]
    [NotifyPropertyChangedFor(nameof(IsTilesPerRowSelectorVisible))]
    private LibraryOverviewViewMode _overviewViewMode = LibraryOverviewViewMode.Tiles;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DesiredTilesPerRow))]
    private LibraryTilesPerRowOption _selectedTilesPerRowOption =
        LibraryTilesPerRowOptions.Auto;

    [ObservableProperty]
    private int _actualTilesPerRow = 3;

    [ObservableProperty]
    private int _actualCompactTilesPerRow = 5;

    public bool HasSections => Sections.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasNextPageError => !string.IsNullOrWhiteSpace(NextPageErrorMessage);

    public bool IsPaging => IsLoadingNextPage || IsLoadingPreviousPage;
    public bool SectionsHasPrevious => _sectionWindow.HasPrevious;
    public int SectionWindowStartOffset => _sectionWindow.WindowStartOffset;
    public int SectionWindowEndOffset => _sectionWindow.WindowEndOffset;

    public bool IsEmpty =>
        !IsLoading &&
        !HasError &&
        !HasSections &&
        string.IsNullOrWhiteSpace(SearchText);

    public bool HasNoSearchResults =>
        !IsLoading &&
        !HasError &&
        !HasSections &&
        !string.IsNullOrWhiteSpace(SearchText);

    public bool IsTableView => OverviewViewMode == LibraryOverviewViewMode.Table;

    public bool IsTilesView => OverviewViewMode == LibraryOverviewViewMode.Tiles;

    public bool IsCompactTilesView => OverviewViewMode == LibraryOverviewViewMode.CompactTiles;

    public bool IsTilesPerRowSelectorVisible => !IsTableView;

    public IReadOnlyList<LibraryTilesPerRowOption> TilesPerRowOptions =>
        LibraryTilesPerRowOptions.All;

    public int DesiredTilesPerRow =>
        SelectedTilesPerRowOption.Value ?? 0;

    public string SectionsShownCountText
    {
        get
        {
            int visibleCount = Math.Min(
                PageSize,
                Math.Max(0, TotalCount - CurrentPageOffset));

            return LibraryRangeTextFormatter.FormatEntity(
                "Разделы",
                "Разделы не найдены",
                CurrentPageOffset,
                visibleCount,
                TotalCount,
                !string.IsNullOrWhiteSpace(SearchText));
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _viewCancellationToken = cancellationToken;
        _isLoaded = true;

        LoadNextPageCommand.NotifyCanExecuteChanged();
        LoadPreviousPageCommand.NotifyCanExecuteChanged();

        await EnsureViewModeLoadedAsync(cancellationToken);
        await ReloadCoreAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _viewCancellationToken,
            cancellationToken);

        await ReloadCoreAsync(linkedCancellationTokenSource.Token);
    }

    [RelayCommand(CanExecute = nameof(CanLoadNextPage))]
    private Task LoadNextPageAsync(CancellationToken cancellationToken)
    {
        return LoadNextPageWithLinkedCancellationAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanLoadPreviousPage))]
    private Task LoadPreviousPageAsync(CancellationToken cancellationToken)
    {
        return LoadPreviousPageWithLinkedCancellationAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task RetryNextPageAsync(CancellationToken cancellationToken)
    {
        NextPageErrorMessage = null;

        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _viewCancellationToken,
            cancellationToken);

        await LoadNextPageWithLinkedCancellationAsync(linkedCancellationTokenSource.Token);
    }

    [RelayCommand]
    private Task ShowTableViewAsync(CancellationToken cancellationToken)
    {
        return SetViewModeAsync(LibraryOverviewViewMode.Table, cancellationToken);
    }

    [RelayCommand]
    private Task ShowTilesViewAsync(CancellationToken cancellationToken)
    {
        return SetViewModeAsync(LibraryOverviewViewMode.Tiles, cancellationToken);
    }

    [RelayCommand]
    private Task ShowCompactTilesViewAsync(CancellationToken cancellationToken)
    {
        return SetViewModeAsync(LibraryOverviewViewMode.CompactTiles, cancellationToken);
    }
    
    [RelayCommand]
    private async Task OpenSectionAsync(
        LibrarySectionCardViewModel? section,
        CancellationToken cancellationToken)
    {
        if (section is null)
            return;

        var result = await _queryDispatcher.SendAsync<GetLibrarySectionRootQuery, Guid>(
            new GetLibrarySectionRootQuery(section.Id),
            cancellationToken);

        if (result.IsFailure)
        {
            ErrorMessage = result.Error.FirstOrDefault()?.Message
                           ?? "Не удалось открыть раздел библиотеки";
            return;
        }

        if (result.Value != section.RootContainerId)
        {
            _logger.LogWarning(
                "RootContainerId раздела {SectionId} изменился: DTO={DtoRootId}, DB={DbRootId}",
                section.Id,
                section.RootContainerId,
                result.Value);
        }

        _pageNavigationService.NavigateTo<LibraryContainerViewModel>(
            viewModel => viewModel.Initialize(result.Value));
    }

    partial void OnSearchTextChanged(string? value)
    {
        int searchVersion = Interlocked.Increment(ref _searchVersion);

        if (_isLoaded)
        {
            _ = ReloadAfterSearchDelayAsync(searchVersion);
        }
    }

    partial void OnSelectedSortOptionChanged(LibrarySectionSortOption value)
    {
        if (_isLoaded)
        {
            _ = ReloadAfterSortChangedAsync();
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

    partial void OnActualTilesPerRowChanged(int value)
    {
        if (value > 0)
        {
            RebuildRows(SectionRows, value);
        }
    }

    partial void OnActualCompactTilesPerRowChanged(int value)
    {
        if (value > 0)
        {
            RebuildRows(CompactSectionRows, value);
        }
    }

    private bool CanLoadNextPage() =>
        _isLoaded &&
        _sectionWindow.HasNext &&
        !IsLoading &&
        !IsLoadingNextPage &&
        !IsLoadingPreviousPage &&
        !HasError &&
        !HasNextPageError;

    private bool CanLoadPreviousPage() =>
        _isLoaded &&
        _sectionWindow.HasPrevious &&
        !IsLoading &&
        !IsLoadingNextPage &&
        !IsLoadingPreviousPage &&
        !HasError;

    private async Task LoadNextPageWithLinkedCancellationAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _viewCancellationToken,
            cancellationToken);

        if (!_sectionWindow.HasNext)
        {
            return;
        }

        await ShowSectionPageAsync(
            _sectionWindow.NextOffset,
            PageWindowInsert.Append,
            _loadVersion,
            linkedCancellationTokenSource.Token,
            isInitialPage: false,
            isPreviousPage: false);
    }

    private async Task LoadPreviousPageWithLinkedCancellationAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _viewCancellationToken,
            cancellationToken);

        if (!_sectionWindow.HasPrevious)
        {
            return;
        }

        await ShowSectionPageAsync(
            _sectionWindow.PreviousOffset,
            PageWindowInsert.Prepend,
            _loadVersion,
            linkedCancellationTokenSource.Token,
            isInitialPage: false,
            isPreviousPage: true);
    }

    private async Task ReloadCoreAsync(CancellationToken cancellationToken)
    {
        int loadVersion = Interlocked.Increment(ref _loadVersion);

        _sectionWindow.Reset();
        CurrentPageOffset = 0;
        TotalCount = 0;
        HasMore = false;
        ErrorMessage = null;
        NextPageErrorMessage = null;
        IsLoadingNextPage = false;
        IsLoadingPreviousPage = false;

        Sections.Clear();
        SectionRows.Clear();
        CompactSectionRows.Clear();

        NotifyCollectionStateChanged();
        SyncSectionWindowState();

        await ShowSectionPageAsync(
            0,
            PageWindowInsert.Append,
            loadVersion,
            cancellationToken,
            isInitialPage: true,
            isPreviousPage: false);
    }

    private async Task ShowSectionPageAsync(
        int offset,
        PageWindowInsert insert,
        int loadVersion,
        CancellationToken cancellationToken,
        bool isInitialPage,
        bool isPreviousPage)
    {
        if (loadVersion != _loadVersion || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (_sectionWindow.TryGetCachedPage(
                offset,
                out IReadOnlyList<LibrarySectionOverviewDto> cached))
        {
            _sectionWindow.ShowPage(offset, cached, insert);
            RebuildSections();
            SyncSectionWindowState();
            return;
        }

        if (isInitialPage)
        {
            IsLoading = true;
            ErrorMessage = null;
        }
        else if (isPreviousPage)
        {
            IsLoadingPreviousPage = true;
        }
        else
        {
            IsLoadingNextPage = true;
            NextPageErrorMessage = null;
        }

        if (!isInitialPage)
        {
            await YieldForPagingLoaderAsync(cancellationToken);
        }

        try
        {
            var query = new GetLibrarySectionsPageQuery(
                SearchText,
                SelectedSortOption.Sort,
                offset,
                PageSize);

            var result = await _queryDispatcher.SendAsync<
                GetLibrarySectionsPageQuery,
                LibrarySectionsPageDto>(
                query,
                cancellationToken);

            if (loadVersion != _loadVersion || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (result.IsFailure)
            {
                string message = result.Error.FirstOrDefault()?.Message
                                 ?? "Не удалось загрузить разделы";

                if (isInitialPage)
                {
                    ErrorMessage = message;
                }
                else
                {
                    NextPageErrorMessage = message;
                }

                return;
            }

            _sectionWindow.SetTotalCount(result.Value.TotalCount);
            _sectionWindow.ShowPage(offset, result.Value.Items, insert);
            RebuildSections();
            SyncSectionWindowState();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Закрытие страницы или новый поиск/сортировка.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось загрузить страницу разделов offset {Offset}", offset);

            if (loadVersion != _loadVersion)
            {
                return;
            }

            if (isInitialPage)
            {
                ErrorMessage = "Не удалось загрузить разделы";
            }
            else
            {
                NextPageErrorMessage = "Не удалось загрузить страницу разделов";
            }
        }
        finally
        {
            if (loadVersion == _loadVersion)
            {
                IsLoading = false;
                IsLoadingNextPage = false;
                IsLoadingPreviousPage = false;
                SyncSectionWindowState();
            }
        }
    }

    private static async Task YieldForPagingLoaderAsync(CancellationToken cancellationToken)
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

    private void RebuildSections()
    {
        Sections.Clear();

        foreach (int offset in _sectionWindow.VisibleOffsets)
        {
            if (!_sectionWindow.TryGetCachedPage(
                    offset,
                    out IReadOnlyList<LibrarySectionOverviewDto> page))
            {
                continue;
            }

            foreach (LibrarySectionOverviewDto section in page)
            {
                Sections.Add(new LibrarySectionCardViewModel(section));
            }
        }

        RebuildRows(SectionRows, Math.Max(1, ActualTilesPerRow), Sections);
        RebuildRows(CompactSectionRows, Math.Max(1, ActualCompactTilesPerRow), Sections);
    }

    private void SyncSectionWindowState()
    {
        TotalCount = _sectionWindow.TotalCount;
        CurrentPageOffset = _sectionWindow.CurrentPageOffset;
        HasMore = _sectionWindow.HasNext;

        OnPropertyChanged(nameof(IsPaging));
        OnPropertyChanged(nameof(SectionsHasPrevious));
        OnPropertyChanged(nameof(SectionWindowStartOffset));
        OnPropertyChanged(nameof(SectionWindowEndOffset));
        OnPropertyChanged(nameof(SectionsShownCountText));

        LoadNextPageCommand.NotifyCanExecuteChanged();
        LoadPreviousPageCommand.NotifyCanExecuteChanged();
        NotifyCollectionStateChanged();
    }

    private void AddToSectionRows(LibrarySectionCardViewModel section)
    {
        AddToRows(SectionRows, section, Math.Max(1, ActualTilesPerRow));
        AddToRows(CompactSectionRows, section, Math.Max(1, ActualCompactTilesPerRow));
    }

    private static void RebuildRows(
        ObservableCollection<LibrarySectionRowViewModel> rows,
        int capacity,
        IEnumerable<LibrarySectionCardViewModel> sections)
    {
        rows.Clear();

        foreach (var section in sections)
        {
            AddToRows(rows, section, capacity);
        }
    }

    private void RebuildRows(
        ObservableCollection<LibrarySectionRowViewModel> rows,
        int capacity)
    {
        RebuildRows(rows, Math.Max(1, capacity), Sections);
    }

    private static void AddToRows(
        ObservableCollection<LibrarySectionRowViewModel> rows,
        LibrarySectionCardViewModel section,
        int capacity)
    {
        var row = rows.LastOrDefault();

        if (row is null || row.IsFull)
        {
            row = new LibrarySectionRowViewModel(capacity);
            rows.Add(row);
        }

        row.Add(section);
    }

    public void UpdateViewport(double logicalItemOffset)
    {
        if (!_sectionWindow.UpdateViewport(logicalItemOffset))
        {
            return;
        }

        CurrentPageOffset = _sectionWindow.CurrentPageOffset;
        OnPropertyChanged(nameof(SectionsShownCountText));
    }

    private async Task ReloadAfterSearchDelayAsync(int searchVersion)
    {
        try
        {
            await Task.Delay(SearchDelay, _viewCancellationToken);

            if (searchVersion == Volatile.Read(ref _searchVersion))
            {
                await ReloadCoreAsync(_viewCancellationToken);
            }
        }
        catch (OperationCanceledException) when (_viewCancellationToken.IsCancellationRequested)
        {
            // ignore
        }
    }

    private async Task ReloadAfterSortChangedAsync()
    {
        try
        {
            await ReloadCoreAsync(_viewCancellationToken);
        }
        catch (OperationCanceledException) when (_viewCancellationToken.IsCancellationRequested)
        {
            // ignore
        }
    }

    private async Task EnsureViewModeLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isViewModeLoaded)
        {
            return;
        }

        try
        {
            var settings = await _settingsService.LoadAsync(cancellationToken);

            OverviewViewMode = settings.LibraryOverviewViewMode;
            SelectedTilesPerRowOption =
                LibraryTilesPerRowOptions.Resolve(
                    settings.LibraryTilesPerRow);
            _isViewModeLoaded = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось загрузить режим просмотра разделов");

            OverviewViewMode = LibraryOverviewViewMode.Tiles;
            _isViewModeLoaded = true;
        }
    }

    private async Task SetViewModeAsync(
        LibraryOverviewViewMode overviewViewMode,
        CancellationToken cancellationToken)
    {
        if (OverviewViewMode == overviewViewMode)
        {
            return;
        }

        OverviewViewMode = overviewViewMode;

        try
        {
            await _settingsService.SaveLibraryOverviewViewModeAsync(overviewViewMode, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Не удалось сохранить режим просмотра разделов {ViewMode}",
                overviewViewMode);
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

    private void NotifyCollectionStateChanged()
    {
        OnPropertyChanged(nameof(HasSections));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasNoSearchResults));
    }
}

public sealed record LibrarySectionSortOption(
    string Name,
    LibrarySectionSort Sort);
