using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Library.GetSectionsPage;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class LibraryOverviewViewModel : ViewModelBase
{
    private const int PageSize = 30;
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(350);

    private readonly IQueryDispatcher _queryDispatcher;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<LibraryOverviewViewModel> _logger;

    private CancellationToken _viewCancellationToken;
    private int _nextOffset;
    private int _loadVersion;
    private int _searchVersion;
    private int? _loadingVersion;
    private bool _isLoaded;
    private bool _isViewModeLoaded;

    public LibraryOverviewViewModel(
        IQueryDispatcher queryDispatcher,
        ISettingsService settingsService,
        ILogger<LibraryOverviewViewModel> logger)
    {
        _queryDispatcher = queryDispatcher;
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

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsTableView))] [NotifyPropertyChangedFor(nameof(IsTilesView))]
    private LibraryViewMode _viewMode = LibraryViewMode.Tiles;

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

    public bool IsTableView => ViewMode == LibraryViewMode.Table;

    public bool IsTilesView => ViewMode == LibraryViewMode.Tiles;

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
        return SetViewModeAsync(LibraryViewMode.Table, cancellationToken);
    }

    [RelayCommand]
    private Task ShowTilesViewAsync(CancellationToken cancellationToken)
    {
        return SetViewModeAsync(LibraryViewMode.Tiles, cancellationToken);
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
        HasMore = true;
        ErrorMessage = null;
        NextPageErrorMessage = null;

        Sections.Clear();
        SectionRows.Clear();

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
        var row = SectionRows.LastOrDefault();

        if (row is null || row.IsFull)
        {
            row = new LibrarySectionRowViewModel();
            SectionRows.Add(row);
        }

        row.Add(section);
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

            ViewMode = settings.LibraryViewMode;
            _isViewModeLoaded = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Не удалось загрузить режим просмотра разделов");

            ViewMode = LibraryViewMode.Tiles;
            _isViewModeLoaded = true;
        }
    }

    private async Task SetViewModeAsync(
        LibraryViewMode viewMode,
        CancellationToken cancellationToken)
    {
        if (ViewMode == viewMode)
        {
            return;
        }

        ViewMode = viewMode;

        try
        {
            await _settingsService.SaveLibraryViewModeAsync(viewMode, cancellationToken);
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
                viewMode);
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