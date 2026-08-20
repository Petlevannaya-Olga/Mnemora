using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Library.GetManagementMaterialsPage;
using Mnemora.Application.Library.GetManagementSectionsPage;
using Mnemora.Application.Library.GetManagementTopicsPage;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class LibraryManagementViewModel
{
    private const int SimpleSectionVisiblePageLimit = 7;
    private const int SimpleSectionCachePageLimit = 10;
    private const int SimpleTopicPageSize = LibraryPagingDefaults.PageSize;
    private const int SimpleTopicVisiblePageLimit = 7;
    private const int SimpleTopicCachePageLimit = 10;
    private const int SimpleMaterialVisiblePageLimit = 7;
    private const int SimpleMaterialCachePageLimit = 10;

    private readonly BoundedPagedWindow<LibrarySectionOverviewDto> _simpleSectionWindow =
        new(SimpleSectionPageSize, SimpleSectionVisiblePageLimit, SimpleSectionCachePageLimit);

    private readonly Dictionary<int, Task<LibraryManagementSectionsPageDto?>> _simpleSectionInFlight = [];
    private readonly object _simpleSectionInFlightGate = new();

    private readonly BoundedPagedWindow<LibraryManagementTopicOverviewDto> _simpleTopicWindow =
        new(SimpleTopicPageSize, SimpleTopicVisiblePageLimit, SimpleTopicCachePageLimit);

    private readonly Dictionary<int, Task<LibraryManagementTopicsPageDto?>> _simpleTopicInFlight = [];
    private readonly object _simpleTopicInFlightGate = new();

    private readonly BoundedPageCache<LibraryManagementMaterialOverviewDto> _simpleMaterialPageCache =
        new(SimpleMaterialCachePageLimit);

    private readonly LinkedList<int> _simpleMaterialVisiblePageOffsets = [];
    private readonly Dictionary<int, Task<LibraryManagementMaterialsPageDto?>> _simpleMaterialInFlight = [];
    private readonly object _simpleMaterialInFlightGate = new();

    private bool _isSimpleSectionsLoadingPreviousPage;
    private CancellationTokenSource? _simpleSectionContextCancellation;

    private int _simpleTopicsTotalCount;
    private int _simpleTopicSourceTotalCount;
    private int _simpleTopicLoadVersion;
    private bool _isSimpleTopicsLoadingNextPage;
    private bool _isSimpleTopicsLoadingPreviousPage;
    private CancellationTokenSource? _simpleTopicContextCancellation;

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

    public bool SimpleTopicsHasMore =>
        _simpleTopicWindow.HasNext &&
        !_isSimpleTopicsLoadingNextPage;

    public bool SimpleTopicsHasPrevious =>
        _simpleTopicWindow.HasPrevious &&
        !_isSimpleTopicsLoadingPreviousPage;

    public bool IsSimpleTopicsLoadingNextPage => _isSimpleTopicsLoadingNextPage;
    public bool IsSimpleTopicsLoadingPreviousPage => _isSimpleTopicsLoadingPreviousPage;
    public bool IsSimpleTopicsPaging =>
        _isSimpleTopicsLoadingNextPage ||
        _isSimpleTopicsLoadingPreviousPage;
    public int SimpleTopicsTotalCount => _simpleTopicsTotalCount;
    public int SimpleTopicWindowStartOffset => _simpleTopicWindow.WindowStartOffset;
    public int SimpleTopicWindowEndOffset => _simpleTopicWindow.WindowEndOffset;
    public int SimpleTopicCurrentPageOffset => _simpleTopicWindow.CurrentPageOffset;
    public int SimpleTopicCachedPageCount => _simpleTopicWindow.CachedPageCount;
    public int SimpleTopicCachedItemUpperBound => _simpleTopicWindow.CachedItemUpperBound;

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
    public int SimpleMaterialCachedItemUpperBound => SimpleMaterialCachePageLimit * SimpleMaterialPageSize;

    public async Task LoadNextSimpleSectionWindowAsync(CancellationToken cancellationToken = default)
    {
        if (!CanLoadNextSimpleSectionPage())
        {
            return;
        }

        int loadVersion = _simpleSectionLoadVersion;
        int offset = _simpleSectionWindow.NextOffset;
        IsSimpleSectionsLoadingNextPage = true;
        SimpleSectionsNextPageErrorMessage = null;
        NotifySimpleSectionsStateChanged();

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _simpleSectionContextCancellation?.Token ?? _viewCancellationToken,
                cancellationToken);

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

            _simpleSectionWindow.ShowPage(offset, page.Items, PageWindowInsert.Append);
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
            _isSimpleSectionsLoadingPreviousPage)
        {
            return;
        }

        int loadVersion = _simpleSectionLoadVersion;
        int offset = _simpleSectionWindow.PreviousOffset;
        _isSimpleSectionsLoadingPreviousPage = true;
        OnPropertyChanged(nameof(IsSimpleSectionsLoadingPreviousPage));
        NotifySimpleSectionsStateChanged();

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _simpleSectionContextCancellation?.Token ?? _viewCancellationToken,
                cancellationToken);

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
            _simpleSectionWindow.ShowPage(offset, page.Items, PageWindowInsert.Prepend);
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
                SimpleSectionPageSize);

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

    private async Task ReloadSimpleTopicsPagedAsync(CancellationToken cancellationToken)
    {
        if (SelectedSection is null)
        {
            ClearSimpleTopicsPagedState();
            return;
        }

        int loadVersion = ResetSimpleTopicPagingState(cancellationToken);
        IsContextLoading = true;
        ErrorMessage = null;

        try
        {
            LibraryManagementTopicsPageDto? page = await GetSimpleTopicPageAsync(
                offset: 0,
                loadVersion,
                _simpleTopicContextCancellation!.Token,
                reportFailure: true);

            if (page is null || loadVersion != _simpleTopicLoadVersion)
            {
                return;
            }

            ApplyTopicPageTotals(page);
            _simpleTopicWindow.ShowPage(0, page.Items, PageWindowInsert.Append);
            RebuildSimpleTopicWindow();
            NotifySimpleTopicsStateChanged();
        }
        finally
        {
            if (loadVersion == _simpleTopicLoadVersion)
            {
                IsContextLoading = false;
                NotifySimpleTopicsStateChanged();
            }
        }
    }

    public async Task LoadNextSimpleTopicWindowAsync(CancellationToken cancellationToken = default)
    {
        if (!CanLoadNextSimpleTopicPage() || SelectedSection is null)
        {
            return;
        }

        int loadVersion = _simpleTopicLoadVersion;
        int offset = _simpleTopicWindow.NextOffset;
        _isSimpleTopicsLoadingNextPage = true;
        OnPropertyChanged(nameof(IsSimpleTopicsLoadingNextPage));
        NotifySimpleTopicsStateChanged();

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _simpleTopicContextCancellation?.Token ?? _viewCancellationToken,
                cancellationToken);

            LibraryManagementTopicsPageDto? page = await GetSimpleTopicPageAsync(
                offset,
                loadVersion,
                linked.Token,
                reportFailure: true);

            if (page is null || loadVersion != _simpleTopicLoadVersion)
            {
                return;
            }

            ApplyTopicPageTotals(page);

            if (page.Items.Count == 0)
            {
                return;
            }

            _simpleTopicWindow.ShowPage(offset, page.Items, PageWindowInsert.Append);
            RebuildSimpleTopicWindow();
            NotifySimpleTopicsStateChanged();
        }
        finally
        {
            if (loadVersion == _simpleTopicLoadVersion)
            {
                _isSimpleTopicsLoadingNextPage = false;
                OnPropertyChanged(nameof(IsSimpleTopicsLoadingNextPage));
                LoadNextSimpleTopicPageCommand.NotifyCanExecuteChanged();
                NotifySimpleTopicsStateChanged();
            }
        }
    }

    public async Task LoadPreviousSimpleTopicWindowAsync(CancellationToken cancellationToken = default)
    {
        if (!SimpleTopicsHasPrevious ||
            IsContextLoading ||
            _isSimpleTopicsLoadingPreviousPage ||
            SelectedSection is null)
        {
            return;
        }

        int loadVersion = _simpleTopicLoadVersion;
        int offset = _simpleTopicWindow.PreviousOffset;
        _isSimpleTopicsLoadingPreviousPage = true;
        OnPropertyChanged(nameof(IsSimpleTopicsLoadingPreviousPage));
        NotifySimpleTopicsStateChanged();

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _simpleTopicContextCancellation?.Token ?? _viewCancellationToken,
                cancellationToken);

            LibraryManagementTopicsPageDto? page = await GetSimpleTopicPageAsync(
                offset,
                loadVersion,
                linked.Token,
                reportFailure: true);

            if (page is null || loadVersion != _simpleTopicLoadVersion || page.Items.Count == 0)
            {
                return;
            }

            ApplyTopicPageTotals(page);
            _simpleTopicWindow.ShowPage(offset, page.Items, PageWindowInsert.Prepend);
            RebuildSimpleTopicWindow();
            NotifySimpleTopicsStateChanged();
        }
        finally
        {
            if (loadVersion == _simpleTopicLoadVersion)
            {
                _isSimpleTopicsLoadingPreviousPage = false;
                OnPropertyChanged(nameof(IsSimpleTopicsLoadingPreviousPage));
                NotifySimpleTopicsStateChanged();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadNextSimpleTopicPage))]
    private Task LoadNextSimpleTopicPageAsync(CancellationToken cancellationToken) =>
        LoadNextSimpleTopicWindowAsync(cancellationToken);

    private bool CanLoadNextSimpleTopicPage() =>
        IsSimpleTopicsPage &&
        _simpleTopicWindow.HasNext &&
        !_isSimpleTopicsLoadingNextPage &&
        !IsContextLoading &&
        !HasError;

    private int ResetSimpleTopicPagingState(CancellationToken cancellationToken)
    {
        int loadVersion = Interlocked.Increment(ref _simpleTopicLoadVersion);

        _simpleTopicContextCancellation?.Cancel();
        _simpleTopicContextCancellation?.Dispose();
        _simpleTopicContextCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _viewCancellationToken,
            cancellationToken);

        _simpleTopicWindow.Reset();

        lock (_simpleTopicInFlightGate)
        {
            _simpleTopicInFlight.Clear();
        }

        _simpleTopicsTotalCount = 0;
        _simpleTopicSourceTotalCount = SelectedSection?.SectionOverview?.TopicsCount
                                      ?? SelectedSection?.Section?.Topics.Count
                                      ?? 0;
        _isSimpleTopicsLoadingNextPage = false;
        _isSimpleTopicsLoadingPreviousPage = false;
        SimpleTopics.Clear();
        SimpleTopicRows.Clear();
        SimpleCompactTopicRows.Clear();
        NotifySimpleTopicsStateChanged();

        return loadVersion;
    }

    private void ClearSimpleTopicsPagedState()
    {
        Interlocked.Increment(ref _simpleTopicLoadVersion);
        _simpleTopicContextCancellation?.Cancel();
        _simpleTopicContextCancellation?.Dispose();
        _simpleTopicContextCancellation = null;
        _simpleTopicWindow.Reset();

        lock (_simpleTopicInFlightGate)
        {
            _simpleTopicInFlight.Clear();
        }

        _simpleTopicsTotalCount = 0;
        _simpleTopicSourceTotalCount = 0;
        _isSimpleTopicsLoadingNextPage = false;
        _isSimpleTopicsLoadingPreviousPage = false;
        SimpleTopics.Clear();
        SimpleTopicRows.Clear();
        SimpleCompactTopicRows.Clear();
        NotifySimpleTopicsStateChanged();
    }

    private async Task<LibraryManagementTopicsPageDto?> GetSimpleTopicPageAsync(
        int offset,
        int loadVersion,
        CancellationToken cancellationToken,
        bool reportFailure)
    {
        if (loadVersion != _simpleTopicLoadVersion || SelectedSection is null)
        {
            return null;
        }

        if (_simpleTopicWindow.TryGetCachedPage(
                offset,
                out IReadOnlyList<LibraryManagementTopicOverviewDto> cached))
        {
            return new LibraryManagementTopicsPageDto(
                cached,
                offset + cached.Count,
                offset + cached.Count < _simpleTopicsTotalCount,
                _simpleTopicsTotalCount);
        }

        Task<LibraryManagementTopicsPageDto?> task;

        lock (_simpleTopicInFlightGate)
        {
            if (!_simpleTopicInFlight.TryGetValue(offset, out task!))
            {
                Guid sectionId = SelectedSection.Id;
                string? search = SimpleTopicSearchText;
                LibraryManagementTopicSort sort = SelectedSimpleTopicSortOption.Sort;

                task = QuerySimpleTopicPageAsync(
                    sectionId,
                    search,
                    sort,
                    offset,
                    cancellationToken,
                    reportFailure);

                _simpleTopicInFlight[offset] = task;
            }
        }

        try
        {
            LibraryManagementTopicsPageDto? page = await task;

            if (page is null ||
                loadVersion != _simpleTopicLoadVersion ||
                cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            _simpleTopicWindow.StorePage(offset, page.Items);
            return page;
        }
        finally
        {
            lock (_simpleTopicInFlightGate)
            {
                if (_simpleTopicInFlight.TryGetValue(
                        offset,
                        out Task<LibraryManagementTopicsPageDto?>? current) &&
                    ReferenceEquals(current, task))
                {
                    _simpleTopicInFlight.Remove(offset);
                }
            }
        }
    }

    private async Task<LibraryManagementTopicsPageDto?> QuerySimpleTopicPageAsync(
        Guid sectionId,
        string? search,
        LibraryManagementTopicSort sort,
        int offset,
        CancellationToken cancellationToken,
        bool reportFailure)
    {
        try
        {
            var query = new GetLibraryManagementTopicsPageQuery(
                sectionId,
                search,
                ToTopicPageSort(sort),
                offset,
                SimpleTopicPageSize);

            var result = await queryDispatcher.SendAsync<
                GetLibraryManagementTopicsPageQuery,
                LibraryManagementTopicsPageDto>(
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
                                   ?? "Не удалось загрузить темы";
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
            logger.LogError(exception, "Не удалось загрузить страницу тем {Offset}", offset);

            if (reportFailure)
            {
                ErrorMessage = "Не удалось загрузить темы";
            }

            return null;
        }
    }

    private void ApplyTopicPageTotals(LibraryManagementTopicsPageDto page)
    {
        _simpleTopicsTotalCount = page.TotalCount;
        _simpleTopicWindow.SetTotalCount(page.TotalCount);

        if (string.IsNullOrWhiteSpace(SimpleTopicSearchText))
        {
            _simpleTopicSourceTotalCount = page.TotalCount;
        }
    }

    private void RebuildSimpleTopicWindow()
    {
        SimpleTopics.Clear();
        SimpleTopicRows.Clear();
        SimpleCompactTopicRows.Clear();

        foreach (int offset in _simpleTopicWindow.VisibleOffsets)
        {
            if (!_simpleTopicWindow.TryGetCachedPage(
                    offset,
                    out IReadOnlyList<LibraryManagementTopicOverviewDto> page))
            {
                continue;
            }

            int position = offset + 1;

            foreach (LibraryManagementTopicOverviewDto topic in page)
            {
                SimpleTopics.Add(new LibraryManagementOrderItemViewModel(topic, position++));
            }
        }

        RebuildSimpleTopicRows(SimpleTopicRows, capacity: 3);
        RebuildSimpleTopicRows(SimpleCompactTopicRows, capacity: 4);
    }

    private void RebuildSimpleTopicRows(
        ObservableCollection<LibraryManagementTopicRowViewModel> rows,
        int capacity)
    {
        bool showCreateTile = _simpleTopicWindow.WindowStartOffset == 0;
        int index = 0;

        while (index < SimpleTopics.Count)
        {
            bool isFirstRow = rows.Count == 0 && showCreateTile;
            int rowCapacity = isFirstRow ? Math.Max(1, capacity - 1) : capacity;
            var row = new LibraryManagementTopicRowViewModel(rowCapacity, isFirstRow);

            for (int count = 0; count < rowCapacity && index < SimpleTopics.Count; count++, index++)
            {
                row.Add(SimpleTopics[index]);
            }

            rows.Add(row);
        }
    }

    public void UpdateSimpleTopicViewport(double logicalItemOffset)
    {
        if (!_simpleTopicWindow.UpdateViewport(logicalItemOffset))
        {
            return;
        }

        OnPropertyChanged(nameof(SimpleTopicCurrentPageOffset));
        OnPropertyChanged(nameof(SimpleTopicsShownCountText));
    }

    private async Task ReloadSimpleMaterialsPagedAsync(CancellationToken cancellationToken)
    {
        if (SelectedTopic is null)
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
        if (!CanLoadNextSimpleMaterialPage() || SelectedTopic is null)
        {
            return;
        }

        int loadVersion = _simpleMaterialLoadVersion;
        int offset = _simpleMaterialWindowEndOffset;
        _isSimpleMaterialsLoadingNextPage = true;
        NotifySimpleMaterialsStateChanged();

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _simpleMaterialContextCancellation?.Token ?? _viewCancellationToken,
                cancellationToken);

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
            _isSimpleMaterialsLoadingPreviousPage ||
            SelectedTopic is null)
        {
            return;
        }

        int loadVersion = _simpleMaterialLoadVersion;
        int firstOffset = _simpleMaterialVisiblePageOffsets.First?.Value ?? 0;
        int offset = Math.Max(0, firstOffset - SimpleMaterialPageSize);
        _isSimpleMaterialsLoadingPreviousPage = true;
        NotifySimpleMaterialsStateChanged();

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _simpleMaterialContextCancellation?.Token ?? _viewCancellationToken,
                cancellationToken);

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
        if (loadVersion != _simpleMaterialLoadVersion || SelectedTopic is null)
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
                Guid topicId = SelectedTopic.Id;
                string? search = SimpleMaterialSearchText;
                LibraryManagementMaterialFilter filter = SimpleMaterialFilter;
                LibraryManagementMaterialSort sort = SelectedSimpleMaterialSortOption.Sort;

                task = QuerySimpleMaterialPageAsync(
                    topicId,
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
        Guid topicId,
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
                topicId,
                search,
                ToMaterialPageFilter(filter),
                ToMaterialPageSort(sort),
                offset,
                SimpleMaterialPageSize);

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
        while (_simpleMaterialVisiblePageOffsets.Count > SimpleMaterialVisiblePageLimit)
        {
            _simpleMaterialVisiblePageOffsets.RemoveFirst();
        }
    }

    private void TrimVisibleMaterialPagesFromEnd()
    {
        while (_simpleMaterialVisiblePageOffsets.Count > SimpleMaterialVisiblePageLimit)
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
            SimpleMaterialPageSize);

        if (_simpleMaterialsFilteredTotalCount > 0)
        {
            int lastPageOffset =
                (_simpleMaterialsFilteredTotalCount - 1) / SimpleMaterialPageSize * SimpleMaterialPageSize;
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
            SimpleMaterialPageSize,
            Math.Max(0, _simpleMaterialsFilteredTotalCount - _simpleMaterialCurrentPageOffset));

        return LibraryRangeTextFormatter.Format(
            _simpleMaterialCurrentPageOffset,
            visibleCount,
            _simpleMaterialsFilteredTotalCount,
            !string.IsNullOrWhiteSpace(SimpleMaterialSearchText));
    }

    private static LibraryManagementTopicPageSort ToTopicPageSort(LibraryManagementTopicSort sort) =>
        sort switch
        {
            LibraryManagementTopicSort.Custom => LibraryManagementTopicPageSort.Custom,
            LibraryManagementTopicSort.RecentActivity => LibraryManagementTopicPageSort.RecentActivity,
            LibraryManagementTopicSort.Name => LibraryManagementTopicPageSort.Name,
            LibraryManagementTopicSort.Newest => LibraryManagementTopicPageSort.Newest,
            _ => LibraryManagementTopicPageSort.Custom,
        };

    private static LibraryManagementMaterialPageFilter ToMaterialPageFilter(LibraryManagementMaterialFilter filter) =>
        filter switch
        {
            LibraryManagementMaterialFilter.Articles => LibraryManagementMaterialPageFilter.Articles,
            LibraryManagementMaterialFilter.Questions => LibraryManagementMaterialPageFilter.Questions,
            _ => LibraryManagementMaterialPageFilter.All,
        };

    private static LibraryManagementMaterialPageSort ToMaterialPageSort(LibraryManagementMaterialSort sort) =>
        sort switch
        {
            LibraryManagementMaterialSort.Custom => LibraryManagementMaterialPageSort.Custom,
            LibraryManagementMaterialSort.RecentActivity => LibraryManagementMaterialPageSort.RecentActivity,
            LibraryManagementMaterialSort.Name => LibraryManagementMaterialPageSort.Name,
            LibraryManagementMaterialSort.Newest => LibraryManagementMaterialPageSort.Newest,
            _ => LibraryManagementMaterialPageSort.Custom,
        };
}
