using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Library.GetManagementMaterialsPage;
using Mnemora.Application.Library.GetManagementSectionsPage;
using Mnemora.Application.Library.GetManagementFoldersPage;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class LibraryManagementViewModel
{
    private const int SIMPLE_SECTION_VISIBLE_PAGE_LIMIT = 7;
    private const int SIMPLE_SECTION_CACHE_PAGE_LIMIT = 10;
    private const int SIMPLE_FOLDER_PAGE_SIZE = LibraryPagingDefaults.PAGE_SIZE;
    private const int SIMPLE_FOLDER_VISIBLE_PAGE_LIMIT = 7;
    private const int SIMPLE_FOLDER_CACHE_PAGE_LIMIT = 10;
    private const int SIMPLE_MATERIAL_VISIBLE_PAGE_LIMIT = 7;
    private const int SIMPLE_MATERIAL_CACHE_PAGE_LIMIT = 10;

    private readonly BoundedPagedWindow<LibrarySectionOverviewDto> _simpleSectionWindow =
        new(SIMPLE_SECTION_PAGE_SIZE, SIMPLE_SECTION_VISIBLE_PAGE_LIMIT, SIMPLE_SECTION_CACHE_PAGE_LIMIT);

    private readonly Dictionary<int, Task<LibraryManagementSectionsPageDto?>> _simpleSectionInFlight = [];
    private readonly object _simpleSectionInFlightGate = new();

    private readonly BoundedPagedWindow<LibraryManagementFolderOverviewDto> _simpleFolderWindow =
        new(SIMPLE_FOLDER_PAGE_SIZE, SIMPLE_FOLDER_VISIBLE_PAGE_LIMIT, SIMPLE_FOLDER_CACHE_PAGE_LIMIT);

    private readonly Dictionary<int, Task<LibraryManagementFoldersPageDto?>> _simpleFolderInFlight = [];
    private readonly object _simpleFolderInFlightGate = new();

    private readonly BoundedPageCache<LibraryManagementMaterialOverviewDto> _simpleMaterialPageCache =
        new(SIMPLE_MATERIAL_CACHE_PAGE_LIMIT);

    private readonly LinkedList<int> _simpleMaterialVisiblePageOffsets = [];
    private readonly Dictionary<int, Task<LibraryManagementMaterialsPageDto?>> _simpleMaterialInFlight = [];
    private readonly object _simpleMaterialInFlightGate = new();

    private bool _isSimpleSectionsLoadingPreviousPage;
    private CancellationTokenSource? _simpleSectionContextCancellation;

    private int _simpleFoldersTotalCount;
    private int _simpleFolderSourceTotalCount;
    private int _simpleFolderLoadVersion;
    private bool _isSimpleFoldersLoadingNextPage;
    private bool _isSimpleFoldersLoadingPreviousPage;
    private CancellationTokenSource? _simpleFolderContextCancellation;

    private int _simpleMaterialLoadVersion;
    private int _simpleMaterialWindowStartOffset;
    private int _simpleMaterialWindowEndOffset;
    private int _simpleMaterialCurrentPageOffset;
    private int _simpleMaterialSourceTotalCount;
    private bool _isSimpleMaterialsLoadingNextPage;
    private bool _isSimpleMaterialsLoadingPreviousPage;
    private CancellationTokenSource? _simpleMaterialContextCancellation;

    public bool SimpleSectionsHasPrevious =>
        _simpleSectionWindow.HasPrevious &&
        !_isSimpleSectionsLoadingPreviousPage;

    public bool IsSimpleSectionsPaging =>
        IsSimpleSectionsLoadingNextPage ||
        _isSimpleSectionsLoadingPreviousPage;

    public bool IsSimpleSectionsLoadingPreviousPage => _isSimpleSectionsLoadingPreviousPage;
    public int SimpleSectionWindowStartOffset => _simpleSectionWindow.WindowStartOffset;
    public int SimpleSectionWindowEndOffset => _simpleSectionWindow.WindowEndOffset;
    public int SimpleSectionCurrentPageOffset => _simpleSectionWindow.CurrentPageOffset;
    public int SimpleSectionCachedPageCount => _simpleSectionWindow.CachedPageCount;
    public int SimpleSectionCachedItemUpperBound => _simpleSectionWindow.CachedItemUpperBound;

    public bool SimpleFoldersHasMore =>
        _simpleFolderWindow.HasNext &&
        !_isSimpleFoldersLoadingNextPage;

    public bool SimpleFoldersHasPrevious =>
        _simpleFolderWindow.HasPrevious &&
        !_isSimpleFoldersLoadingPreviousPage;

    public bool IsSimpleFoldersLoadingNextPage => _isSimpleFoldersLoadingNextPage;
    public bool IsSimpleFoldersLoadingPreviousPage => _isSimpleFoldersLoadingPreviousPage;
    public bool IsSimpleFoldersPaging =>
        _isSimpleFoldersLoadingNextPage ||
        _isSimpleFoldersLoadingPreviousPage;
    public int SimpleFoldersTotalCount => _simpleFoldersTotalCount;
    public int SimpleFolderWindowStartOffset => _simpleFolderWindow.WindowStartOffset;
    public int SimpleFolderWindowEndOffset => _simpleFolderWindow.WindowEndOffset;
    public int SimpleFolderCurrentPageOffset => _simpleFolderWindow.CurrentPageOffset;
    public int SimpleFolderCachedPageCount => _simpleFolderWindow.CachedPageCount;
    public int SimpleFolderCachedItemUpperBound => _simpleFolderWindow.CachedItemUpperBound;

    public bool SimpleMaterialsHasPrevious =>
        _simpleMaterialWindowStartOffset > 0 &&
        !_isSimpleMaterialsLoadingPreviousPage;

    public bool IsSimpleMaterialsLoadingNextPage => _isSimpleMaterialsLoadingNextPage;
    public bool IsSimpleMaterialsLoadingPreviousPage => _isSimpleMaterialsLoadingPreviousPage;
    public bool IsSimpleMaterialsPaging =>
        _isSimpleMaterialsLoadingNextPage ||
        _isSimpleMaterialsLoadingPreviousPage;

    public int SimpleMaterialWindowStartOffset => _simpleMaterialWindowStartOffset;
    public int SimpleMaterialWindowEndOffset => _simpleMaterialWindowEndOffset;
    public int SimpleMaterialCurrentPageOffset => _simpleMaterialCurrentPageOffset;
    public int SimpleMaterialCachedPageCount => _simpleMaterialPageCache.Count;
    public int SimpleMaterialCachedItemUpperBound => SIMPLE_MATERIAL_CACHE_PAGE_LIMIT * SIMPLE_MATERIAL_PAGE_SIZE;

    public async Task LoadNextSimpleSectionWindowAsync(CancellationToken cancellationToken = default)
    {
        if (!CanLoadNextSimpleSectionPage())
        {
            return;
        }

        int loadVersion = _simpleSectionLoadVersion;
        int offset = _simpleSectionWindow.NextOffset;

        if (_simpleSectionWindow.TryGetCachedPage(
                offset,
                out IReadOnlyList<LibrarySectionOverviewDto> cached))
        {
            _simpleSectionWindow.ShowPage(offset, cached, PageWindowInsert.APPEND);
            RebuildSimpleSectionWindow();
            SyncSimpleSectionPagingProperties();
            return;
        }

        IsSimpleSectionsLoadingNextPage = true;
        SimpleSectionsNextPageErrorMessage = null;
        NotifySimpleSectionsStateChanged();

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _simpleSectionContextCancellation?.Token ?? _viewCancellationToken,
                cancellationToken);

            await YieldForPagingLoaderAsync(linked.Token);

            LibraryManagementSectionsPageDto? page = await GetSimpleSectionPageAsync(
                offset,
                loadVersion,
                linked.Token,
                reportFailure: true);

            if (page is null || loadVersion != _simpleSectionLoadVersion)
            {
                return;
            }

            ApplySectionPageTotals(page);

            if (page.Items.Count == 0)
            {
                return;
            }

            _simpleSectionWindow.ShowPage(offset, page.Items, PageWindowInsert.APPEND);
            RebuildSimpleSectionWindow();
            SyncSimpleSectionPagingProperties();
        }
        finally
        {
            if (loadVersion == _simpleSectionLoadVersion)
            {
                IsSimpleSectionsLoadingNextPage = false;
                SyncSimpleSectionPagingProperties();
            }
        }
    }

    public async Task LoadPreviousSimpleSectionWindowAsync(CancellationToken cancellationToken = default)
    {
        if (!SimpleSectionsHasPrevious ||
            IsSimpleSectionsLoading ||
            IsSimpleSectionsLoadingNextPage ||
            _isSimpleSectionsLoadingPreviousPage)
        {
            return;
        }

        int loadVersion = _simpleSectionLoadVersion;
        int offset = _simpleSectionWindow.PreviousOffset;

        if (_simpleSectionWindow.TryGetCachedPage(
                offset,
                out IReadOnlyList<LibrarySectionOverviewDto> cached))
        {
            _simpleSectionWindow.ShowPage(offset, cached, PageWindowInsert.PREPEND);
            RebuildSimpleSectionWindow();
            SyncSimpleSectionPagingProperties();
            return;
        }

        _isSimpleSectionsLoadingPreviousPage = true;
        OnPropertyChanged(nameof(IsSimpleSectionsLoadingPreviousPage));
        NotifySimpleSectionsStateChanged();

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _simpleSectionContextCancellation?.Token ?? _viewCancellationToken,
                cancellationToken);

            await YieldForPagingLoaderAsync(linked.Token);

            LibraryManagementSectionsPageDto? page = await GetSimpleSectionPageAsync(
                offset,
                loadVersion,
                linked.Token,
                reportFailure: true);

            if (page is null || loadVersion != _simpleSectionLoadVersion || page.Items.Count == 0)
            {
                return;
            }

            ApplySectionPageTotals(page);
            _simpleSectionWindow.ShowPage(offset, page.Items, PageWindowInsert.PREPEND);
            RebuildSimpleSectionWindow();
            SyncSimpleSectionPagingProperties();
        }
        finally
        {
            if (loadVersion == _simpleSectionLoadVersion)
            {
                _isSimpleSectionsLoadingPreviousPage = false;
                OnPropertyChanged(nameof(IsSimpleSectionsLoadingPreviousPage));
                SyncSimpleSectionPagingProperties();
            }
        }
    }

    private int ResetSimpleSectionPagingState(CancellationToken cancellationToken)
    {
        int loadVersion = Interlocked.Increment(ref _simpleSectionLoadVersion);

        _simpleSectionContextCancellation?.Cancel();
        _simpleSectionContextCancellation?.Dispose();
        _simpleSectionContextCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _viewCancellationToken,
            cancellationToken);

        _simpleSectionWindow.Reset();

        lock (_simpleSectionInFlightGate)
        {
            _simpleSectionInFlight.Clear();
        }

        _isSimpleSectionsLoadingPreviousPage = false;
        IsSimpleSectionsLoadingNextPage = false;
        SimpleSectionsNextPageErrorMessage = null;
        SimpleSectionsTotalCount = 0;
        SimpleSections.Clear();
        SimpleSectionRows.Clear();
        SimpleCompactSectionRows.Clear();
        SyncSimpleSectionPagingProperties();

        return loadVersion;
    }

    private async Task<LibraryManagementSectionsPageDto?> GetSimpleSectionPageAsync(
        int offset,
        int loadVersion,
        CancellationToken cancellationToken,
        bool reportFailure)
    {
        if (loadVersion != _simpleSectionLoadVersion)
        {
            return null;
        }

        if (_simpleSectionWindow.TryGetCachedPage(
                offset,
                out IReadOnlyList<LibrarySectionOverviewDto> cached))
        {
            return new LibraryManagementSectionsPageDto(
                cached,
                offset + cached.Count,
                offset + cached.Count < SimpleSectionsTotalCount,
                SimpleSectionsTotalCount);
        }

        Task<LibraryManagementSectionsPageDto?> task;

        lock (_simpleSectionInFlightGate)
        {
            if (!_simpleSectionInFlight.TryGetValue(offset, out task!))
            {
                string? search = SearchText;
                LibraryManagementSectionSort sort = SelectedSimpleSectionSortOption.Sort;

                task = QuerySimpleSectionPageAsync(
                    search,
                    sort,
                    offset,
                    cancellationToken,
                    reportFailure);

                _simpleSectionInFlight[offset] = task;
            }
        }

        try
        {
            LibraryManagementSectionsPageDto? page = await task;

            if (page is null ||
                loadVersion != _simpleSectionLoadVersion ||
                cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            _simpleSectionWindow.StorePage(offset, page.Items);
            return page;
        }
        finally
        {
            lock (_simpleSectionInFlightGate)
            {
                if (_simpleSectionInFlight.TryGetValue(
                        offset,
                        out Task<LibraryManagementSectionsPageDto?>? current) &&
                    ReferenceEquals(current, task))
                {
                    _simpleSectionInFlight.Remove(offset);
                }
            }
        }
    }

    private async Task<LibraryManagementSectionsPageDto?> QuerySimpleSectionPageAsync(
        string? search,
        LibraryManagementSectionSort sort,
        int offset,
        CancellationToken cancellationToken,
        bool reportFailure)
    {
        try
        {
            var query = new GetLibraryManagementSectionsPageQuery(
                search,
                sort,
                offset,
                SIMPLE_SECTION_PAGE_SIZE);

            var result = await queryDispatcher.SendAsync<
                GetLibraryManagementSectionsPageQuery,
                LibraryManagementSectionsPageDto>(
                query,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            if (result.IsFailure)
            {
                if (reportFailure)
                {
                    string message = result.Error.FirstOrDefault()?.Message
                                     ?? "Не удалось загрузить разделы";

                    if (offset == 0)
                    {
                        ErrorMessage = message;
                    }
                    else
                    {
                        SimpleSectionsNextPageErrorMessage = message;
                    }
                }

                return null;
            }

            return result.Value;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось загрузить страницу разделов {Offset}", offset);

            if (reportFailure)
            {
                if (offset == 0)
                {
                    ErrorMessage = "Не удалось загрузить разделы";
                }
                else
                {
                    SimpleSectionsNextPageErrorMessage = "Не удалось загрузить следующую порцию разделов";
                }
            }

            return null;
        }
    }

    private void ApplySectionPageTotals(LibraryManagementSectionsPageDto page)
    {
        SimpleSectionsTotalCount = page.TotalCount;
        _simpleSectionWindow.SetTotalCount(page.TotalCount);
    }

    private void RebuildSimpleSectionWindow()
    {
        SimpleSections.Clear();
        SimpleSectionRows.Clear();
        SimpleCompactSectionRows.Clear();

        foreach (int offset in _simpleSectionWindow.VisibleOffsets)
        {
            if (!_simpleSectionWindow.TryGetCachedPage(
                    offset,
                    out IReadOnlyList<LibrarySectionOverviewDto> page))
            {
                continue;
            }

            foreach (LibrarySectionOverviewDto section in page)
            {
                SimpleSections.Add(new LibraryManagementSectionViewModel(section));
            }
        }

        RebuildSimpleSectionRows(SimpleSectionRows, capacity: 3);
        RebuildSimpleSectionRows(SimpleCompactSectionRows, capacity: 4);
    }

    private void RebuildSimpleSectionRows(
        ObservableCollection<LibraryManagementSectionRowViewModel> rows,
        int capacity)
    {
        bool showCreateTile = _simpleSectionWindow.WindowStartOffset == 0;
        int index = 0;

        while (index < SimpleSections.Count)
        {
            bool isFirstRow = rows.Count == 0 && showCreateTile;
            int rowCapacity = isFirstRow ? Math.Max(1, capacity - 1) : capacity;
            var row = new LibraryManagementSectionRowViewModel(rowCapacity, isFirstRow);

            for (int count = 0; count < rowCapacity && index < SimpleSections.Count; count++, index++)
            {
                row.Add(SimpleSections[index]);
            }

            rows.Add(row);
        }
    }

    private void SyncSimpleSectionPagingProperties()
    {
        SimpleSectionsHasMore = _simpleSectionWindow.HasNext;
        OnPropertyChanged(nameof(SimpleSectionsHasPrevious));
        OnPropertyChanged(nameof(SimpleSectionWindowStartOffset));
        OnPropertyChanged(nameof(SimpleSectionWindowEndOffset));
        OnPropertyChanged(nameof(SimpleSectionCurrentPageOffset));
        OnPropertyChanged(nameof(SimpleSectionCachedPageCount));
        OnPropertyChanged(nameof(SimpleSectionsShownCountText));
        LoadNextSimpleSectionPageCommand.NotifyCanExecuteChanged();
        NotifySimpleSectionsStateChanged();
    }

    public void UpdateSimpleSectionViewport(double logicalItemOffset)
    {
        if (!_simpleSectionWindow.UpdateViewport(logicalItemOffset))
        {
            return;
        }

        OnPropertyChanged(nameof(SimpleSectionCurrentPageOffset));
        OnPropertyChanged(nameof(SimpleSectionsShownCountText));
    }

    private async Task ReloadSimpleFoldersPagedAsync(CancellationToken cancellationToken)
    {
        if (SelectedSection is null)
        {
            ClearSimpleFoldersPagedState();
            return;
        }

        int loadVersion = ResetSimpleFolderPagingState(cancellationToken);
        IsContextLoading = true;
        ErrorMessage = null;

        try
        {
            LibraryManagementFoldersPageDto? page = await GetSimpleFolderPageAsync(
                offset: 0,
                loadVersion,
                _simpleFolderContextCancellation!.Token,
                reportFailure: true);

            if (page is null || loadVersion != _simpleFolderLoadVersion)
            {
                return;
            }

            ApplyFolderPageTotals(page);
            _simpleFolderWindow.ShowPage(0, page.Items, PageWindowInsert.APPEND);
            RebuildSimpleFolderWindow();
            NotifySimpleFoldersStateChanged();
        }
        finally
        {
            if (loadVersion == _simpleFolderLoadVersion)
            {
                IsContextLoading = false;
                NotifySimpleFoldersStateChanged();
            }
        }
    }

    public async Task LoadNextSimpleFolderWindowAsync(CancellationToken cancellationToken = default)
    {
        if (!CanLoadNextSimpleFolderPage() || SelectedSection is null)
        {
            return;
        }

        int loadVersion = _simpleFolderLoadVersion;
        int offset = _simpleFolderWindow.NextOffset;

        if (_simpleFolderWindow.TryGetCachedPage(
                offset,
                out IReadOnlyList<LibraryManagementFolderOverviewDto> cached))
        {
            _simpleFolderWindow.ShowPage(offset, cached, PageWindowInsert.APPEND);
            RebuildSimpleFolderWindow();
            NotifySimpleFoldersStateChanged();
            return;
        }

        _isSimpleFoldersLoadingNextPage = true;
        OnPropertyChanged(nameof(IsSimpleFoldersLoadingNextPage));
        NotifySimpleFoldersStateChanged();

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _simpleFolderContextCancellation?.Token ?? _viewCancellationToken,
                cancellationToken);

            await YieldForPagingLoaderAsync(linked.Token);

            LibraryManagementFoldersPageDto? page = await GetSimpleFolderPageAsync(
                offset,
                loadVersion,
                linked.Token,
                reportFailure: true);

            if (page is null || loadVersion != _simpleFolderLoadVersion)
            {
                return;
            }

            ApplyFolderPageTotals(page);

            if (page.Items.Count == 0)
            {
                return;
            }

            _simpleFolderWindow.ShowPage(offset, page.Items, PageWindowInsert.APPEND);
            RebuildSimpleFolderWindow();
            NotifySimpleFoldersStateChanged();
        }
        finally
        {
            if (loadVersion == _simpleFolderLoadVersion)
            {
                _isSimpleFoldersLoadingNextPage = false;
                OnPropertyChanged(nameof(IsSimpleFoldersLoadingNextPage));
                LoadNextSimpleFolderPageCommand.NotifyCanExecuteChanged();
                NotifySimpleFoldersStateChanged();
            }
        }
    }

    public async Task LoadPreviousSimpleFolderWindowAsync(CancellationToken cancellationToken = default)
    {
        if (!SimpleFoldersHasPrevious ||
            IsContextLoading ||
            _isSimpleFoldersLoadingNextPage ||
            _isSimpleFoldersLoadingPreviousPage ||
            SelectedSection is null)
        {
            return;
        }

        int loadVersion = _simpleFolderLoadVersion;
        int offset = _simpleFolderWindow.PreviousOffset;

        if (_simpleFolderWindow.TryGetCachedPage(
                offset,
                out IReadOnlyList<LibraryManagementFolderOverviewDto> cached))
        {
            _simpleFolderWindow.ShowPage(offset, cached, PageWindowInsert.PREPEND);
            RebuildSimpleFolderWindow();
            NotifySimpleFoldersStateChanged();
            return;
        }

        _isSimpleFoldersLoadingPreviousPage = true;
        OnPropertyChanged(nameof(IsSimpleFoldersLoadingPreviousPage));
        NotifySimpleFoldersStateChanged();

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _simpleFolderContextCancellation?.Token ?? _viewCancellationToken,
                cancellationToken);

            await YieldForPagingLoaderAsync(linked.Token);

            LibraryManagementFoldersPageDto? page = await GetSimpleFolderPageAsync(
                offset,
                loadVersion,
                linked.Token,
                reportFailure: true);

            if (page is null || loadVersion != _simpleFolderLoadVersion || page.Items.Count == 0)
            {
                return;
            }

            ApplyFolderPageTotals(page);
            _simpleFolderWindow.ShowPage(offset, page.Items, PageWindowInsert.PREPEND);
            RebuildSimpleFolderWindow();
            NotifySimpleFoldersStateChanged();
        }
        finally
        {
            if (loadVersion == _simpleFolderLoadVersion)
            {
                _isSimpleFoldersLoadingPreviousPage = false;
                OnPropertyChanged(nameof(IsSimpleFoldersLoadingPreviousPage));
                NotifySimpleFoldersStateChanged();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadNextSimpleFolderPage))]
    private Task LoadNextSimpleFolderPageAsync(CancellationToken cancellationToken) =>
        LoadNextSimpleFolderWindowAsync(cancellationToken);

    private bool CanLoadNextSimpleFolderPage() =>
        IsSimpleFoldersPage &&
        _simpleFolderWindow.HasNext &&
        !_isSimpleFoldersLoadingNextPage &&
        !_isSimpleFoldersLoadingPreviousPage &&
        !IsContextLoading &&
        !HasError;

    private int ResetSimpleFolderPagingState(CancellationToken cancellationToken)
    {
        int loadVersion = Interlocked.Increment(ref _simpleFolderLoadVersion);

        _simpleFolderContextCancellation?.Cancel();
        _simpleFolderContextCancellation?.Dispose();
        _simpleFolderContextCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _viewCancellationToken,
            cancellationToken);

        _simpleFolderWindow.Reset();

        lock (_simpleFolderInFlightGate)
        {
            _simpleFolderInFlight.Clear();
        }

        _simpleFoldersTotalCount = 0;
        _simpleFolderSourceTotalCount = SelectedSection?.SectionOverview?.RootFoldersCount
                                      ?? SelectedSection?.Section?.Folders.Count(folder => folder.Depth == 1)
                                      ?? 0;
        _isSimpleFoldersLoadingNextPage = false;
        _isSimpleFoldersLoadingPreviousPage = false;
        SimpleFolders.Clear();
        SimpleFolderRows.Clear();
        SimpleCompactFolderRows.Clear();
        NotifySimpleFoldersStateChanged();

        return loadVersion;
    }

    private void ClearSimpleFoldersPagedState()
    {
        Interlocked.Increment(ref _simpleFolderLoadVersion);
        _simpleFolderContextCancellation?.Cancel();
        _simpleFolderContextCancellation?.Dispose();
        _simpleFolderContextCancellation = null;
        _simpleFolderWindow.Reset();

        lock (_simpleFolderInFlightGate)
        {
            _simpleFolderInFlight.Clear();
        }

        _simpleFoldersTotalCount = 0;
        _simpleFolderSourceTotalCount = 0;
        _isSimpleFoldersLoadingNextPage = false;
        _isSimpleFoldersLoadingPreviousPage = false;
        SimpleFolders.Clear();
        SimpleFolderRows.Clear();
        SimpleCompactFolderRows.Clear();
        NotifySimpleFoldersStateChanged();
    }

    private async Task<LibraryManagementFoldersPageDto?> GetSimpleFolderPageAsync(
        int offset,
        int loadVersion,
        CancellationToken cancellationToken,
        bool reportFailure)
    {
        if (loadVersion != _simpleFolderLoadVersion || SelectedSection is null)
        {
            return null;
        }

        if (_simpleFolderWindow.TryGetCachedPage(
                offset,
                out IReadOnlyList<LibraryManagementFolderOverviewDto> cached))
        {
            return new LibraryManagementFoldersPageDto(
                cached,
                offset + cached.Count,
                offset + cached.Count < _simpleFoldersTotalCount,
                _simpleFoldersTotalCount);
        }

        Task<LibraryManagementFoldersPageDto?> task;

        lock (_simpleFolderInFlightGate)
        {
            if (!_simpleFolderInFlight.TryGetValue(offset, out task!))
            {
                Guid sectionId = SelectedSection.Id;
                string? search = SimpleFolderSearchText;
                LibraryManagementFolderSort sort = SelectedSimpleFolderSortOption.Sort;

                task = QuerySimpleFolderPageAsync(
                    sectionId,
                    search,
                    sort,
                    offset,
                    cancellationToken,
                    reportFailure);

                _simpleFolderInFlight[offset] = task;
            }
        }

        try
        {
            LibraryManagementFoldersPageDto? page = await task;

            if (page is null ||
                loadVersion != _simpleFolderLoadVersion ||
                cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            _simpleFolderWindow.StorePage(offset, page.Items);
            return page;
        }
        finally
        {
            lock (_simpleFolderInFlightGate)
            {
                if (_simpleFolderInFlight.TryGetValue(
                        offset,
                        out Task<LibraryManagementFoldersPageDto?>? current) &&
                    ReferenceEquals(current, task))
                {
                    _simpleFolderInFlight.Remove(offset);
                }
            }
        }
    }

    private async Task<LibraryManagementFoldersPageDto?> QuerySimpleFolderPageAsync(
        Guid sectionId,
        string? search,
        LibraryManagementFolderSort sort,
        int offset,
        CancellationToken cancellationToken,
        bool reportFailure)
    {
        try
        {
            var query = new GetLibraryManagementFoldersPageQuery(
                sectionId,
                search,
                ToFolderPageSort(sort),
                offset,
                SIMPLE_FOLDER_PAGE_SIZE);

            var result = await queryDispatcher.SendAsync<
                GetLibraryManagementFoldersPageQuery,
                LibraryManagementFoldersPageDto>(
                query,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested || SelectedSection?.Id != sectionId)
            {
                return null;
            }

            if (result.IsFailure)
            {
                if (reportFailure)
                {
                    ErrorMessage = result.Error.FirstOrDefault()?.Message
                                   ?? "Не удалось загрузить папки";
                }

                return null;
            }

            return result.Value;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось загрузить страницу папок {Offset}", offset);

            if (reportFailure)
            {
                ErrorMessage = "Не удалось загрузить папки";
            }

            return null;
        }
    }

    private void ApplyFolderPageTotals(LibraryManagementFoldersPageDto page)
    {
        _simpleFoldersTotalCount = page.TotalCount;
        _simpleFolderWindow.SetTotalCount(page.TotalCount);

        if (string.IsNullOrWhiteSpace(SimpleFolderSearchText))
        {
            _simpleFolderSourceTotalCount = page.TotalCount;
        }
    }

    private void RebuildSimpleFolderWindow()
    {
        SimpleFolders.Clear();
        SimpleFolderRows.Clear();
        SimpleCompactFolderRows.Clear();

        foreach (int offset in _simpleFolderWindow.VisibleOffsets)
        {
            if (!_simpleFolderWindow.TryGetCachedPage(
                    offset,
                    out IReadOnlyList<LibraryManagementFolderOverviewDto> page))
            {
                continue;
            }

            int position = offset + 1;

            foreach (LibraryManagementFolderOverviewDto folder in page)
            {
                SimpleFolders.Add(new LibraryManagementOrderItemViewModel(folder, position++));
            }
        }

        RebuildSimpleFolderRows(SimpleFolderRows, capacity: 3);
        RebuildSimpleFolderRows(SimpleCompactFolderRows, capacity: 4);
    }

    private void RebuildSimpleFolderRows(
        ObservableCollection<LibraryManagementFolderRowViewModel> rows,
        int capacity)
    {
        bool showCreateTile = _simpleFolderWindow.WindowStartOffset == 0;
        int index = 0;

        while (index < SimpleFolders.Count)
        {
            bool isFirstRow = rows.Count == 0 && showCreateTile;
            int rowCapacity = isFirstRow ? Math.Max(1, capacity - 1) : capacity;
            var row = new LibraryManagementFolderRowViewModel(rowCapacity, isFirstRow);

            for (int count = 0; count < rowCapacity && index < SimpleFolders.Count; count++, index++)
            {
                row.Add(SimpleFolders[index]);
            }

            rows.Add(row);
        }
    }

    public void UpdateSimpleFolderViewport(double logicalItemOffset)
    {
        if (!_simpleFolderWindow.UpdateViewport(logicalItemOffset))
        {
            return;
        }

        OnPropertyChanged(nameof(SimpleFolderCurrentPageOffset));
        OnPropertyChanged(nameof(SimpleFoldersShownCountText));
    }

    private async Task ReloadSimpleMaterialsPagedAsync(CancellationToken cancellationToken)
    {
        if (SelectedFolder is null)
        {
            ClearSimpleMaterialsPagedState();
            return;
        }

        int loadVersion = ResetSimpleMaterialPagingState(cancellationToken);
        IsContextLoading = true;
        ErrorMessage = null;

        try
        {
            LibraryManagementMaterialsPageDto? page = await GetSimpleMaterialPageAsync(
                offset: 0,
                loadVersion,
                _simpleMaterialContextCancellation!.Token,
                reportFailure: true);

            if (page is null || loadVersion != _simpleMaterialLoadVersion)
            {
                return;
            }

            ApplyMaterialPageTotals(page);
            AddVisibleMaterialPageOffset(0, append: true);
            RebuildSimpleMaterialWindow();
            SelectedMaterial = SimpleMaterials.FirstOrDefault();
            NotifySimpleMaterialsStateChanged();
        }
        finally
        {
            if (loadVersion == _simpleMaterialLoadVersion)
            {
                IsContextLoading = false;
                NotifySimpleMaterialsStateChanged();
            }
        }
    }

    public async Task LoadNextSimpleMaterialWindowAsync(CancellationToken cancellationToken = default)
    {
        if (!CanLoadNextSimpleMaterialPage() || SelectedFolder is null)
        {
            return;
        }

        int loadVersion = _simpleMaterialLoadVersion;
        int offset = _simpleMaterialWindowEndOffset;

        if (_simpleMaterialPageCache.TryGet(
                offset,
                out IReadOnlyList<LibraryManagementMaterialOverviewDto> cached))
        {
            AddVisibleMaterialPageOffset(offset, append: true);
            TrimVisibleMaterialPagesFromStart();
            RebuildSimpleMaterialWindow();
            NotifySimpleMaterialsStateChanged();
            return;
        }

        _isSimpleMaterialsLoadingNextPage = true;
        NotifySimpleMaterialsStateChanged();

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _simpleMaterialContextCancellation?.Token ?? _viewCancellationToken,
                cancellationToken);

            await YieldForPagingLoaderAsync(linked.Token);

            LibraryManagementMaterialsPageDto? page = await GetSimpleMaterialPageAsync(
                offset,
                loadVersion,
                linked.Token,
                reportFailure: true);

            if (page is null || loadVersion != _simpleMaterialLoadVersion)
            {
                return;
            }

            ApplyMaterialPageTotals(page);

            if (page.Items.Count == 0)
            {
                return;
            }

            AddVisibleMaterialPageOffset(offset, append: true);
            TrimVisibleMaterialPagesFromStart();
            RebuildSimpleMaterialWindow();
            NotifySimpleMaterialsStateChanged();
        }
        finally
        {
            if (loadVersion == _simpleMaterialLoadVersion)
            {
                _isSimpleMaterialsLoadingNextPage = false;
                NotifySimpleMaterialsStateChanged();
            }
        }
    }

    public async Task LoadPreviousSimpleMaterialWindowAsync(CancellationToken cancellationToken = default)
    {
        if (!SimpleMaterialsHasPrevious ||
            IsContextLoading ||
            _isSimpleMaterialsLoadingNextPage ||
            _isSimpleMaterialsLoadingPreviousPage ||
            SelectedFolder is null)
        {
            return;
        }

        int loadVersion = _simpleMaterialLoadVersion;
        int firstOffset = _simpleMaterialVisiblePageOffsets.First?.Value ?? 0;
        int offset = Math.Max(0, firstOffset - SIMPLE_MATERIAL_PAGE_SIZE);

        if (_simpleMaterialPageCache.TryGet(
                offset,
                out IReadOnlyList<LibraryManagementMaterialOverviewDto> cached))
        {
            AddVisibleMaterialPageOffset(offset, append: false);
            TrimVisibleMaterialPagesFromEnd();
            RebuildSimpleMaterialWindow();
            NotifySimpleMaterialsStateChanged();
            return;
        }

        _isSimpleMaterialsLoadingPreviousPage = true;
        NotifySimpleMaterialsStateChanged();

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _simpleMaterialContextCancellation?.Token ?? _viewCancellationToken,
                cancellationToken);

            await YieldForPagingLoaderAsync(linked.Token);

            LibraryManagementMaterialsPageDto? page = await GetSimpleMaterialPageAsync(
                offset,
                loadVersion,
                linked.Token,
                reportFailure: true);

            if (page is null || loadVersion != _simpleMaterialLoadVersion || page.Items.Count == 0)
            {
                return;
            }

            ApplyMaterialPageTotals(page);
            AddVisibleMaterialPageOffset(offset, append: false);
            TrimVisibleMaterialPagesFromEnd();
            RebuildSimpleMaterialWindow();
            NotifySimpleMaterialsStateChanged();
        }
        finally
        {
            if (loadVersion == _simpleMaterialLoadVersion)
            {
                _isSimpleMaterialsLoadingPreviousPage = false;
                NotifySimpleMaterialsStateChanged();
            }
        }
    }

    [RelayCommand]
    private Task LoadPreviousSimpleMaterialPageAsync(CancellationToken cancellationToken) =>
        LoadPreviousSimpleMaterialWindowAsync(cancellationToken);

    private int ResetSimpleMaterialPagingState(CancellationToken cancellationToken)
    {
        int loadVersion = Interlocked.Increment(ref _simpleMaterialLoadVersion);

        _simpleMaterialContextCancellation?.Cancel();
        _simpleMaterialContextCancellation?.Dispose();
        _simpleMaterialContextCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _viewCancellationToken,
            cancellationToken);

        _simpleMaterialPageCache.Clear();
        _simpleMaterialVisiblePageOffsets.Clear();

        lock (_simpleMaterialInFlightGate)
        {
            _simpleMaterialInFlight.Clear();
        }

        _simpleMaterialWindowStartOffset = 0;
        _simpleMaterialWindowEndOffset = 0;
        _simpleMaterialCurrentPageOffset = 0;
        _simpleMaterialsFilteredTotalCount = 0;
        _simpleMaterialSourceTotalCount = 0;
        _isSimpleMaterialsLoadingNextPage = false;
        _isSimpleMaterialsLoadingPreviousPage = false;
        SimpleMaterials.Clear();
        SelectedMaterial = null;
        NotifySimpleMaterialsStateChanged();

        return loadVersion;
    }

    private void ClearSimpleMaterialsPagedState()
    {
        Interlocked.Increment(ref _simpleMaterialLoadVersion);
        _simpleMaterialContextCancellation?.Cancel();
        _simpleMaterialContextCancellation?.Dispose();
        _simpleMaterialContextCancellation = null;
        _simpleMaterialPageCache.Clear();
        _simpleMaterialVisiblePageOffsets.Clear();

        lock (_simpleMaterialInFlightGate)
        {
            _simpleMaterialInFlight.Clear();
        }

        _simpleMaterialWindowStartOffset = 0;
        _simpleMaterialWindowEndOffset = 0;
        _simpleMaterialCurrentPageOffset = 0;
        _simpleMaterialsFilteredTotalCount = 0;
        _simpleMaterialSourceTotalCount = 0;
        SimpleMaterials.Clear();
        SelectedMaterial = null;
        NotifySimpleMaterialsStateChanged();
    }

    private async Task<LibraryManagementMaterialsPageDto?> GetSimpleMaterialPageAsync(
        int offset,
        int loadVersion,
        CancellationToken cancellationToken,
        bool reportFailure)
    {
        if (loadVersion != _simpleMaterialLoadVersion || SelectedFolder is null)
        {
            return null;
        }

        if (_simpleMaterialPageCache.TryGet(offset, out IReadOnlyList<LibraryManagementMaterialOverviewDto> cached))
        {
            return new LibraryManagementMaterialsPageDto(
                cached,
                offset + cached.Count,
                offset + cached.Count < _simpleMaterialsFilteredTotalCount,
                _simpleMaterialsFilteredTotalCount,
                _simpleMaterialSourceTotalCount);
        }

        Task<LibraryManagementMaterialsPageDto?> task;

        lock (_simpleMaterialInFlightGate)
        {
            if (!_simpleMaterialInFlight.TryGetValue(offset, out task!))
            {
                Guid folderId = SelectedFolder.Id;
                string? search = SimpleMaterialSearchText;
                LibraryManagementMaterialFilter filter = SimpleMaterialFilter;
                LibraryManagementMaterialSort sort = SelectedSimpleMaterialSortOption.Sort;

                task = QuerySimpleMaterialPageAsync(
                    folderId,
                    search,
                    filter,
                    sort,
                    offset,
                    cancellationToken,
                    reportFailure);

                _simpleMaterialInFlight[offset] = task;
            }
        }

        try
        {
            LibraryManagementMaterialsPageDto? page = await task;

            if (page is null || loadVersion != _simpleMaterialLoadVersion || cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            _simpleMaterialPageCache.Set(offset, page.Items);
            return page;
        }
        finally
        {
            lock (_simpleMaterialInFlightGate)
            {
                if (_simpleMaterialInFlight.TryGetValue(offset, out Task<LibraryManagementMaterialsPageDto?>? current) &&
                    ReferenceEquals(current, task))
                {
                    _simpleMaterialInFlight.Remove(offset);
                }
            }
        }
    }

    private async Task<LibraryManagementMaterialsPageDto?> QuerySimpleMaterialPageAsync(
        Guid folderId,
        string? search,
        LibraryManagementMaterialFilter filter,
        LibraryManagementMaterialSort sort,
        int offset,
        CancellationToken cancellationToken,
        bool reportFailure)
    {
        try
        {
            var query = new GetLibraryManagementMaterialsPageQuery(
                folderId,
                search,
                ToMaterialPageFilter(filter),
                ToMaterialPageSort(sort),
                offset,
                SIMPLE_MATERIAL_PAGE_SIZE);

            var result = await queryDispatcher.SendAsync<
                GetLibraryManagementMaterialsPageQuery,
                LibraryManagementMaterialsPageDto>(
                query,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            if (result.IsFailure)
            {
                if (reportFailure)
                {
                    ErrorMessage = result.Error.FirstOrDefault()?.Message
                                   ?? "Не удалось загрузить материалы";
                }

                return null;
            }

            return result.Value;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось загрузить страницу материалов {Offset}", offset);

            if (reportFailure)
            {
                ErrorMessage = "Не удалось загрузить материалы";
            }

            return null;
        }
    }

    private void ApplyMaterialPageTotals(LibraryManagementMaterialsPageDto page)
    {
        _simpleMaterialsFilteredTotalCount = page.TotalCount;
        _simpleMaterialSourceTotalCount = page.SourceTotalCount;
    }

    private void AddVisibleMaterialPageOffset(int offset, bool append)
    {
        if (_simpleMaterialVisiblePageOffsets.Contains(offset))
        {
            return;
        }

        if (append)
        {
            _simpleMaterialVisiblePageOffsets.AddLast(offset);
        }
        else
        {
            _simpleMaterialVisiblePageOffsets.AddFirst(offset);
        }
    }

    private void TrimVisibleMaterialPagesFromStart()
    {
        while (_simpleMaterialVisiblePageOffsets.Count > SIMPLE_MATERIAL_VISIBLE_PAGE_LIMIT)
        {
            _simpleMaterialVisiblePageOffsets.RemoveFirst();
        }
    }

    private void TrimVisibleMaterialPagesFromEnd()
    {
        while (_simpleMaterialVisiblePageOffsets.Count > SIMPLE_MATERIAL_VISIBLE_PAGE_LIMIT)
        {
            _simpleMaterialVisiblePageOffsets.RemoveLast();
        }
    }

    private void RebuildSimpleMaterialWindow()
    {
        SimpleMaterials.Clear();

        int position = _simpleMaterialVisiblePageOffsets.First?.Value + 1 ?? 1;
        int? firstOffset = null;
        int endOffset = 0;

        foreach (int offset in _simpleMaterialVisiblePageOffsets)
        {
            if (!_simpleMaterialPageCache.TryGet(
                    offset,
                    out IReadOnlyList<LibraryManagementMaterialOverviewDto> page))
            {
                continue;
            }

            firstOffset ??= offset;

            foreach (LibraryManagementMaterialOverviewDto material in page)
            {
                SimpleMaterials.Add(new LibraryManagementOrderItemViewModel(material, position++));
            }

            endOffset = Math.Max(endOffset, offset + page.Count);
        }

        _simpleMaterialWindowStartOffset = firstOffset ?? 0;
        _simpleMaterialWindowEndOffset = endOffset;
    }

    public void UpdateSimpleMaterialViewport(double verticalOffset)
    {
        int pageOffset = LibraryRangeTextFormatter.GetPageStartOffset(
            _simpleMaterialWindowStartOffset,
            verticalOffset,
            SIMPLE_MATERIAL_PAGE_SIZE);

        if (_simpleMaterialsFilteredTotalCount > 0)
        {
            int lastPageOffset =
                (_simpleMaterialsFilteredTotalCount - 1) / SIMPLE_MATERIAL_PAGE_SIZE * SIMPLE_MATERIAL_PAGE_SIZE;
            pageOffset = Math.Min(pageOffset, lastPageOffset);
        }

        if (_simpleMaterialCurrentPageOffset == pageOffset)
        {
            return;
        }

        _simpleMaterialCurrentPageOffset = pageOffset;
        OnPropertyChanged(nameof(SimpleMaterialCurrentPageOffset));
        OnPropertyChanged(nameof(SimpleMaterialsShownCountText));
    }

    private string FormatSimpleMaterialRangeText()
    {
        int visibleCount = Math.Min(
            SIMPLE_MATERIAL_PAGE_SIZE,
            Math.Max(0, _simpleMaterialsFilteredTotalCount - _simpleMaterialCurrentPageOffset));

        return LibraryRangeTextFormatter.Format(
            _simpleMaterialCurrentPageOffset,
            visibleCount,
            _simpleMaterialsFilteredTotalCount,
            !string.IsNullOrWhiteSpace(SimpleMaterialSearchText));
    }

    private static LibraryManagementFolderPageSort ToFolderPageSort(LibraryManagementFolderSort sort) =>
        sort switch
        {
            LibraryManagementFolderSort.CUSTOM => LibraryManagementFolderPageSort.CUSTOM,
            LibraryManagementFolderSort.RECENT_ACTIVITY => LibraryManagementFolderPageSort.RECENT_ACTIVITY,
            LibraryManagementFolderSort.NAME => LibraryManagementFolderPageSort.NAME,
            LibraryManagementFolderSort.NEWEST => LibraryManagementFolderPageSort.NEWEST,
            _ => LibraryManagementFolderPageSort.CUSTOM,
        };

    private static LibraryManagementMaterialPageFilter ToMaterialPageFilter(LibraryManagementMaterialFilter filter) =>
        filter switch
        {
            LibraryManagementMaterialFilter.ARTICLES => LibraryManagementMaterialPageFilter.ARTICLES,
            LibraryManagementMaterialFilter.QUESTIONS => LibraryManagementMaterialPageFilter.QUESTIONS,
            _ => LibraryManagementMaterialPageFilter.ALL,
        };

    private static LibraryManagementMaterialPageSort ToMaterialPageSort(LibraryManagementMaterialSort sort) =>
        sort switch
        {
            LibraryManagementMaterialSort.CUSTOM => LibraryManagementMaterialPageSort.CUSTOM,
            LibraryManagementMaterialSort.RECENT_ACTIVITY => LibraryManagementMaterialPageSort.RECENT_ACTIVITY,
            LibraryManagementMaterialSort.NAME => LibraryManagementMaterialPageSort.NAME,
            LibraryManagementMaterialSort.NEWEST => LibraryManagementMaterialPageSort.NEWEST,
            _ => LibraryManagementMaterialPageSort.CUSTOM,
        };

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

}
