using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
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
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(350);

    private readonly IQueryDispatcher _queryDispatcher;
    private readonly ISettingsService _settingsService;
    private readonly IPageNavigationService _pageNavigationService;
    private readonly ILogger<LibraryOverviewViewModel> _logger;

    private CancellationToken _viewCancellationToken;
    private int _nextOffset;
    private int _loadVersion;
    private int _searchVersion;
    private int? _loadingVersion;
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
    private bool _isLoading;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(LoadNextPageCommand))]
    private bool _isLoadingNextPage;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(LoadNextPageCommand))]
    private bool _hasMore = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSearchResults))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextPageCommand))]
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
    private LibraryOverviewViewMode _overviewViewMode = LibraryOverviewViewMode.Tiles;

    public bool HasSections => Sections.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasNextPageError => !string.IsNullOrWhiteSpace(NextPageErrorMessage);

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

    [RelayCommand]
    private async Task RetryNextPageAsync(CancellationToken cancellationToken)
    {
        NextPageErrorMessage = null;

        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _viewCancellationToken,
            cancellationToken);

        await LoadPageAsync(_loadVersion, linkedCancellationTokenSource.Token);
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
    private void OpenSection(LibrarySectionCardViewModel? section)
    {
        if (section is null)
        {
            return;
        }

        _pageNavigationService.NavigateTo<LibrarySectionViewModel>(
            viewModel => viewModel.Initialize(section.Id));
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

    private bool CanLoadNextPage()
    {
        return _isLoaded &&
               HasMore &&
               !IsLoading &&
               !IsLoadingNextPage &&
               !HasError &&
               !HasNextPageError;
    }

    private async Task LoadNextPageWithLinkedCancellationAsync(CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _viewCancellationToken,
            cancellationToken);

        await LoadPageAsync(_loadVersion, linkedCancellationTokenSource.Token);
    }

    private async Task ReloadCoreAsync(CancellationToken cancellationToken)
    {
        int loadVersion = Interlocked.Increment(ref _loadVersion);

        _nextOffset = 0;
        CurrentPageOffset = 0;
        TotalCount = 0;
        HasMore = true;
        ErrorMessage = null;
        NextPageErrorMessage = null;

        Sections.Clear();
        SectionRows.Clear();
        CompactSectionRows.Clear();

        NotifyCollectionStateChanged();

        await LoadPageAsync(loadVersion, cancellationToken);
    }

    private async Task LoadPageAsync(int loadVersion, CancellationToken cancellationToken)
    {
        if (loadVersion != _loadVersion ||
            _loadingVersion == loadVersion ||
            !HasMore ||
            cancellationToken.IsCancellationRequested)
        {
            return;
        }

        _loadingVersion = loadVersion;

        bool isInitialPage = _nextOffset == 0;

        if (isInitialPage)
        {
            IsLoading = true;
            ErrorMessage = null;
        }
        else
        {
            IsLoadingNextPage = true;
            NextPageErrorMessage = null;
        }

        try
        {
            var query = new GetLibrarySectionsPageQuery(
                SearchText,
                SelectedSortOption.Sort,
                _nextOffset,
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
                var message = result.Error.FirstOrDefault()?.Message
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

            foreach (var section in result.Value.Items)
            {
                var sectionViewModel = new LibrarySectionCardViewModel(section);

                Sections.Add(sectionViewModel);
                AddToSectionRows(sectionViewModel);
            }

            _nextOffset = result.Value.NextOffset;
            HasMore = result.Value.HasMore;
            TotalCount = result.Value.TotalCount;

            NotifyCollectionStateChanged();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // ignore
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось загрузить страницу разделов");

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
                NextPageErrorMessage = "Не удалось загрузить следующую порцию разделов";
            }
        }
        finally
        {
            if (_loadingVersion == loadVersion)
            {
                _loadingVersion = null;
            }

            if (loadVersion == _loadVersion)
            {
                IsLoading = false;
                IsLoadingNextPage = false;
                LoadNextPageCommand.NotifyCanExecuteChanged();
                NotifyCollectionStateChanged();
            }
        }
    }

    private void AddToSectionRows(LibrarySectionCardViewModel section)
    {
        AddToRows(SectionRows, section, 3);
        AddToRows(CompactSectionRows, section, 5);
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
        int pageOffset = LibraryRangeTextFormatter.GetPageStartOffset(
            windowStartOffset: 0,
            verticalOffset: logicalItemOffset,
            pageSize: PageSize);

        if (TotalCount > 0)
        {
            int lastPageOffset = (TotalCount - 1) / PageSize * PageSize;
            pageOffset = Math.Min(pageOffset, lastPageOffset);
        }

        CurrentPageOffset = pageOffset;
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
