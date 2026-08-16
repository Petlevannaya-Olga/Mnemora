using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Commands;
using Mnemora.Application.Library.Get;
using Mnemora.Application.Library.GetManagementSectionsPage;
using Mnemora.Application.Library.Order;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Desktop.Dialogs;
using Mnemora.Desktop.Notifications;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Desktop.ViewModels.Sections;
using Mnemora.Desktop.ViewModels.Topics;

namespace Mnemora.Desktop.ViewModels.Library;

public enum LibraryManagementSimplePage
{
    Sections,
    Topics,
    Materials,
}

public sealed partial class LibraryManagementViewModel(
    IQueryDispatcher queryDispatcher,
    ICommandDispatcher commandDispatcher,
    IDialogService dialogService,
    ISettingsService settingsService,
    INotificationService notificationService,
    ILogger<LibraryManagementViewModel> logger)
    : ViewModelBase
{
    private readonly List<LibrarySectionDto> _library = [];
    private readonly Dictionary<Guid, Guid[]> _pendingTopicOrders = new();
    private readonly Dictionary<Guid, Guid[]> _pendingMaterialOrders = new();

    private const int SimpleSectionPageSize = 30;
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(350);

    private Guid[]? _pendingSectionOrder;

    private CancellationToken _viewCancellationToken;
    private int _simpleSectionNextOffset;
    private int _simpleSectionLoadVersion;
    private int _simpleSectionSearchVersion;
    private int? _simpleSectionLoadingVersion;
    private bool _isSimpleSectionsLoaded;
    private bool _isSimpleViewModeLoaded;

    private Task? _loadTask;
    private bool _reloadRequested;
    private bool _isLoaded;
    private bool _suppressSelectionReload;
    private int _contextLoadVersion;
    private Guid? _preferredSectionId;
    private Guid? _preferredTopicId;

    public ObservableCollection<LibraryManagementOrderItemViewModel> Sections { get; } = [];

    public ObservableCollection<LibraryManagementOrderItemViewModel> Topics { get; } = [];

    public ObservableCollection<LibraryManagementOrderItemViewModel> Materials { get; } = [];

    public ObservableCollection<LibraryManagementSectionViewModel> SimpleSections { get; } = [];

    public ObservableCollection<LibraryManagementSectionRowViewModel> SimpleSectionRows { get; } = [];

    public ObservableCollection<LibraryManagementSectionRowViewModel> SimpleCompactSectionRows { get; } = [];

    public IReadOnlyList<LibraryManagementSectionSortOption> SimpleSectionSortOptions { get; } =
    [
        new("Мой порядок", LibraryManagementSectionSort.Custom),
        new("Последняя активность", LibraryManagementSectionSort.RecentActivity),
        new("По названию", LibraryManagementSectionSort.Name),
        new("Сначала новые", LibraryManagementSectionSort.Newest),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyCanExecuteChangedFor(nameof(SaveOrderCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddTopicCommand))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveOrderCommand))]
    private bool _isContextLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveOrderCommand))]
    private bool _isSavingOrder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedSection))]
    [NotifyPropertyChangedFor(nameof(SelectedPath))]
    [NotifyCanExecuteChangedFor(nameof(AddTopicCommand))]
    private LibraryManagementOrderItemViewModel? _selectedSection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedTopic))]
    [NotifyPropertyChangedFor(nameof(SelectedPath))]
    private LibraryManagementOrderItemViewModel? _selectedTopic;

    [ObservableProperty]
    private LibraryManagementOrderItemViewModel? _selectedMaterial;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSimpleSections))]
    [NotifyPropertyChangedFor(nameof(IsSimpleSectionsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSimpleSectionSearchResults))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextSimpleSectionPageCommand))]
    private bool _isSimpleSectionsLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadNextSimpleSectionPageCommand))]
    private bool _isSimpleSectionsLoadingNextPage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadNextSimpleSectionPageCommand))]
    private bool _simpleSectionsHasMore = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSimpleSectionsNextPageError))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextSimpleSectionPageCommand))]
    private string? _simpleSectionsNextPageErrorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimpleSectionsShownCountText))]
    private int _simpleSectionsTotalCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSimpleTilesView))]
    [NotifyPropertyChangedFor(nameof(IsSimpleCompactTilesView))]
    [NotifyPropertyChangedFor(nameof(IsSimpleTableView))]
    private LibraryManagementViewMode _simpleViewMode = LibraryManagementViewMode.Tiles;

    [ObservableProperty]
    private LibraryManagementSectionSortOption _selectedSimpleSectionSortOption =
        new("Мой порядок", LibraryManagementSectionSort.Custom);

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSimpleMode))]
    [NotifyPropertyChangedFor(nameof(IsSimpleSectionsPage))]
    [NotifyPropertyChangedFor(nameof(IsSimpleTopicsPage))]
    [NotifyPropertyChangedFor(nameof(IsSimpleMaterialsPage))]
    [NotifyPropertyChangedFor(nameof(ShowOrderFooter))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextSimpleSectionPageCommand))]
    private bool _isAdvancedMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSimpleSectionsPage))]
    [NotifyPropertyChangedFor(nameof(IsSimpleTopicsPage))]
    [NotifyPropertyChangedFor(nameof(IsSimpleMaterialsPage))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextSimpleSectionPageCommand))]
    private LibraryManagementSimplePage _simplePage = LibraryManagementSimplePage.Sections;

    public bool IsSimpleMode => !IsAdvancedMode;

    public bool IsSimpleSectionsPage => IsSimpleMode && SimplePage == LibraryManagementSimplePage.Sections;

    public bool IsSimpleTopicsPage => IsSimpleMode && SimplePage == LibraryManagementSimplePage.Topics;

    public bool IsSimpleMaterialsPage => IsSimpleMode && SimplePage == LibraryManagementSimplePage.Materials;

    public bool HasSimpleSections => SimpleSections.Count > 0;

    public bool IsSimpleSectionsEmpty =>
        !IsSimpleSectionsLoading &&
        !HasError &&
        !HasSimpleSections &&
        string.IsNullOrWhiteSpace(SearchText);

    public bool HasNoSimpleSectionSearchResults =>
        !IsSimpleSectionsLoading &&
        !HasError &&
        !HasSimpleSections &&
        !string.IsNullOrWhiteSpace(SearchText);

    public bool HasSimpleSectionsNextPageError =>
        !string.IsNullOrWhiteSpace(SimpleSectionsNextPageErrorMessage);

    public bool IsSimpleTilesView => SimpleViewMode == LibraryManagementViewMode.Tiles;

    public bool IsSimpleCompactTilesView => SimpleViewMode == LibraryManagementViewMode.CompactTiles;

    public bool IsSimpleTableView => SimpleViewMode == LibraryManagementViewMode.Table;

    public string SimpleSectionsShownCountText =>
        $"Показано {SimpleSections.Count} из {SimpleSectionsTotalCount}";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsEmpty => !IsLoading && !HasError && Sections.Count == 0;

    public bool HasSelectedSection => SelectedSection is not null;

    public bool HasSelectedTopic => SelectedTopic is not null;

    public bool HasTopics => Topics.Count > 0;

    public bool HasMaterials => Materials.Count > 0;

    public int LoadedMaterialsCount => Materials.Count;

    public int TotalMaterialsCount =>
        SelectedTopic is null
            ? 0
            : GetTopicMaterials(FindTopic(SelectedTopic.Id)).Count;

    public string MaterialsShownCountText =>
        $"Показано {LoadedMaterialsCount} из {TotalMaterialsCount}";

    public bool HasUnsavedOrder =>
        _pendingSectionOrder is not null ||
        _pendingTopicOrders.Count > 0 ||
        _pendingMaterialOrders.Count > 0;

    public bool ShowOrderFooter => IsAdvancedMode || HasUnsavedOrder;

    public string SelectedPath
    {
        get
        {
            if (SelectedSection is null)
            {
                return "Выберите раздел";
            }

            return SelectedTopic is null
                ? SelectedSection.Name
                : $"{SelectedSection.Name} / {SelectedTopic.Name}";
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _viewCancellationToken = cancellationToken;
        _isSimpleSectionsLoaded = true;
        LoadNextSimpleSectionPageCommand.NotifyCanExecuteChanged();

        await EnsureSimpleViewModeLoadedAsync(cancellationToken);
        await ReloadSimpleSectionsCoreAsync(cancellationToken);
    }

    public void MoveSection(LibraryManagementOrderItemViewModel item, int targetIndex)
    {
        if (!MoveItem(Sections, item, targetIndex))
        {
            return;
        }

        _pendingSectionOrder = Sections.Select(section => section.Id).ToArray();
        NotifyOrderChanged();
    }

    public void MoveTopic(LibraryManagementOrderItemViewModel item, int targetIndex)
    {
        if (SelectedSection is null || !MoveItem(Topics, item, targetIndex))
        {
            return;
        }

        _pendingTopicOrders[SelectedSection.Id] = Topics.Select(topic => topic.Id).ToArray();
        NotifyOrderChanged();
    }

    public void MoveMaterial(LibraryManagementOrderItemViewModel item, int targetIndex)
    {
        if (SelectedTopic is null || !MoveItem(Materials, item, targetIndex))
        {
            return;
        }

        _pendingMaterialOrders[SelectedTopic.Id] = Materials.Select(material => material.Id).ToArray();
        NotifyOrderChanged();
    }

    [RelayCommand]
    private void ShowSimpleMode()
    {
        IsAdvancedMode = false;
        SimplePage = LibraryManagementSimplePage.Sections;
    }

    [RelayCommand]
    private async Task ShowAdvancedModeAsync(CancellationToken cancellationToken)
    {
        IsAdvancedMode = true;
        await EnsureAdvancedLibraryLoadedAsync(cancellationToken);

        if (SelectedSection is not null && SelectedTopic is null)
        {
            SelectedTopic = Topics.FirstOrDefault();
        }
    }

    [RelayCommand]
    private async Task ToggleEditingModeAsync(CancellationToken cancellationToken)
    {
        if (IsAdvancedMode)
        {
            ShowSimpleMode();
            return;
        }

        await ShowAdvancedModeAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task OpenSimpleSectionAsync(
        LibraryManagementSectionViewModel? item,
        CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        await EnsureAdvancedLibraryLoadedAsync(cancellationToken);

        LibraryManagementOrderItemViewModel? advancedSection =
            Sections.FirstOrDefault(section => section.Id == item.Id);

        if (advancedSection is null)
        {
            return;
        }

        SelectedTopic = null;
        SelectedSection = advancedSection;
        SimplePage = LibraryManagementSimplePage.Topics;
    }

    [RelayCommand]
    private void OpenSimpleTopic(LibraryManagementOrderItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedTopic = item;
        SimplePage = LibraryManagementSimplePage.Materials;
    }

    [RelayCommand]
    private void BackSimple()
    {
        switch (SimplePage)
        {
            case LibraryManagementSimplePage.Materials:
                SimplePage = LibraryManagementSimplePage.Topics;
                break;
            case LibraryManagementSimplePage.Topics:
                SelectedTopic = null;
                SimplePage = LibraryManagementSimplePage.Sections;
                break;
        }
    }

    [RelayCommand]
    private async Task AddSectionAsync(CancellationToken cancellationToken)
    {
        var sectionId = dialogService.Show<CreateSectionDialogViewModel, Guid?>();

        if (sectionId is null)
        {
            return;
        }

        _preferredSectionId = sectionId.Value;
        _preferredTopicId = null;
        notificationService.ShowSuccess("Раздел создан");
        await RefreshAfterMutationAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task EditSectionAsync(
        LibraryManagementOrderItemViewModel? item,
        CancellationToken cancellationToken)
    {
        LibrarySectionDto? section = item?.Section ?? FindSection(item?.Id);

        if (section is null)
        {
            return;
        }

        var sectionId = dialogService.Show<EditSectionDialogViewModel, Guid?>(
            viewModel => viewModel.Initialize(section));

        if (sectionId is null)
        {
            return;
        }

        _preferredSectionId = section.Id;
        notificationService.ShowSuccess("Изменения раздела сохранены");
        await RefreshAfterMutationAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task DeleteSectionAsync(
        LibraryManagementOrderItemViewModel? item,
        CancellationToken cancellationToken)
    {
        LibrarySectionDto? section = item?.Section ?? FindSection(item?.Id);

        if (section is null)
        {
            return;
        }

        bool wasDeleted = dialogService.Show<DeleteSectionDialogViewModel, bool>(
            viewModel => viewModel.Initialize(section));

        if (!wasDeleted)
        {
            return;
        }

        _pendingTopicOrders.Remove(section.Id);
        _preferredSectionId = null;
        _preferredTopicId = null;
        notificationService.ShowSuccess($"Раздел «{section.Name}» удалён");
        await RefreshAfterMutationAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanAddTopic))]
    private async Task AddTopicAsync(
        LibraryManagementOrderItemViewModel? item,
        CancellationToken cancellationToken)
    {
        LibrarySectionDto? section = item?.Section
                                     ?? FindSection(item?.Id)
                                     ?? FindSection(SelectedSection?.Id);

        if (section is null)
        {
            return;
        }

        var topicId = dialogService.Show<CreateTopicDialogViewModel, Guid?>(
            viewModel => viewModel.Initialize(section.Id, section.Name));

        if (topicId is null)
        {
            return;
        }

        _preferredSectionId = section.Id;
        _preferredTopicId = topicId.Value;
        notificationService.ShowSuccess("Тема создана");
        await RefreshAfterMutationAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task EditTopicAsync(
        LibraryManagementOrderItemViewModel? item,
        CancellationToken cancellationToken)
    {
        LibraryTopicDto? topic = item?.Topic ?? FindTopic(item?.Id);

        if (topic is null)
        {
            return;
        }

        var topicId = dialogService.Show<EditTopicDialogViewModel, Guid?>(
            viewModel => viewModel.Initialize(topic));

        if (topicId is null)
        {
            return;
        }

        _preferredTopicId = topic.Id;
        notificationService.ShowSuccess("Изменения темы сохранены");
        await RefreshAfterMutationAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task DeleteTopicAsync(
        LibraryManagementOrderItemViewModel? item,
        CancellationToken cancellationToken)
    {
        LibraryTopicDto? topic = item?.Topic ?? FindTopic(item?.Id);

        if (topic is null)
        {
            return;
        }

        bool wasDeleted = dialogService.Show<DeleteTopicDialogViewModel, bool>(
            viewModel => viewModel.Initialize(topic));

        if (!wasDeleted)
        {
            return;
        }

        _pendingMaterialOrders.Remove(topic.Id);
        _preferredTopicId = null;
        notificationService.ShowSuccess($"Тема «{topic.Name}» удалена");
        await RefreshAfterMutationAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanSaveOrder))]
    private async Task SaveOrderAsync(CancellationToken cancellationToken)
    {
        IsSavingOrder = true;
        ErrorMessage = null;

        try
        {
            if (_pendingSectionOrder is not null)
            {
                if (!await SaveOrderCoreAsync(
                        LibraryOrderTarget.Sections,
                        parentId: null,
                        _pendingSectionOrder,
                        cancellationToken))
                {
                    return;
                }

                _pendingSectionOrder = null;
            }

            foreach (var pendingOrder in _pendingTopicOrders.ToArray())
            {
                if (!await SaveOrderCoreAsync(
                        LibraryOrderTarget.Topics,
                        pendingOrder.Key,
                        pendingOrder.Value,
                        cancellationToken))
                {
                    return;
                }

                _pendingTopicOrders.Remove(pendingOrder.Key);
            }

            foreach (var pendingOrder in _pendingMaterialOrders.ToArray())
            {
                if (!await SaveOrderCoreAsync(
                        LibraryOrderTarget.Materials,
                        pendingOrder.Key,
                        pendingOrder.Value,
                        cancellationToken))
                {
                    return;
                }

                _pendingMaterialOrders.Remove(pendingOrder.Key);
            }

            notificationService.ShowSuccess("Порядок сохранён");

            if (SelectedSimpleSectionSortOption.Sort == LibraryManagementSectionSort.Custom)
            {
                await ReloadSimpleSectionsCoreAsync(cancellationToken);
            }
        }
        finally
        {
            IsSavingOrder = false;
            NotifyOrderChanged();
        }
    }

    [RelayCommand]
    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        await ReloadSimpleSectionsCoreAsync(cancellationToken);

        if (IsAdvancedMode || _isLoaded)
        {
            _preferredSectionId = SelectedSection?.Id;
            _preferredTopicId = SelectedTopic?.Id;
            await ReloadAdvancedLibraryAsync(cancellationToken);
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadNextSimpleSectionPage))]
    private Task LoadNextSimpleSectionPageAsync(CancellationToken cancellationToken)
    {
        return LoadNextSimpleSectionPageWithLinkedCancellationAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task RetryNextSimpleSectionPageAsync(CancellationToken cancellationToken)
    {
        SimpleSectionsNextPageErrorMessage = null;

        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _viewCancellationToken,
            cancellationToken);

        await LoadSimpleSectionsPageAsync(
            _simpleSectionLoadVersion,
            linkedCancellationTokenSource.Token);
    }

    [RelayCommand]
    private Task ShowSimpleTilesViewAsync(CancellationToken cancellationToken)
    {
        return SetSimpleViewModeAsync(LibraryManagementViewMode.Tiles, cancellationToken);
    }

    [RelayCommand]
    private Task ShowSimpleCompactTilesViewAsync(CancellationToken cancellationToken)
    {
        return SetSimpleViewModeAsync(LibraryManagementViewMode.CompactTiles, cancellationToken);
    }

    [RelayCommand]
    private Task ShowSimpleTableViewAsync(CancellationToken cancellationToken)
    {
        return SetSimpleViewModeAsync(LibraryManagementViewMode.Table, cancellationToken);
    }

    [RelayCommand]
    private async Task AddSimpleTopicAsync(
        LibraryManagementSectionViewModel? item,
        CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        var topicId = dialogService.Show<CreateTopicDialogViewModel, Guid?>(
            viewModel => viewModel.Initialize(item.Id, item.Name));

        if (topicId is null)
        {
            return;
        }

        _preferredSectionId = item.Id;
        _preferredTopicId = topicId.Value;
        notificationService.ShowSuccess("Тема создана");
        await RefreshAfterMutationAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task EditSimpleSectionAsync(
        LibraryManagementSectionViewModel? item,
        CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        await EnsureAdvancedLibraryLoadedAsync(cancellationToken);

        LibraryManagementOrderItemViewModel? advancedItem =
            Sections.FirstOrDefault(section => section.Id == item.Id);

        await EditSectionAsync(advancedItem, cancellationToken);
    }

    [RelayCommand]
    private async Task DeleteSimpleSectionAsync(
        LibraryManagementSectionViewModel? item,
        CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        await EnsureAdvancedLibraryLoadedAsync(cancellationToken);

        LibraryManagementOrderItemViewModel? advancedItem =
            Sections.FirstOrDefault(section => section.Id == item.Id);

        await DeleteSectionAsync(advancedItem, cancellationToken);
    }

    partial void OnSearchTextChanged(string? value)
    {
        int searchVersion = Interlocked.Increment(ref _simpleSectionSearchVersion);

        if (_isSimpleSectionsLoaded && IsSimpleSectionsPage)
        {
            _ = ReloadSimpleSectionsAfterSearchDelayAsync(searchVersion);
            return;
        }

        // В расширенном режиме поиск не фильтрует коллекцию, чтобы не ломать
        // семантику ручного порядка. Он только переводит выбор к найденному элементу.
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string search = value.Trim();

        LibraryManagementOrderItemViewModel? section = Sections.FirstOrDefault(item =>
            item.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

        if (section is not null)
        {
            SelectedSection = section;
            return;
        }

        LibraryManagementOrderItemViewModel? topic = Topics.FirstOrDefault(item =>
            item.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

        if (topic is not null)
        {
            SelectedTopic = topic;
            return;
        }

        SelectedMaterial = Materials.FirstOrDefault(item =>
            item.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    partial void OnSelectedSimpleSectionSortOptionChanged(LibraryManagementSectionSortOption value)
    {
        if (_isSimpleSectionsLoaded)
        {
            _ = ReloadSimpleSectionsAfterSortChangedAsync();
        }
    }

    partial void OnSelectedSectionChanged(LibraryManagementOrderItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedPath));

        if (!_isLoaded || _suppressSelectionReload)
        {
            return;
        }

        _ = ChangeSectionContextSafelyAsync(value);
    }

    partial void OnSelectedTopicChanged(LibraryManagementOrderItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedPath));

        if (!_isLoaded || _suppressSelectionReload)
        {
            return;
        }

        _ = ChangeTopicContextSafelyAsync(value);
    }

    private async Task EnsureAdvancedLibraryLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded && _library.Count > 0)
        {
            return;
        }

        await ReloadAdvancedLibraryAsync(cancellationToken);
    }

    private Task ReloadAdvancedLibraryAsync(CancellationToken cancellationToken)
    {
        if (_loadTask is { IsCompleted: false })
        {
            _reloadRequested = true;
            return _loadTask;
        }

        _loadTask = LoadUntilCurrentAsync(cancellationToken);
        return _loadTask;
    }

    private bool CanLoadNextSimpleSectionPage()
    {
        return _isSimpleSectionsLoaded &&
               IsSimpleSectionsPage &&
               SimpleSectionsHasMore &&
               !IsSimpleSectionsLoading &&
               !IsSimpleSectionsLoadingNextPage &&
               !HasError &&
               !HasSimpleSectionsNextPageError;
    }

    private async Task LoadNextSimpleSectionPageWithLinkedCancellationAsync(
        CancellationToken cancellationToken)
    {
        using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _viewCancellationToken,
            cancellationToken);

        await LoadSimpleSectionsPageAsync(
            _simpleSectionLoadVersion,
            linkedCancellationTokenSource.Token);
    }

    private async Task ReloadSimpleSectionsCoreAsync(CancellationToken cancellationToken)
    {
        int loadVersion = Interlocked.Increment(ref _simpleSectionLoadVersion);

        _simpleSectionNextOffset = 0;
        SimpleSectionsHasMore = true;
        ErrorMessage = null;
        SimpleSectionsNextPageErrorMessage = null;
        SimpleSectionsTotalCount = 0;

        SimpleSections.Clear();
        SimpleSectionRows.Clear();
        SimpleCompactSectionRows.Clear();

        NotifySimpleSectionsStateChanged();
        await LoadSimpleSectionsPageAsync(loadVersion, cancellationToken);
    }

    private async Task LoadSimpleSectionsPageAsync(
        int loadVersion,
        CancellationToken cancellationToken)
    {
        if (loadVersion != _simpleSectionLoadVersion ||
            _simpleSectionLoadingVersion == loadVersion ||
            !SimpleSectionsHasMore ||
            cancellationToken.IsCancellationRequested)
        {
            return;
        }

        _simpleSectionLoadingVersion = loadVersion;
        bool isInitialPage = _simpleSectionNextOffset == 0;

        if (isInitialPage)
        {
            IsSimpleSectionsLoading = true;
            ErrorMessage = null;
        }
        else
        {
            IsSimpleSectionsLoadingNextPage = true;
            SimpleSectionsNextPageErrorMessage = null;
        }

        try
        {
            var query = new GetLibraryManagementSectionsPageQuery(
                SearchText,
                SelectedSimpleSectionSortOption.Sort,
                _simpleSectionNextOffset,
                SimpleSectionPageSize);

            var result = await queryDispatcher.SendAsync<
                GetLibraryManagementSectionsPageQuery,
                LibraryManagementSectionsPageDto>(
                query,
                cancellationToken);

            if (loadVersion != _simpleSectionLoadVersion || cancellationToken.IsCancellationRequested)
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
                    SimpleSectionsNextPageErrorMessage = message;
                }

                return;
            }

            foreach (LibrarySectionOverviewDto section in result.Value.Items)
            {
                var sectionViewModel = new LibraryManagementSectionViewModel(section);
                SimpleSections.Add(sectionViewModel);
                AddSimpleSectionToRows(sectionViewModel);
            }

            _simpleSectionNextOffset = result.Value.NextOffset;
            SimpleSectionsHasMore = result.Value.HasMore;
            SimpleSectionsTotalCount = result.Value.TotalCount;
            NotifySimpleSectionsStateChanged();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // View/search request was cancelled.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось загрузить страницу разделов управления");

            if (loadVersion != _simpleSectionLoadVersion)
            {
                return;
            }

            if (isInitialPage)
            {
                ErrorMessage = "Не удалось загрузить разделы";
            }
            else
            {
                SimpleSectionsNextPageErrorMessage = "Не удалось загрузить следующую порцию разделов";
            }
        }
        finally
        {
            if (_simpleSectionLoadingVersion == loadVersion)
            {
                _simpleSectionLoadingVersion = null;
            }

            if (loadVersion == _simpleSectionLoadVersion)
            {
                IsSimpleSectionsLoading = false;
                IsSimpleSectionsLoadingNextPage = false;
                LoadNextSimpleSectionPageCommand.NotifyCanExecuteChanged();
                NotifySimpleSectionsStateChanged();
            }
        }
    }

    private void AddSimpleSectionToRows(LibraryManagementSectionViewModel section)
    {
        AddSimpleSectionToRows(SimpleSectionRows, section, 3);
        AddSimpleSectionToRows(SimpleCompactSectionRows, section, 4);
    }

    private static void AddSimpleSectionToRows(
        ObservableCollection<LibraryManagementSectionRowViewModel> rows,
        LibraryManagementSectionViewModel section,
        int capacity)
    {
        LibraryManagementSectionRowViewModel? row = rows.LastOrDefault();

        if (row is null || row.IsFull)
        {
            row = new LibraryManagementSectionRowViewModel(capacity);
            rows.Add(row);
        }

        row.Add(section);
    }

    private async Task ReloadSimpleSectionsAfterSearchDelayAsync(int searchVersion)
    {
        try
        {
            await Task.Delay(SearchDelay, _viewCancellationToken);

            if (searchVersion == Volatile.Read(ref _simpleSectionSearchVersion))
            {
                await ReloadSimpleSectionsCoreAsync(_viewCancellationToken);
            }
        }
        catch (OperationCanceledException) when (_viewCancellationToken.IsCancellationRequested)
        {
            // View was unloaded.
        }
    }

    private async Task ReloadSimpleSectionsAfterSortChangedAsync()
    {
        try
        {
            await ReloadSimpleSectionsCoreAsync(_viewCancellationToken);
        }
        catch (OperationCanceledException) when (_viewCancellationToken.IsCancellationRequested)
        {
            // View was unloaded.
        }
    }

    private async Task EnsureSimpleViewModeLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isSimpleViewModeLoaded)
        {
            return;
        }

        try
        {
            AppSettings settings = await settingsService.LoadAsync(cancellationToken);
            SimpleViewMode = settings.LibraryManagementViewMode;
            _isSimpleViewModeLoaded = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось загрузить режим отображения управления библиотекой");
            SimpleViewMode = LibraryManagementViewMode.Tiles;
            _isSimpleViewModeLoaded = true;
        }
    }

    private async Task SetSimpleViewModeAsync(
        LibraryManagementViewMode viewMode,
        CancellationToken cancellationToken)
    {
        if (SimpleViewMode == viewMode && _isSimpleViewModeLoaded)
        {
            return;
        }

        SimpleViewMode = viewMode;
        _isSimpleViewModeLoaded = true;

        try
        {
            await settingsService.SaveLibraryManagementViewModeAsync(viewMode, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось сохранить режим отображения управления библиотекой");
        }
    }

    private async Task RefreshAfterMutationAsync(CancellationToken cancellationToken)
    {
        await ReloadSimpleSectionsCoreAsync(cancellationToken);

        if (_isLoaded || IsAdvancedMode || SimplePage != LibraryManagementSimplePage.Sections)
        {
            await ReloadAdvancedLibraryAsync(cancellationToken);
        }
    }

    private async Task LoadUntilCurrentAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;

        try
        {
            do
            {
                _reloadRequested = false;
                await LoadCoreAsync(cancellationToken);
            }
            while (_reloadRequested && !cancellationToken.IsCancellationRequested);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;

        Guid? selectedSectionId = _preferredSectionId ?? SelectedSection?.Id;
        Guid? selectedTopicId = _preferredTopicId ?? SelectedTopic?.Id;

        var libraryResult = await queryDispatcher.SendAsync<
            GetLibraryQuery,
            IReadOnlyList<LibrarySectionDto>>(
            new GetLibraryQuery(),
            cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (libraryResult.IsFailure)
        {
            ErrorMessage = libraryResult.Error.FirstOrDefault()?.Message
                           ?? "Не удалось загрузить библиотеку";
            return;
        }

        _library.Clear();
        _library.AddRange(libraryResult.Value);

        _isLoaded = false;

        try
        {
            await LoadSectionsAsync(selectedSectionId, cancellationToken);
            await LoadTopicsAsync(selectedTopicId, cancellationToken);
            await LoadMaterialsAsync(cancellationToken);
        }
        finally
        {
            _isLoaded = true;
            _preferredSectionId = null;
            _preferredTopicId = null;
            NotifyCollectionStateChanged();
        }
    }

    private async Task LoadSectionsAsync(Guid? selectedSectionId, CancellationToken cancellationToken)
    {
        var result = await LoadOrderItemsAsync(
            LibraryOrderTarget.Sections,
            parentId: null,
            cancellationToken);

        if (result is null)
        {
            return;
        }

        IReadOnlyList<LibraryOrderItemDto> orderedItems = ApplyPendingOrder(
            result,
            _pendingSectionOrder);

        Sections.Clear();

        int position = 1;

        foreach (var orderItem in orderedItems)
        {
            LibrarySectionDto? section = FindSection(orderItem.Id);

            Sections.Add(new LibraryManagementOrderItemViewModel(
                orderItem,
                LibraryOrderTarget.Sections,
                position++,
                section: section));
        }

        if (_pendingSectionOrder is not null)
        {
            _pendingSectionOrder = Sections.Select(section => section.Id).ToArray();
        }

        _suppressSelectionReload = true;
        try
        {
            SelectedSection = Sections.FirstOrDefault(section => section.Id == selectedSectionId)
                              ?? Sections.FirstOrDefault();
        }
        finally
        {
            _suppressSelectionReload = false;
        }
    }

    private async Task LoadTopicsAsync(Guid? selectedTopicId, CancellationToken cancellationToken)
    {
        Topics.Clear();

        _suppressSelectionReload = true;
        try
        {
            SelectedTopic = null;
        }
        finally
        {
            _suppressSelectionReload = false;
        }

        if (SelectedSection is null)
        {
            return;
        }

        var result = await LoadOrderItemsAsync(
            LibraryOrderTarget.Topics,
            SelectedSection.Id,
            cancellationToken);

        if (result is null)
        {
            return;
        }

        _pendingTopicOrders.TryGetValue(SelectedSection.Id, out Guid[]? pendingOrder);
        IReadOnlyList<LibraryOrderItemDto> orderedItems = ApplyPendingOrder(result, pendingOrder);

        int position = 1;

        foreach (var orderItem in orderedItems)
        {
            LibraryTopicDto? topic = FindTopic(orderItem.Id);

            Topics.Add(new LibraryManagementOrderItemViewModel(
                orderItem,
                LibraryOrderTarget.Topics,
                position++,
                topic: topic));
        }

        if (pendingOrder is not null)
        {
            _pendingTopicOrders[SelectedSection.Id] = Topics.Select(topic => topic.Id).ToArray();
        }

        _suppressSelectionReload = true;
        try
        {
            SelectedTopic = Topics.FirstOrDefault(topic => topic.Id == selectedTopicId)
                            ?? (IsAdvancedMode ? Topics.FirstOrDefault() : null);
        }
        finally
        {
            _suppressSelectionReload = false;
        }
    }

    private async Task LoadMaterialsAsync(CancellationToken cancellationToken)
    {
        Materials.Clear();
        SelectedMaterial = null;

        if (SelectedTopic is null)
        {
            NotifyCollectionStateChanged();
            return;
        }

        var result = await LoadOrderItemsAsync(
            LibraryOrderTarget.Materials,
            SelectedTopic.Id,
            cancellationToken);

        if (result is null)
        {
            return;
        }

        _pendingMaterialOrders.TryGetValue(SelectedTopic.Id, out Guid[]? pendingOrder);
        IReadOnlyList<LibraryOrderItemDto> orderedItems = ApplyPendingOrder(result, pendingOrder);

        IReadOnlyList<LibraryMaterialDto> topicMaterials = GetTopicMaterials(FindTopic(SelectedTopic.Id));
        var materialsById = topicMaterials.ToDictionary(material => material.Id);

        int position = 1;

        foreach (var orderItem in orderedItems)
        {
            materialsById.TryGetValue(orderItem.Id, out LibraryMaterialDto? material);

            Materials.Add(new LibraryManagementOrderItemViewModel(
                orderItem,
                LibraryOrderTarget.Materials,
                position++,
                material: material));
        }

        if (pendingOrder is not null)
        {
            _pendingMaterialOrders[SelectedTopic.Id] = Materials.Select(material => material.Id).ToArray();
        }

        SelectedMaterial = Materials.FirstOrDefault();
        NotifyCollectionStateChanged();
    }

    private async Task<IReadOnlyList<LibraryOrderItemDto>?> LoadOrderItemsAsync(
        LibraryOrderTarget target,
        Guid? parentId,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync<
            GetLibraryOrderItemsQuery,
            IReadOnlyList<LibraryOrderItemDto>>(
            new GetLibraryOrderItemsQuery(target, parentId),
            cancellationToken);

        if (result.IsFailure)
        {
            ErrorMessage = result.Error.FirstOrDefault()?.Message
                           ?? "Не удалось загрузить порядок библиотеки";
            return null;
        }

        return result.Value;
    }

    private async Task ChangeSectionContextSafelyAsync(LibraryManagementOrderItemViewModel? section)
    {
        int loadVersion = Interlocked.Increment(ref _contextLoadVersion);
        IsContextLoading = true;

        try
        {
            Guid? preferredTopicId = SelectedTopic?.Id;
            await LoadTopicsAsync(preferredTopicId, CancellationToken.None);

            if (loadVersion != _contextLoadVersion || SelectedSection?.Id != section?.Id)
            {
                return;
            }

            await LoadMaterialsAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось сменить раздел управления библиотекой");
            ErrorMessage = "Не удалось загрузить выбранный раздел";
        }
        finally
        {
            if (loadVersion == _contextLoadVersion)
            {
                IsContextLoading = false;
            }
        }
    }

    private async Task ChangeTopicContextSafelyAsync(LibraryManagementOrderItemViewModel? topic)
    {
        int loadVersion = Interlocked.Increment(ref _contextLoadVersion);
        IsContextLoading = true;

        try
        {
            await LoadMaterialsAsync(CancellationToken.None);

            if (loadVersion != _contextLoadVersion || SelectedTopic?.Id != topic?.Id)
            {
                return;
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось сменить тему управления библиотекой");
            ErrorMessage = "Не удалось загрузить выбранную тему";
        }
        finally
        {
            if (loadVersion == _contextLoadVersion)
            {
                IsContextLoading = false;
            }
        }
    }

    private async Task<bool> SaveOrderCoreAsync(
        LibraryOrderTarget target,
        Guid? parentId,
        IReadOnlyCollection<Guid> itemIds,
        CancellationToken cancellationToken)
    {
        var command = new SaveLibraryOrderCommand(target, parentId, itemIds.ToArray());
        var result = await commandDispatcher.SendAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            ErrorMessage = result.Error.FirstOrDefault()?.Message ?? "Не удалось сохранить порядок";
            return false;
        }

        return true;
    }

    private LibrarySectionDto? FindSection(Guid? id)
    {
        return id is null
            ? null
            : _library.FirstOrDefault(section => section.Id == id.Value);
    }

    private LibraryTopicDto? FindTopic(Guid? id)
    {
        if (id is null)
        {
            return null;
        }

        return _library
            .SelectMany(section => section.Topics)
            .FirstOrDefault(topic => topic.Id == id.Value);
    }

    private static IReadOnlyList<LibraryMaterialDto> GetTopicMaterials(LibraryTopicDto? topic)
    {
        if (topic is null)
        {
            return Array.Empty<LibraryMaterialDto>();
        }

        // В текущей модели библиотеки Materials является init-свойством LibraryTopicDto.
        // Reflection оставляет этот ViewModel совместимым и со старой версией контракта,
        // где у LibraryTopicDto ещё не было списка материалов.
        var materialsProperty = typeof(LibraryTopicDto).GetProperty("Materials");

        if (materialsProperty?.GetValue(topic) is IEnumerable<LibraryMaterialDto> materials)
        {
            return materials.ToArray();
        }

        return Array.Empty<LibraryMaterialDto>();
    }

    private static IReadOnlyList<LibraryOrderItemDto> ApplyPendingOrder(
        IReadOnlyList<LibraryOrderItemDto> source,
        IReadOnlyList<Guid>? pendingOrder)
    {
        if (pendingOrder is null || pendingOrder.Count == 0)
        {
            return source;
        }

        var itemsById = source.ToDictionary(item => item.Id);
        var result = new List<LibraryOrderItemDto>(source.Count);

        foreach (Guid id in pendingOrder)
        {
            if (itemsById.Remove(id, out LibraryOrderItemDto? item))
            {
                result.Add(item);
            }
        }

        result.AddRange(source.Where(item => itemsById.ContainsKey(item.Id)));
        return result;
    }

    private static bool MoveItem(
        ObservableCollection<LibraryManagementOrderItemViewModel> collection,
        LibraryManagementOrderItemViewModel item,
        int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(item);

        int sourceIndex = collection.IndexOf(item);

        if (sourceIndex < 0 ||
            targetIndex < 0 ||
            targetIndex >= collection.Count ||
            sourceIndex == targetIndex)
        {
            return false;
        }

        collection.Move(sourceIndex, targetIndex);

        for (int index = 0; index < collection.Count; index++)
        {
            collection[index].Position = index + 1;
        }

        return true;
    }

    private bool CanAddTopic(LibraryManagementOrderItemViewModel? item)
    {
        return !IsLoading && (item is not null || SelectedSection is not null);
    }

    private bool CanSaveOrder() => HasUnsavedOrder && !IsLoading && !IsContextLoading && !IsSavingOrder;

    private void NotifyOrderChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedOrder));
        OnPropertyChanged(nameof(ShowOrderFooter));
        SaveOrderCommand.NotifyCanExecuteChanged();
    }

    private void NotifySimpleSectionsStateChanged()
    {
        OnPropertyChanged(nameof(HasSimpleSections));
        OnPropertyChanged(nameof(IsSimpleSectionsEmpty));
        OnPropertyChanged(nameof(HasNoSimpleSectionSearchResults));
        OnPropertyChanged(nameof(SimpleSectionsShownCountText));
        LoadNextSimpleSectionPageCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCollectionStateChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasTopics));
        OnPropertyChanged(nameof(HasMaterials));
        OnPropertyChanged(nameof(LoadedMaterialsCount));
        OnPropertyChanged(nameof(TotalMaterialsCount));
        OnPropertyChanged(nameof(MaterialsShownCountText));
        OnPropertyChanged(nameof(SelectedPath));
    }
}
