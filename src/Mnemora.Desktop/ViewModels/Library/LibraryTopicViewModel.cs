using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Library.GetMaterialsPage;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class LibraryTopicViewModel : ViewModelBase
{
    private const int PageSize = 50;
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(350);

    private readonly IQueryDispatcher _queryDispatcher;
    private readonly IPageNavigationService _pageNavigationService;
    private readonly ILogger<LibraryTopicViewModel> _logger;

    private CancellationToken _viewCancellationToken;
    private Guid _topicId;
    private int _nextOffset;
    private int _loadVersion;
    private int _searchVersion;
    private int? _loadingVersion;
    private bool _isLoaded;

    public LibraryTopicViewModel(
        IQueryDispatcher queryDispatcher,
        IPageNavigationService pageNavigationService,
        ILogger<LibraryTopicViewModel> logger)
    {
        _queryDispatcher = queryDispatcher;
        _pageNavigationService = pageNavigationService;
        _logger = logger;

        FilterOptions =
        [
            new("Все", LibraryMaterialFilter.All),
            new("Статьи", LibraryMaterialFilter.Articles),
            new("Вопросы", LibraryMaterialFilter.Questions)
        ];

        SortOptions =
        [
            new("Недавно изменённые", LibraryMaterialSort.RecentlyUpdated),
            new("По названию", LibraryMaterialSort.Name),
            new("Сначала новые", LibraryMaterialSort.Newest),
            new("Сначала простые", LibraryMaterialSort.Easiest),
            new("Сначала сложные", LibraryMaterialSort.Hardest)
        ];

        _selectedFilterOption = FilterOptions[0];
        _selectedSortOption = SortOptions[0];
    }

    public ObservableCollection<LibraryMaterialListItemViewModel> Materials { get; } = [];

    public IReadOnlyList<LibraryMaterialFilterOption> FilterOptions { get; }

    public IReadOnlyList<LibraryMaterialSortOption> SortOptions { get; }
    
    public bool IsAllFilter =>
        SelectedFilterOption.Filter == LibraryMaterialFilter.All;

    public bool IsArticlesFilter =>
        SelectedFilterOption.Filter == LibraryMaterialFilter.Articles;

    public bool IsQuestionsFilter =>
        SelectedFilterOption.Filter == LibraryMaterialFilter.Questions;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(TopicTitle))]
    private LibraryTopicHeaderDto? _topic;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSearchResults))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextPageCommand))]
    private bool _isLoading;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(LoadNextPageCommand))]
    private bool _isLoadingNextPage;

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(LoadNextPageCommand))]
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

    [ObservableProperty]
    private LibraryMaterialFilterOption _selectedFilterOption;

    [ObservableProperty]
    private LibraryMaterialSortOption _selectedSortOption;

    public string TopicTitle => Topic?.Name ?? "Тема";

    public bool HasMaterials => Materials.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasNextPageError => !string.IsNullOrWhiteSpace(NextPageErrorMessage);

    public bool IsEmpty =>
        !IsLoading &&
        !HasError &&
        !HasMaterials &&
        string.IsNullOrWhiteSpace(SearchText) &&
        SelectedFilterOption.Filter == LibraryMaterialFilter.All;

    public bool HasNoSearchResults =>
        !IsLoading &&
        !HasError &&
        !HasMaterials &&
        (!string.IsNullOrWhiteSpace(SearchText) ||
         SelectedFilterOption.Filter != LibraryMaterialFilter.All);

    public void Initialize(Guid topicId)
    {
        if (topicId == Guid.Empty)
        {
            throw new ArgumentException(
                "Идентификатор темы не может быть пустым.",
                nameof(topicId));
        }

        if (_isLoaded)
        {
            throw new InvalidOperationException(
                "Нельзя изменить тему после начала загрузки.");
        }

        _topicId = topicId;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_topicId == Guid.Empty)
        {
            throw new InvalidOperationException("Тема не была инициализирована.");
        }

        _viewCancellationToken = cancellationToken;
        _isLoaded = true;

        LoadNextPageCommand.NotifyCanExecuteChanged();

        await ReloadCoreAsync(cancellationToken);
    }

    [RelayCommand]
    private void NavigateBack()
    {
        if (Topic is null)
        {
            _pageNavigationService.NavigateTo<LibraryOverviewViewModel>();
            return;
        }

        _pageNavigationService.NavigateTo<LibrarySectionViewModel>(
            viewModel => viewModel.Initialize(Topic.SectionId));
    }

    [RelayCommand]
    private void NavigateLibrary()
    {
        _pageNavigationService.NavigateTo<LibraryOverviewViewModel>();
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

        if (_isLoaded)
        {
            _ = ReloadAfterSelectionChangedAsync();
        }
    }

    partial void OnSelectedSortOptionChanged(LibraryMaterialSortOption value)
    {
        if (_isLoaded)
        {
            _ = ReloadAfterSelectionChangedAsync();
        }
    }

    private bool CanLoadNextPage()
    {
        return _isLoaded &&
               _topicId != Guid.Empty &&
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

        Materials.Clear();
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
            var query = new GetLibraryMaterialsPageQuery(
                _topicId,
                SearchText,
                SelectedFilterOption.Filter,
                SelectedSortOption.Sort,
                _nextOffset,
                PageSize);

            var result = await _queryDispatcher.SendAsync<
                GetLibraryMaterialsPageQuery,
                LibraryMaterialsPageDto>(
                query,
                cancellationToken);

            if (loadVersion != _loadVersion || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (result.IsFailure)
            {
                string message = result.Error.FirstOrDefault()?.Message
                                 ?? "Не удалось загрузить материалы темы";

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

            Topic = result.Value.Topic;

            foreach (var material in result.Value.Items)
            {
                Materials.Add(new LibraryMaterialListItemViewModel(material));
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
            _logger.LogError(
                exception,
                "Не удалось загрузить материалы темы {TopicId}",
                _topicId);

            if (loadVersion != _loadVersion)
            {
                return;
            }

            if (isInitialPage)
            {
                ErrorMessage = "Не удалось загрузить материалы темы";
            }
            else
            {
                NextPageErrorMessage = "Не удалось загрузить следующую порцию материалов";
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

    private async Task ReloadAfterSelectionChangedAsync()
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

    private void NotifyCollectionStateChanged()
    {
        OnPropertyChanged(nameof(HasMaterials));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasNoSearchResults));
    }
}

public sealed record LibraryMaterialFilterOption(
    string Name,
    LibraryMaterialFilter Filter);

public sealed record LibraryMaterialSortOption(
    string Name,
    LibraryMaterialSort Sort);