using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Library.GetTopicsPage;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.ViewModels.Common;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class LibrarySectionViewModel(
    IQueryDispatcher queryDispatcher,
    IPageNavigationService pageNavigationService,
    ILogger<LibrarySectionViewModel> logger)
    : ViewModelBase
{
    private const int PageSize = 30;
    private const int TopicsPerRow = 3;
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(350);

    private CancellationToken _viewCancellationToken;
    private Guid _sectionId;
    private int _nextOffset;
    private int _loadVersion;
    private int _searchVersion;
    private int? _loadingVersion;
    private bool _isLoaded;

    public ObservableCollection<LibraryTopicCardViewModel> Topics { get; } = [];

    public ObservableCollection<LibraryTopicRowViewModel> TopicRows { get; } = [];

    public IReadOnlyList<LibraryTopicSortOption> SortOptions { get; } =
    [
        new("Последняя активность", LibraryTopicSort.RecentActivity),
        new("По названию", LibraryTopicSort.Name),
        new("Сначала новые", LibraryTopicSort.Newest)
    ];

    [ObservableProperty, NotifyPropertyChangedFor(nameof(SectionTitle))]
    private LibrarySectionHeaderDto? _section;

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
    private LibraryTopicSortOption _selectedSortOption = new(
        "Последняя активность",
        LibraryTopicSort.RecentActivity);

    public string SectionTitle => Section?.Name ?? "Раздел";

    public bool HasTopics => Topics.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasNextPageError => !string.IsNullOrWhiteSpace(NextPageErrorMessage);

    public bool IsEmpty =>
        !IsLoading &&
        !HasError &&
        !HasTopics &&
        string.IsNullOrWhiteSpace(SearchText);

    public bool HasNoSearchResults =>
        !IsLoading &&
        !HasError &&
        !HasTopics &&
        !string.IsNullOrWhiteSpace(SearchText);

    public void Initialize(Guid sectionId)
    {
        if (sectionId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор раздела не может быть пустым.", nameof(sectionId));
        }

        if (_isLoaded)
        {
            throw new InvalidOperationException("Нельзя изменить раздел после начала загрузки.");
        }

        _sectionId = sectionId;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_sectionId == Guid.Empty)
        {
            throw new InvalidOperationException("Раздел не был инициализирован.");
        }

        _viewCancellationToken = cancellationToken;
        _isLoaded = true;

        LoadNextPageCommand.NotifyCanExecuteChanged();

        await ReloadCoreAsync(cancellationToken);
    }

    [RelayCommand]
    private void NavigateBack()
    {
        pageNavigationService.NavigateTo<LibraryOverviewViewModel>();
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
    private void OpenTopic(LibraryTopicCardViewModel? topic)
    {
        if (topic is null)
        {
            return;
        }

        pageNavigationService.NavigateTo<LibraryTopicViewModel>(
            viewModel => viewModel.Initialize(topic.Id));
    }

    partial void OnSearchTextChanged(string? value)
    {
        int searchVersion = Interlocked.Increment(ref _searchVersion);

        if (_isLoaded)
        {
            _ = ReloadAfterSearchDelayAsync(searchVersion);
        }
    }

    partial void OnSelectedSortOptionChanged(LibraryTopicSortOption value)
    {
        if (_isLoaded)
        {
            _ = ReloadAfterSortChangedAsync();
        }
    }

    private bool CanLoadNextPage()
    {
        return _isLoaded &&
               _sectionId != Guid.Empty &&
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

        Topics.Clear();
        TopicRows.Clear();

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
            var query = new GetLibraryTopicsPageQuery(
                _sectionId,
                SearchText,
                SelectedSortOption.Sort,
                _nextOffset,
                PageSize);

            var result = await queryDispatcher.SendAsync<
                GetLibraryTopicsPageQuery,
                LibraryTopicsPageDto>(
                query,
                cancellationToken);

            if (loadVersion != _loadVersion || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (result.IsFailure)
            {
                string message = result.Error.FirstOrDefault()?.Message
                                 ?? "Не удалось загрузить темы раздела";

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

            Section = result.Value.Section;

            foreach (var topic in result.Value.Items)
            {
                var topicViewModel = new LibraryTopicCardViewModel(topic);

                Topics.Add(topicViewModel);
                AddToTopicRows(topicViewModel);
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
            logger.LogError(
                exception,
                "Не удалось загрузить страницу тем раздела {SectionId}",
                _sectionId);

            if (loadVersion != _loadVersion)
            {
                return;
            }

            if (isInitialPage)
            {
                ErrorMessage = "Не удалось загрузить темы раздела";
            }
            else
            {
                NextPageErrorMessage = "Не удалось загрузить следующую порцию тем";
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

    private void AddToTopicRows(LibraryTopicCardViewModel topic)
    {
        var row = TopicRows.LastOrDefault();

        if (row is null || row.IsFull)
        {
            row = new LibraryTopicRowViewModel(TopicsPerRow);
            TopicRows.Add(row);
        }

        row.Add(topic);
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

    private void NotifyCollectionStateChanged()
    {
        OnPropertyChanged(nameof(HasTopics));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasNoSearchResults));
    }
}

public sealed record LibraryTopicSortOption(
    string Name,
    LibraryTopicSort Sort);