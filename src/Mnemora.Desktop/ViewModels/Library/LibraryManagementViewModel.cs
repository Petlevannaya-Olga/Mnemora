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

public enum LibraryManagementTopicSort
{
    Custom,
    RecentActivity,
    Name,
    Newest,
}

public sealed record LibraryManagementTopicSortOption(
    string Name,
    LibraryManagementTopicSort Sort);

public enum LibraryManagementMaterialFilter
{
    All,
    Articles,
    Questions,
}

public enum LibraryManagementMaterialSort
{
    Custom,
    RecentActivity,
    Name,
    Newest,
}

public sealed record LibraryManagementMaterialSortOption(
    string Name,
    LibraryManagementMaterialSort Sort);

public sealed partial class LibraryManagementViewModel(
    IQueryDispatcher queryDispatcher,
    ICommandDispatcher commandDispatcher,
    IDialogService dialogService,
    ISettingsService settingsService,
    INotificationService notificationService,
    CreateMaterialViewModel createMaterialViewModel,
    ILogger<LibraryManagementViewModel> logger)
    : ViewModelBase
{
    private readonly List<LibrarySectionDto> _library = [];
    private readonly Dictionary<Guid, Guid[]> _pendingTopicOrders = new();
    private readonly Dictionary<Guid, Guid[]> _pendingMaterialOrders = new();

    public CreateMaterialViewModel CreateMaterial { get; } = createMaterialViewModel;

    private const int SimpleSectionPageSize = LibraryPagingDefaults.PageSize;
    private const int SimpleMaterialPageSize = LibraryPagingDefaults.PageSize;
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(350);

    private Guid[]? _pendingSectionOrder;
    private Guid[]? _orderSnapshot;

    private CancellationToken _viewCancellationToken;
    private int _simpleSectionLoadVersion;
    private int _simpleSectionSearchVersion;
    private int _simpleTopicSearchVersion;
    private int _simpleMaterialSearchVersion;
    private int _simpleMaterialVisibleCount = SimpleMaterialPageSize;
    private int _simpleMaterialsFilteredTotalCount;
    private bool _isSimpleSectionsLoaded;
    private bool _isSimpleViewModeLoaded;
    private bool _isSimpleSortSettingsLoaded;
    private bool _isApplyingSimpleSortSettings;

    // Сортировка тем зависит от раздела, а сортировка материалов — от темы.
    // Поэтому эти настройки хранятся отдельно для каждого родительского контекста.
    private readonly Dictionary<Guid, LibraryManagementSortMode> _topicSortBySection = new();
    private readonly Dictionary<Guid, LibraryManagementSortMode> _materialSortByTopic = new();

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

    public ObservableCollection<LibraryManagementOrderItemViewModel> SimpleTopics { get; } = [];

    public ObservableCollection<LibraryManagementOrderItemViewModel> SimpleMaterials { get; } = [];

    public ObservableCollection<LibraryManagementTopicRowViewModel> SimpleTopicRows { get; } = [];

    public ObservableCollection<LibraryManagementTopicRowViewModel> SimpleCompactTopicRows { get; } = [];

    public IReadOnlyList<LibraryManagementSectionSortOption> SimpleSectionSortOptions { get; } =
    [
        new("Мой порядок", LibraryManagementSectionSort.Custom),
        new("Последняя активность", LibraryManagementSectionSort.RecentActivity),
        new("По названию", LibraryManagementSectionSort.Name),
        new("Сначала новые", LibraryManagementSectionSort.Newest),
    ];

    public IReadOnlyList<LibraryManagementTopicSortOption> SimpleTopicSortOptions { get; } =
    [
        new("Мой порядок", LibraryManagementTopicSort.Custom),
        new("Последняя активность", LibraryManagementTopicSort.RecentActivity),
        new("По названию", LibraryManagementTopicSort.Name),
        new("Сначала новые", LibraryManagementTopicSort.Newest),
    ];

    public IReadOnlyList<LibraryManagementMaterialSortOption> SimpleMaterialSortOptions { get; } =
    [
        new("Мой порядок", LibraryManagementMaterialSort.Custom),
        new("Последняя активность", LibraryManagementMaterialSort.RecentActivity),
        new("По названию", LibraryManagementMaterialSort.Name),
        new("Сначала новые", LibraryManagementMaterialSort.Newest),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsSimpleTopicsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSimpleTopicSearchResults))]
    [NotifyPropertyChangedFor(nameof(IsSimpleMaterialsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSimpleMaterialResults))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyCanExecuteChangedFor(nameof(SaveOrderCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddTopicCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartCreateMaterialCommand))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSimpleTopicsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSimpleTopicSearchResults))]
    [NotifyPropertyChangedFor(nameof(IsSimpleMaterialsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSimpleMaterialResults))]
    [NotifyCanExecuteChangedFor(nameof(SaveOrderCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartCreateMaterialCommand))]
    private bool _isContextLoading;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveOrderCommand))]
    private bool _isSavingOrder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedSection))]
    [NotifyPropertyChangedFor(nameof(SelectedPath))]
    [NotifyPropertyChangedFor(nameof(OrderContextName))]
    [NotifyPropertyChangedFor(nameof(HasOrderContext))]
    [NotifyCanExecuteChangedFor(nameof(AddTopicCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartCreateMaterialCommand))]
    private LibraryManagementOrderItemViewModel? _selectedSection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedTopic))]
    [NotifyPropertyChangedFor(nameof(SelectedPath))]
    [NotifyPropertyChangedFor(nameof(OrderContextName))]
    [NotifyPropertyChangedFor(nameof(HasOrderContext))]
    [NotifyCanExecuteChangedFor(nameof(StartCreateMaterialCommand))]
    private LibraryManagementOrderItemViewModel? _selectedTopic;

    [ObservableProperty]
    private LibraryManagementOrderItemViewModel? _selectedMaterial;

    [ObservableProperty]
    private bool _isCreatingMaterial;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSimpleSections))]
    [NotifyPropertyChangedFor(nameof(IsSimpleSectionsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSimpleSectionSearchResults))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextSimpleSectionPageCommand))]
    private bool _isSimpleSectionsLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSimpleSectionsPaging))]
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

    private LibraryManagementViewMode _simpleSectionsViewMode = LibraryManagementViewMode.Tiles;
    private LibraryManagementViewMode _simpleTopicsViewMode = LibraryManagementViewMode.Tiles;
    private LibraryManagementViewMode _simpleMaterialsViewMode = LibraryManagementViewMode.Table;

    [ObservableProperty]
    private LibraryManagementSectionSortOption _selectedSimpleSectionSortOption =
        new("Мой порядок", LibraryManagementSectionSort.Custom);

    [ObservableProperty]
    private LibraryManagementTopicSortOption _selectedSimpleTopicSortOption =
        new("Мой порядок", LibraryManagementTopicSort.Custom);

    [ObservableProperty]
    private string? _simpleTopicSearchText;

    [ObservableProperty]
    private LibraryManagementMaterialSortOption _selectedSimpleMaterialSortOption =
        new("Мой порядок", LibraryManagementMaterialSort.Custom);

    [ObservableProperty]
    private string? _simpleMaterialSearchText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSimpleAllMaterialsFilter))]
    [NotifyPropertyChangedFor(nameof(IsSimpleArticlesFilter))]
    [NotifyPropertyChangedFor(nameof(IsSimpleQuestionsFilter))]
    [NotifyPropertyChangedFor(nameof(IsSimpleMaterialsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSimpleMaterialResults))]
    private LibraryManagementMaterialFilter _simpleMaterialFilter = LibraryManagementMaterialFilter.All;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOrderMode))]
    [NotifyPropertyChangedFor(nameof(IsSimpleMode))]
    [NotifyPropertyChangedFor(nameof(IsSimpleSectionsPage))]
    [NotifyPropertyChangedFor(nameof(IsSimpleTopicsPage))]
    [NotifyPropertyChangedFor(nameof(IsSimpleMaterialsPage))]
    [NotifyPropertyChangedFor(nameof(OrderItems))]
    [NotifyPropertyChangedFor(nameof(OrderTargetTag))]
    [NotifyPropertyChangedFor(nameof(OrderTitle))]
    [NotifyPropertyChangedFor(nameof(OrderDescription))]
    [NotifyPropertyChangedFor(nameof(OrderContextName))]
    [NotifyPropertyChangedFor(nameof(HasOrderContext))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextSimpleSectionPageCommand))]
    private LibraryOrderTarget? _activeOrderTarget;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSimpleSectionsPage))]
    [NotifyPropertyChangedFor(nameof(IsSimpleTopicsPage))]
    [NotifyPropertyChangedFor(nameof(IsSimpleMaterialsPage))]
    [NotifyPropertyChangedFor(nameof(IsSimpleTilesView))]
    [NotifyPropertyChangedFor(nameof(IsSimpleCompactTilesView))]
    [NotifyPropertyChangedFor(nameof(IsSimpleTableView))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextSimpleSectionPageCommand))]
    private LibraryManagementSimplePage _simplePage = LibraryManagementSimplePage.Sections;

    public bool IsOrderMode => ActiveOrderTarget is not null;

    public bool IsSimpleMode => !IsOrderMode;

    public bool IsSimpleSectionsPage => IsSimpleMode && SimplePage == LibraryManagementSimplePage.Sections;

    public bool IsSimpleTopicsPage => IsSimpleMode && SimplePage == LibraryManagementSimplePage.Topics;

    public bool IsSimpleMaterialsPage => IsSimpleMode && SimplePage == LibraryManagementSimplePage.Materials;

    public IEnumerable<LibraryManagementOrderItemViewModel> OrderItems =>
        ActiveOrderTarget switch
        {
            LibraryOrderTarget.Sections => Sections,
            LibraryOrderTarget.Topics => Topics,
            LibraryOrderTarget.Materials => Materials,
            _ => Array.Empty<LibraryManagementOrderItemViewModel>(),
        };

    public string OrderTargetTag =>
        ActiveOrderTarget switch
        {
            LibraryOrderTarget.Sections => "Sections",
            LibraryOrderTarget.Topics => "Topics",
            LibraryOrderTarget.Materials => "Materials",
            _ => string.Empty,
        };

    public string OrderTitle =>
        ActiveOrderTarget switch
        {
            LibraryOrderTarget.Sections => "Настроить порядок разделов",
            LibraryOrderTarget.Topics => "Настроить порядок тем",
            LibraryOrderTarget.Materials => "Настроить порядок материалов",
            _ => "Настроить порядок",
        };

    public string OrderDescription =>
        ActiveOrderTarget switch
        {
            LibraryOrderTarget.Sections => "Перетащите разделы в нужном порядке.",
            LibraryOrderTarget.Topics => "Перетащите темы внутри выбранного раздела.",
            LibraryOrderTarget.Materials => "Перетащите материалы внутри выбранной темы.",
            _ => string.Empty,
        };

    public string OrderContextName =>
        ActiveOrderTarget switch
        {
            LibraryOrderTarget.Topics => SelectedSection?.Name ?? string.Empty,
            LibraryOrderTarget.Materials => SelectedTopic?.Name ?? string.Empty,
            _ => string.Empty,
        };

    public bool HasOrderContext => !string.IsNullOrWhiteSpace(OrderContextName);

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

    private LibraryManagementViewMode CurrentSimpleViewMode =>
        SimplePage switch
        {
            LibraryManagementSimplePage.Sections => _simpleSectionsViewMode,
            LibraryManagementSimplePage.Topics => _simpleTopicsViewMode,
            LibraryManagementSimplePage.Materials => _simpleMaterialsViewMode,
            _ => LibraryManagementViewMode.Tiles,
        };

    public bool IsSimpleTilesView => CurrentSimpleViewMode == LibraryManagementViewMode.Tiles;

    public bool IsSimpleCompactTilesView => CurrentSimpleViewMode == LibraryManagementViewMode.CompactTiles;

    public bool IsSimpleTableView => CurrentSimpleViewMode == LibraryManagementViewMode.Table;

    public string SimpleSectionsShownCountText
    {
        get
        {
            int visibleCount = Math.Min(
                SimpleSectionPageSize,
                Math.Max(0, SimpleSectionsTotalCount - SimpleSectionCurrentPageOffset));

            return LibraryRangeTextFormatter.FormatEntity(
                "Разделы",
                "Разделы не найдены",
                SimpleSectionCurrentPageOffset,
                visibleCount,
                SimpleSectionsTotalCount,
                !string.IsNullOrWhiteSpace(SearchText));
        }
    }

    public bool HasSimpleTopicSource => _simpleTopicSourceTotalCount > 0;

    public bool HasSimpleTopics => SimpleTopics.Count > 0;

    public bool IsSimpleTopicsEmpty =>
        !IsContextLoading &&
        !HasError &&
        !HasSimpleTopicSource &&
        string.IsNullOrWhiteSpace(SimpleTopicSearchText);

    public bool HasNoSimpleTopicSearchResults =>
        !IsContextLoading &&
        !HasError &&
        HasSimpleTopicSource &&
        !HasSimpleTopics &&
        !string.IsNullOrWhiteSpace(SimpleTopicSearchText);

    public string SimpleTopicsShownCountText
    {
        get
        {
            int visibleCount = Math.Min(
                SimpleTopicPageSize,
                Math.Max(0, _simpleTopicsTotalCount - SimpleTopicCurrentPageOffset));

            return LibraryRangeTextFormatter.FormatEntity(
                "Темы",
                "Темы не найдены",
                SimpleTopicCurrentPageOffset,
                visibleCount,
                _simpleTopicsTotalCount,
                !string.IsNullOrWhiteSpace(SimpleTopicSearchText));
        }
    }

    public bool HasSimpleMaterialSource =>
        _simpleMaterialSourceTotalCount > 0;

    public bool HasSimpleMaterials => SimpleMaterials.Count > 0;

    public bool SimpleMaterialsHasMore =>
        _simpleMaterialWindowEndOffset < _simpleMaterialsFilteredTotalCount &&
        !_isSimpleMaterialsLoadingNextPage;

    public bool IsSimpleAllMaterialsFilter => SimpleMaterialFilter == LibraryManagementMaterialFilter.All;

    public bool IsSimpleArticlesFilter => SimpleMaterialFilter == LibraryManagementMaterialFilter.Articles;

    public bool IsSimpleQuestionsFilter => SimpleMaterialFilter == LibraryManagementMaterialFilter.Questions;

    public bool IsSimpleMaterialsEmpty =>
        !IsContextLoading &&
        !HasError &&
        !HasSimpleMaterialSource &&
        string.IsNullOrWhiteSpace(SimpleMaterialSearchText) &&
        SimpleMaterialFilter == LibraryManagementMaterialFilter.All;

    public bool HasNoSimpleMaterialResults =>
        !IsContextLoading &&
        !HasError &&
        HasSimpleMaterialSource &&
        !HasSimpleMaterials &&
        (!string.IsNullOrWhiteSpace(SimpleMaterialSearchText) ||
         SimpleMaterialFilter != LibraryManagementMaterialFilter.All);

    public string SimpleMaterialsShownCountText =>
        FormatSimpleMaterialRangeText();

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

    public bool ShowOrderFooter => IsOrderMode || HasUnsavedOrder;

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

        await EnsureSimpleViewModeLoadedAsync(cancellationToken);
        await EnsureSimpleSortSettingsLoadedAsync(cancellationToken);

        _isSimpleSectionsLoaded = true;
        LoadNextSimpleSectionPageCommand.NotifyCanExecuteChanged();

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

    /// <summary>
    /// Загружает полный список элементов текущего уровня для отдельного окна настройки порядка.
    /// Пагинация простого режима здесь намеренно не используется: для ручного порядка нужен весь уровень.
    /// </summary>
    public async Task<IReadOnlyList<LibraryManagementOrderItemViewModel>> LoadOrderItemsForDialogAsync(
        LibraryOrderTarget target,
        CancellationToken cancellationToken)
    {
        await EnsureOrderLibraryLoadedAsync(cancellationToken);

        switch (target)
        {
            case LibraryOrderTarget.Sections:
                break;

            case LibraryOrderTarget.Topics:
                if (SelectedSection is null)
                {
                    return Array.Empty<LibraryManagementOrderItemViewModel>();
                }

                await LoadTopicsAsync(SelectedTopic?.Id, cancellationToken);
                break;

            case LibraryOrderTarget.Materials:
                if (SelectedTopic is null)
                {
                    return Array.Empty<LibraryManagementOrderItemViewModel>();
                }

                await LoadMaterialsAsync(cancellationToken);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, null);
        }

        LibraryManagementOrderItemViewModel[] items =
            GetOrderCollection(target)
                .ToArray();

        return target == LibraryOrderTarget.Materials
            ? items
                .Where(material =>
                    material.IsTopLevelMaterial)
                .ToArray()
            : items;
    }

    /// <summary>
    /// Сохраняет порядок, полученный из модального окна, и синхронизирует текущие коллекции экрана.
    /// </summary>
    public async Task<bool> SaveOrderFromDialogAsync(
        LibraryOrderTarget target,
        IReadOnlyList<Guid> orderedIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(orderedIds);

        Guid? parentId = target switch
        {
            LibraryOrderTarget.Sections => null,
            LibraryOrderTarget.Topics => SelectedSection?.Id,
            LibraryOrderTarget.Materials => SelectedTopic?.Id,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };

        if (target != LibraryOrderTarget.Sections && parentId is null)
        {
            return false;
        }

        IsSavingOrder = true;
        ErrorMessage = null;

        try
        {
            IReadOnlyList<Guid> effectiveOrderedIds =
                target == LibraryOrderTarget.Materials
                    ? ExpandMaterialOrderWithLinkedQuestions(
                        orderedIds)
                    : orderedIds;

            bool wasSaved = await SaveOrderCoreAsync(
                target,
                parentId,
                effectiveOrderedIds,
                cancellationToken);

            if (!wasSaved)
            {
                return false;
            }

            ApplyOrderToCollection(
                GetOrderCollection(target),
                effectiveOrderedIds);
            ClearPendingOrder(target);
            notificationService.ShowSuccess("Порядок сохранён");

            switch (target)
            {
                case LibraryOrderTarget.Sections
                    when SelectedSimpleSectionSortOption.Sort == LibraryManagementSectionSort.Custom:
                    await ReloadSimpleSectionsCoreAsync(cancellationToken);
                    break;

                case LibraryOrderTarget.Topics:
                    if (IsSimpleTopicsPage)
                    {
                        await ReloadSimpleTopicsPagedAsync(cancellationToken);
                    }
                    break;

                case LibraryOrderTarget.Materials:
                    if (IsSimpleMaterialsPage)
                    {
                        await ReloadSimpleMaterialsPagedAsync(cancellationToken);
                    }
                    break;
            }

            NotifyOrderChanged();
            return true;
        }
        finally
        {
            IsSavingOrder = false;
        }
    }

    [RelayCommand]
    private async Task ConfigureSectionsOrderAsync(CancellationToken cancellationToken)
    {
        await EnsureOrderLibraryLoadedAsync(cancellationToken);

        if (Sections.Count == 0)
        {
            return;
        }

        BeginOrder(LibraryOrderTarget.Sections);
    }

    [RelayCommand]
    private async Task ConfigureTopicsOrderAsync(CancellationToken cancellationToken)
    {
        if (SelectedSection is null)
        {
            return;
        }

        await EnsureOrderLibraryLoadedAsync(cancellationToken);
        await LoadTopicsAsync(selectedTopicId: null, cancellationToken: cancellationToken);

        if (Topics.Count == 0)
        {
            return;
        }

        BeginOrder(LibraryOrderTarget.Topics);
    }

    [RelayCommand]
    private async Task ConfigureMaterialsOrderAsync(CancellationToken cancellationToken)
    {
        if (SelectedTopic is null)
        {
            return;
        }

        await EnsureOrderLibraryLoadedAsync(cancellationToken);
        await LoadMaterialsAsync(cancellationToken);

        if (Materials.Count == 0)
        {
            return;
        }

        BeginOrder(LibraryOrderTarget.Materials);
    }

    [RelayCommand]
    private void CancelOrder()
    {
        if (ActiveOrderTarget is null)
        {
            return;
        }

        RestoreOrderSnapshot(ActiveOrderTarget.Value);
        ClearPendingOrder(ActiveOrderTarget.Value);

        ActiveOrderTarget = null;
        _orderSnapshot = null;

        if (IsSimpleTopicsPage && SelectedSection is not null)
        {
            _ = ReloadSimpleTopicsPagedAsync(_viewCancellationToken);
        }
        else if (IsSimpleMaterialsPage && SelectedTopic is not null)
        {
            _ = ReloadSimpleMaterialsPagedAsync(_viewCancellationToken);
        }

        NotifyOrderChanged();
    }

    private void BeginOrder(LibraryOrderTarget target)
    {
        ActiveOrderTarget = target;
        _orderSnapshot = GetOrderCollection(target)
            .Select(item => item.Id)
            .ToArray();
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

        // Normal browsing must stay lightweight: do not materialize the full library tree.
        SelectedTopic = null;
        SelectedSection = new LibraryManagementOrderItemViewModel(item.Source, position: 1);
        SimpleTopicSearchText = null;
        SimplePage = LibraryManagementSimplePage.Topics;

        await ReloadSimpleTopicsPagedAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task OpenSimpleTopicAsync(
        LibraryManagementOrderItemViewModel? item,
        CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        SimpleMaterialSearchText = null;
        SimpleMaterialFilter = LibraryManagementMaterialFilter.All;
        SelectedTopic = item;
        SimplePage = LibraryManagementSimplePage.Materials;

        await ReloadSimpleMaterialsPagedAsync(cancellationToken);
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

    [RelayCommand(CanExecute = nameof(CanStartCreateMaterial))]
    private void StartCreateMaterial()
    {
        if (!CanStartCreateMaterial() ||
            SelectedTopic is null)
        {
            return;
        }

        CreateMaterial.Initialize(
            SelectedTopic,
            CancelCreateMaterial,
            CompleteCreateMaterial);

        IsCreatingMaterial = true;
    }

    private bool CanStartCreateMaterial() =>
        SelectedSection is not null &&
        SelectedTopic is not null &&
        !IsLoading &&
        !IsContextLoading;

    [RelayCommand]
    private void CancelCreateMaterial()
    {
        IsCreatingMaterial = false;
        CreateMaterial.Reset();
    }

    private async void CompleteCreateMaterial()
    {
        string message =
            CreateMaterial.IsQuestionMaterial
                ? "Вопрос создан"
                : "Статья создана";

        _preferredSectionId = SelectedSection?.Id;
        _preferredTopicId = SelectedTopic?.Id;

        IsCreatingMaterial = false;
        CreateMaterial.Reset();

        notificationService.ShowSuccess(message);

        try
        {
            await RefreshAfterMutationAsync(
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Материал создан, но не удалось обновить библиотеку");

            ErrorMessage =
                "Материал создан, но список библиотеки не удалось обновить. Нажмите «Попробовать снова».";
        }
    }

    [RelayCommand]
    private void SelectAllSimpleMaterials()
    {
        SimpleMaterialFilter = LibraryManagementMaterialFilter.All;
    }

    [RelayCommand]
    private void SelectSimpleArticles()
    {
        SimpleMaterialFilter = LibraryManagementMaterialFilter.Articles;
    }

    [RelayCommand]
    private void SelectSimpleQuestions()
    {
        SimpleMaterialFilter = LibraryManagementMaterialFilter.Questions;
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

        if (section is null && (item?.Id ?? SelectedSection?.Id) is Guid sectionId)
        {
            await EnsureOrderLibraryLoadedAsync(cancellationToken);
            section = FindSection(sectionId);
        }

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

        if (topic is null && item is not null)
        {
            await EnsureOrderLibraryLoadedAsync(cancellationToken);
            topic = FindTopic(item.Id);
        }

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

        if (topic is null && item is not null)
        {
            await EnsureOrderLibraryLoadedAsync(cancellationToken);
            topic = FindTopic(item.Id);
        }

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
        LibraryOrderTarget? savedTarget = ActiveOrderTarget;
        IsSavingOrder = true;
        ErrorMessage = null;
        bool wasSaved = false;

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

            if (savedTarget == LibraryOrderTarget.Sections &&
                SelectedSimpleSectionSortOption.Sort == LibraryManagementSectionSort.Custom)
            {
                await ReloadSimpleSectionsCoreAsync(cancellationToken);
            }
            else if (savedTarget == LibraryOrderTarget.Topics && IsSimpleTopicsPage)
            {
                await ReloadSimpleTopicsPagedAsync(cancellationToken);
            }
            else if (savedTarget == LibraryOrderTarget.Materials && IsSimpleMaterialsPage)
            {
                await ReloadSimpleMaterialsPagedAsync(cancellationToken);
            }

            wasSaved = true;
        }
        finally
        {
            IsSavingOrder = false;

            if (wasSaved)
            {
                ActiveOrderTarget = null;
                _orderSnapshot = null;
            }

            NotifyOrderChanged();
        }
    }

    [RelayCommand]
    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        await ReloadSimpleSectionsCoreAsync(cancellationToken);

        if (IsOrderMode || _isLoaded)
        {
            _preferredSectionId = SelectedSection?.Id;
            _preferredTopicId = SelectedTopic?.Id;
            await ReloadOrderLibraryAsync(cancellationToken);
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

        await LoadNextSimpleSectionWindowAsync(
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

        await EnsureOrderLibraryLoadedAsync(cancellationToken);

        LibraryManagementOrderItemViewModel? orderItem =
            Sections.FirstOrDefault(section => section.Id == item.Id);

        await EditSectionAsync(orderItem, cancellationToken);
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

        await EnsureOrderLibraryLoadedAsync(cancellationToken);

        LibraryManagementOrderItemViewModel? orderItem =
            Sections.FirstOrDefault(section => section.Id == item.Id);

        await DeleteSectionAsync(orderItem, cancellationToken);
    }

    partial void OnSearchTextChanged(string? value)
    {
        int searchVersion = Interlocked.Increment(ref _simpleSectionSearchVersion);

        if (_isSimpleSectionsLoaded && IsSimpleSectionsPage)
        {
            _ = ReloadSimpleSectionsAfterSearchDelayAsync(searchVersion);
        }
    }

    partial void OnSelectedSimpleSectionSortOptionChanged(LibraryManagementSectionSortOption value)
    {
        if (_isApplyingSimpleSortSettings)
        {
            return;
        }

        _ = SaveSimpleSectionSortAsync(value.Sort);

        if (_isSimpleSectionsLoaded)
        {
            _ = ReloadSimpleSectionsAfterSortChangedAsync();
        }
    }

    partial void OnSimpleTopicSearchTextChanged(string? value)
    {
        int searchVersion = Interlocked.Increment(ref _simpleTopicSearchVersion);
        _ = ApplySimpleTopicFilterAfterDelayAsync(searchVersion);
    }

    partial void OnSelectedSimpleTopicSortOptionChanged(LibraryManagementTopicSortOption value)
    {
        if (_isApplyingSimpleSortSettings)
        {
            return;
        }

        if (SelectedSection is { } section)
        {
            LibraryManagementSortMode sortMode = ToSettingsSort(value.Sort);
            _topicSortBySection[section.Id] = sortMode;
            _ = SaveSimpleTopicSortAsync(section.Id, value.Sort);
        }

        if (IsSimpleTopicsPage && SelectedSection is not null)
        {
            _ = ReloadSimpleTopicsPagedAsync(_viewCancellationToken);
        }
        else
        {
            ApplySimpleTopicFilterAndSort();
        }
    }

    partial void OnSimpleMaterialSearchTextChanged(string? value)
    {
        int searchVersion = Interlocked.Increment(ref _simpleMaterialSearchVersion);
        _ = ApplySimpleMaterialFilterAfterDelayAsync(searchVersion);
    }

    partial void OnSelectedSimpleMaterialSortOptionChanged(LibraryManagementMaterialSortOption value)
    {
        if (_isApplyingSimpleSortSettings)
        {
            return;
        }

        if (SelectedTopic is { } topic)
        {
            LibraryManagementSortMode sortMode = ToSettingsSort(value.Sort);
            _materialSortByTopic[topic.Id] = sortMode;
            _ = SaveSimpleMaterialSortAsync(topic.Id, value.Sort);
        }

        if (IsSimpleMaterialsPage && SelectedTopic is not null)
        {
            _ = ReloadSimpleMaterialsPagedAsync(_viewCancellationToken);
        }
    }

    partial void OnSimpleMaterialFilterChanged(LibraryManagementMaterialFilter value)
    {
        if (IsSimpleMaterialsPage && SelectedTopic is not null)
        {
            _ = ReloadSimpleMaterialsPagedAsync(_viewCancellationToken);
        }
    }

    partial void OnSelectedSectionChanged(LibraryManagementOrderItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedPath));
        ApplyTopicSortForSection(value?.Id);

        if (!_isLoaded || _suppressSelectionReload || !IsOrderMode)
        {
            return;
        }

        _ = ChangeSectionContextSafelyAsync(value);
    }

    partial void OnSelectedTopicChanged(LibraryManagementOrderItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedPath));
        ApplyMaterialSortForTopic(value?.Id);

        if (!_isLoaded || _suppressSelectionReload || !IsOrderMode)
        {
            return;
        }

        _ = ChangeTopicContextSafelyAsync(value);
    }

    private async Task EnsureOrderLibraryLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded && _library.Count > 0)
        {
            return;
        }

        await ReloadOrderLibraryAsync(cancellationToken);
    }

    private Task ReloadOrderLibraryAsync(CancellationToken cancellationToken)
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
               _simpleSectionWindow.HasNext &&
               !IsSimpleSectionsLoading &&
               !IsSimpleSectionsLoadingNextPage &&
               !HasError &&
               !HasSimpleSectionsNextPageError;
    }

    private Task LoadNextSimpleSectionPageWithLinkedCancellationAsync(
        CancellationToken cancellationToken)
    {
        return LoadNextSimpleSectionWindowAsync(cancellationToken);
    }

    private async Task ReloadSimpleSectionsCoreAsync(CancellationToken cancellationToken)
    {
        int loadVersion = ResetSimpleSectionPagingState(cancellationToken);
        IsSimpleSectionsLoading = true;
        ErrorMessage = null;

        try
        {
            LibraryManagementSectionsPageDto? page = await GetSimpleSectionPageAsync(
                offset: 0,
                loadVersion,
                _simpleSectionContextCancellation!.Token,
                reportFailure: true);

            if (page is null || loadVersion != _simpleSectionLoadVersion)
            {
                return;
            }

            ApplySectionPageTotals(page);
            _simpleSectionWindow.ShowPage(0, page.Items, PageWindowInsert.Append);
            RebuildSimpleSectionWindow();
            SyncSimpleSectionPagingProperties();
        }
        finally
        {
            if (loadVersion == _simpleSectionLoadVersion)
            {
                IsSimpleSectionsLoading = false;
                SyncSimpleSectionPagingProperties();
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

        if (row is null)
        {
            // В первой строке одно место занимает плитка «Создать раздел».
            row = new LibraryManagementSectionRowViewModel(
                Math.Max(1, capacity - 1),
                isFirstRow: true);
            rows.Add(row);
        }
        else if (row.IsFull)
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
            _simpleSectionsViewMode = settings.LibraryManagementSectionsViewMode;
            _simpleTopicsViewMode = settings.LibraryManagementTopicsViewMode;
            _simpleMaterialsViewMode = settings.LibraryManagementMaterialsViewMode;
            _isSimpleViewModeLoaded = true;
            NotifySimpleViewModeChanged();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось загрузить режимы отображения управления библиотекой");
            _simpleSectionsViewMode = LibraryManagementViewMode.Tiles;
            _simpleTopicsViewMode = LibraryManagementViewMode.Tiles;
            _simpleMaterialsViewMode = LibraryManagementViewMode.Table;
            _isSimpleViewModeLoaded = true;
            NotifySimpleViewModeChanged();
        }
    }

    private async Task EnsureSimpleSortSettingsLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isSimpleSortSettingsLoaded)
        {
            return;
        }

        try
        {
            AppSettings settings = await settingsService.LoadAsync(cancellationToken);

            _topicSortBySection.Clear();
            foreach (var pair in settings.LibraryManagementTopicSortBySection)
            {
                _topicSortBySection[pair.Key] = pair.Value;
            }

            _materialSortByTopic.Clear();
            foreach (var pair in settings.LibraryManagementMaterialSortByTopic)
            {
                _materialSortByTopic[pair.Key] = pair.Value;
            }

            _isApplyingSimpleSortSettings = true;

            SelectedSimpleSectionSortOption = SimpleSectionSortOptions.First(option =>
                option.Sort == ToSectionSort(settings.LibraryManagementSectionSort));

            // До выбора конкретного раздела/темы дочерние сортировки остаются
            // в значении по умолчанию. При смене контекста они восстанавливаются
            // из словарей выше.
            SelectedSimpleTopicSortOption = SimpleTopicSortOptions.First(option =>
                option.Sort == LibraryManagementTopicSort.Custom);

            SelectedSimpleMaterialSortOption = SimpleMaterialSortOptions.First(option =>
                option.Sort == LibraryManagementMaterialSort.Custom);

            _isSimpleSortSettingsLoaded = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось загрузить настройки сортировки управления библиотекой");

            _topicSortBySection.Clear();
            _materialSortByTopic.Clear();

            _isApplyingSimpleSortSettings = true;
            SelectedSimpleSectionSortOption = SimpleSectionSortOptions.First(option =>
                option.Sort == LibraryManagementSectionSort.Custom);
            SelectedSimpleTopicSortOption = SimpleTopicSortOptions.First(option =>
                option.Sort == LibraryManagementTopicSort.Custom);
            SelectedSimpleMaterialSortOption = SimpleMaterialSortOptions.First(option =>
                option.Sort == LibraryManagementMaterialSort.Custom);

            _isSimpleSortSettingsLoaded = true;
        }
        finally
        {
            _isApplyingSimpleSortSettings = false;
        }
    }

    private void ApplyTopicSortForSection(Guid? sectionId)
    {
        if (!_isSimpleSortSettingsLoaded || sectionId is null)
        {
            return;
        }

        LibraryManagementSortMode sortMode =
            _topicSortBySection.TryGetValue(sectionId.Value, out LibraryManagementSortMode savedSort)
                ? savedSort
                : LibraryManagementSortMode.Custom;

        bool wasApplying = _isApplyingSimpleSortSettings;
        _isApplyingSimpleSortSettings = true;

        try
        {
            SelectedSimpleTopicSortOption = SimpleTopicSortOptions.First(option =>
                option.Sort == ToTopicSort(sortMode));
        }
        finally
        {
            _isApplyingSimpleSortSettings = wasApplying;
        }
    }

    private void ApplyMaterialSortForTopic(Guid? topicId)
    {
        if (!_isSimpleSortSettingsLoaded || topicId is null)
        {
            return;
        }

        LibraryManagementSortMode sortMode =
            _materialSortByTopic.TryGetValue(topicId.Value, out LibraryManagementSortMode savedSort)
                ? savedSort
                : LibraryManagementSortMode.Custom;

        bool wasApplying = _isApplyingSimpleSortSettings;
        _isApplyingSimpleSortSettings = true;

        try
        {
            SelectedSimpleMaterialSortOption = SimpleMaterialSortOptions.First(option =>
                option.Sort == ToMaterialSort(sortMode));
        }
        finally
        {
            _isApplyingSimpleSortSettings = wasApplying;
        }
    }

    private async Task SaveSimpleSectionSortAsync(LibraryManagementSectionSort sort)
    {
        try
        {
            await settingsService.SaveLibraryManagementSectionSortAsync(
                ToSettingsSort(sort),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось сохранить сортировку разделов управления библиотекой");
        }
    }

    private async Task SaveSimpleTopicSortAsync(
        Guid sectionId,
        LibraryManagementTopicSort sort)
    {
        try
        {
            await settingsService.SaveLibraryManagementTopicSortAsync(
                sectionId,
                ToSettingsSort(sort),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Не удалось сохранить сортировку тем раздела {SectionId}",
                sectionId);
        }
    }

    private async Task SaveSimpleMaterialSortAsync(
        Guid topicId,
        LibraryManagementMaterialSort sort)
    {
        try
        {
            await settingsService.SaveLibraryManagementMaterialSortAsync(
                topicId,
                ToSettingsSort(sort),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Не удалось сохранить сортировку материалов темы {TopicId}",
                topicId);
        }
    }

    private static LibraryManagementSectionSort ToSectionSort(LibraryManagementSortMode sort) =>
        sort switch
        {
            LibraryManagementSortMode.Custom => LibraryManagementSectionSort.Custom,
            LibraryManagementSortMode.RecentActivity => LibraryManagementSectionSort.RecentActivity,
            LibraryManagementSortMode.Name => LibraryManagementSectionSort.Name,
            LibraryManagementSortMode.Newest => LibraryManagementSectionSort.Newest,
            _ => LibraryManagementSectionSort.Custom,
        };

    private static LibraryManagementTopicSort ToTopicSort(LibraryManagementSortMode sort) =>
        sort switch
        {
            LibraryManagementSortMode.Custom => LibraryManagementTopicSort.Custom,
            LibraryManagementSortMode.RecentActivity => LibraryManagementTopicSort.RecentActivity,
            LibraryManagementSortMode.Name => LibraryManagementTopicSort.Name,
            LibraryManagementSortMode.Newest => LibraryManagementTopicSort.Newest,
            _ => LibraryManagementTopicSort.Custom,
        };

    private static LibraryManagementMaterialSort ToMaterialSort(LibraryManagementSortMode sort) =>
        sort switch
        {
            LibraryManagementSortMode.Custom => LibraryManagementMaterialSort.Custom,
            LibraryManagementSortMode.RecentActivity => LibraryManagementMaterialSort.RecentActivity,
            LibraryManagementSortMode.Name => LibraryManagementMaterialSort.Name,
            LibraryManagementSortMode.Newest => LibraryManagementMaterialSort.Newest,
            _ => LibraryManagementMaterialSort.Custom,
        };

    private static LibraryManagementSortMode ToSettingsSort(LibraryManagementSectionSort sort) =>
        sort switch
        {
            LibraryManagementSectionSort.Custom => LibraryManagementSortMode.Custom,
            LibraryManagementSectionSort.RecentActivity => LibraryManagementSortMode.RecentActivity,
            LibraryManagementSectionSort.Name => LibraryManagementSortMode.Name,
            LibraryManagementSectionSort.Newest => LibraryManagementSortMode.Newest,
            _ => LibraryManagementSortMode.Custom,
        };

    private static LibraryManagementSortMode ToSettingsSort(LibraryManagementTopicSort sort) =>
        sort switch
        {
            LibraryManagementTopicSort.Custom => LibraryManagementSortMode.Custom,
            LibraryManagementTopicSort.RecentActivity => LibraryManagementSortMode.RecentActivity,
            LibraryManagementTopicSort.Name => LibraryManagementSortMode.Name,
            LibraryManagementTopicSort.Newest => LibraryManagementSortMode.Newest,
            _ => LibraryManagementSortMode.Custom,
        };

    private static LibraryManagementSortMode ToSettingsSort(LibraryManagementMaterialSort sort) =>
        sort switch
        {
            LibraryManagementMaterialSort.Custom => LibraryManagementSortMode.Custom,
            LibraryManagementMaterialSort.RecentActivity => LibraryManagementSortMode.RecentActivity,
            LibraryManagementMaterialSort.Name => LibraryManagementSortMode.Name,
            LibraryManagementMaterialSort.Newest => LibraryManagementSortMode.Newest,
            _ => LibraryManagementSortMode.Custom,
        };

    private async Task SetSimpleViewModeAsync(
        LibraryManagementViewMode viewMode,
        CancellationToken cancellationToken)
    {
        LibraryManagementSimplePage page = SimplePage;

        if (CurrentSimpleViewMode == viewMode && _isSimpleViewModeLoaded)
        {
            return;
        }

        switch (page)
        {
            case LibraryManagementSimplePage.Sections:
                _simpleSectionsViewMode = viewMode;
                break;
            case LibraryManagementSimplePage.Topics:
                _simpleTopicsViewMode = viewMode;
                break;
            case LibraryManagementSimplePage.Materials:
                _simpleMaterialsViewMode = viewMode;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(page), page, null);
        }

        _isSimpleViewModeLoaded = true;
        NotifySimpleViewModeChanged();

        try
        {
            switch (page)
            {
                case LibraryManagementSimplePage.Sections:
                    await settingsService.SaveLibraryManagementSectionsViewModeAsync(viewMode, cancellationToken);
                    break;
                case LibraryManagementSimplePage.Topics:
                    await settingsService.SaveLibraryManagementTopicsViewModeAsync(viewMode, cancellationToken);
                    break;
                case LibraryManagementSimplePage.Materials:
                    await settingsService.SaveLibraryManagementMaterialsViewModeAsync(viewMode, cancellationToken);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(page), page, null);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Не удалось сохранить режим отображения управления библиотекой для страницы {Page}",
                page);
        }
    }

    private void NotifySimpleViewModeChanged()
    {
        OnPropertyChanged(nameof(IsSimpleTilesView));
        OnPropertyChanged(nameof(IsSimpleCompactTilesView));
        OnPropertyChanged(nameof(IsSimpleTableView));
    }

    private async Task RefreshAfterMutationAsync(CancellationToken cancellationToken)
    {
        await ReloadSimpleSectionsCoreAsync(cancellationToken);

        if (IsOrderMode)
        {
            await ReloadOrderLibraryAsync(cancellationToken);
            return;
        }

        // The full tree is an admin/order cache. A mutation invalidates it, but normal
        // browsing must not eagerly rebuild it.
        _isLoaded = false;
        _library.Clear();
        Sections.Clear();
        Topics.Clear();
        Materials.Clear();

        switch (SimplePage)
        {
            case LibraryManagementSimplePage.Topics when SelectedSection is not null:
                await ReloadSimpleTopicsPagedAsync(cancellationToken);
                break;

            case LibraryManagementSimplePage.Materials when SelectedTopic is not null:
                await ReloadSimpleMaterialsPagedAsync(cancellationToken);
                break;
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
        SimpleTopics.Clear();
        SimpleTopicRows.Clear();
        SimpleCompactTopicRows.Clear();
        NotifySimpleTopicsStateChanged();

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
            SelectedTopic = Topics.FirstOrDefault(topic => topic.Id == selectedTopicId);
        }
        finally
        {
            _suppressSelectionReload = false;
        }

        ApplySimpleTopicFilterAndSort();
    }

    private async Task LoadMaterialsAsync(CancellationToken cancellationToken)
    {
        ResetSimpleMaterialPage();
        Materials.Clear();
        SimpleMaterials.Clear();
        SelectedMaterial = null;

        if (SelectedTopic is null)
        {
            NotifySimpleMaterialsStateChanged();
            NotifyCollectionStateChanged();
            return;
        }

        var result = await LoadOrderItemsAsync(
            LibraryOrderTarget.Materials,
            SelectedTopic.Id,
            cancellationToken);

        if (result is null)
        {
            NotifySimpleMaterialsStateChanged();
            return;
        }

        _pendingMaterialOrders.TryGetValue(SelectedTopic.Id, out Guid[]? pendingOrder);
        IReadOnlyList<LibraryOrderItemDto> orderedItems = ApplyPendingOrder(result, pendingOrder);

        IReadOnlyList<LibraryMaterialDto> topicMaterials =
            GetTopicMaterials(
                FindTopic(
                    SelectedTopic.Id));

        var materialsById =
            topicMaterials.ToDictionary(
                material => material.Id);

        var questionCountsByArticleId =
            topicMaterials
                .Where(material =>
                    string.Equals(
                        material.Type,
                        "Question",
                        StringComparison.OrdinalIgnoreCase) &&
                    material.ArticleId is not null)
                .GroupBy(material =>
                    material.ArticleId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count());

        int position = 1;

        foreach (var orderItem in orderedItems)
        {
            materialsById.TryGetValue(
                orderItem.Id,
                out LibraryMaterialDto? material);

            int articleQuestionCount =
                material is not null &&
                string.Equals(
                    material.Type,
                    "Article",
                    StringComparison.OrdinalIgnoreCase)
                    ? questionCountsByArticleId.GetValueOrDefault(
                        material.Id)
                    : 0;

            Materials.Add(
                new LibraryManagementOrderItemViewModel(
                    orderItem,
                    LibraryOrderTarget.Materials,
                    position++,
                    material: material,
                    articleQuestionCount: articleQuestionCount));
        }

        if (pendingOrder is not null)
        {
            _pendingMaterialOrders[SelectedTopic.Id] = Materials.Select(material => material.Id).ToArray();
        }

        SelectedMaterial = Materials.FirstOrDefault(material =>
            material.IsTopLevelMaterial);
        ApplySimpleMaterialFilterAndSort();
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

    private IReadOnlyList<Guid> ExpandMaterialOrderWithLinkedQuestions(
        IReadOnlyList<Guid> topLevelOrderedIds)
    {
        var linkedQuestionsByArticleId =
            Materials
                .Where(material =>
                    material.IsLinkedQuestion &&
                    material.Material?.ArticleId is not null)
                .GroupBy(material =>
                    material.Material!.ArticleId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(material =>
                            material.Position)
                        .Select(material =>
                            material.Id)
                        .ToArray());

        var result =
            new List<Guid>(
                Materials.Count);

        var added =
            new HashSet<Guid>();

        foreach (Guid materialId
                 in topLevelOrderedIds)
        {
            if (!added.Add(
                    materialId))
            {
                continue;
            }

            result.Add(
                materialId);

            if (!linkedQuestionsByArticleId.TryGetValue(
                    materialId,
                    out Guid[]? linkedQuestionIds))
            {
                continue;
            }

            foreach (Guid questionId
                     in linkedQuestionIds)
            {
                if (added.Add(
                        questionId))
                {
                    result.Add(
                        questionId);
                }
            }
        }

        // Защита от старых или неконсистентных данных:
        // ни один material id не должен потеряться при сохранении порядка.
        foreach (LibraryManagementOrderItemViewModel material
                 in Materials.OrderBy(item =>
                     item.Position))
        {
            if (added.Add(
                    material.Id))
            {
                result.Add(
                    material.Id);
            }
        }

        return result;
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

    private async Task ApplySimpleTopicFilterAfterDelayAsync(int searchVersion)
    {
        try
        {
            await Task.Delay(SearchDelay, _viewCancellationToken);

            if (searchVersion == Volatile.Read(ref _simpleTopicSearchVersion) &&
                IsSimpleTopicsPage &&
                SelectedSection is not null)
            {
                await ReloadSimpleTopicsPagedAsync(_viewCancellationToken);
            }
        }
        catch (OperationCanceledException) when (_viewCancellationToken.IsCancellationRequested)
        {
            // ignore
        }
    }

    private void ApplySimpleTopicFilterAndSort()
    {
        IEnumerable<LibraryManagementOrderItemViewModel> topics = Topics;

        if (!string.IsNullOrWhiteSpace(SimpleTopicSearchText))
        {
            string search = SimpleTopicSearchText.Trim();
            topics = topics.Where(topic =>
                topic.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        topics = SelectedSimpleTopicSortOption.Sort switch
        {
            LibraryManagementTopicSort.Custom => topics
                .OrderBy(topic => topic.Position),

            LibraryManagementTopicSort.RecentActivity => topics
                .OrderByDescending(topic => topic.TopicLastActivityAt)
                .ThenBy(topic => topic.Name, StringComparer.OrdinalIgnoreCase),

            LibraryManagementTopicSort.Name => topics
                .OrderBy(topic => topic.Name, StringComparer.OrdinalIgnoreCase),

            LibraryManagementTopicSort.Newest => topics
                .OrderByDescending(topic => topic.TopicCreatedAt)
                .ThenBy(topic => topic.Name, StringComparer.OrdinalIgnoreCase),

            _ => topics.OrderBy(topic => topic.Position),
        };

        SimpleTopics.Clear();
        SimpleTopicRows.Clear();
        SimpleCompactTopicRows.Clear();

        foreach (LibraryManagementOrderItemViewModel topic in topics)
        {
            SimpleTopics.Add(topic);
            AddSimpleTopicToRows(SimpleTopicRows, topic, 3);
            AddSimpleTopicToRows(SimpleCompactTopicRows, topic, 4);
        }

        NotifySimpleTopicsStateChanged();
    }

    private static void AddSimpleTopicToRows(
        ObservableCollection<LibraryManagementTopicRowViewModel> rows,
        LibraryManagementOrderItemViewModel topic,
        int capacity)
    {
        LibraryManagementTopicRowViewModel? row = rows.LastOrDefault();

        if (row is null)
        {
            // В первой строке одно место занимает плитка «Создать тему».
            row = new LibraryManagementTopicRowViewModel(
                Math.Max(1, capacity - 1),
                isFirstRow: true);
            rows.Add(row);
        }
        else if (row.IsFull)
        {
            row = new LibraryManagementTopicRowViewModel(capacity);
            rows.Add(row);
        }

        row.Add(topic);
    }

    private async Task ApplySimpleMaterialFilterAfterDelayAsync(int searchVersion)
    {
        try
        {
            await Task.Delay(SearchDelay, _viewCancellationToken);

            if (searchVersion == Volatile.Read(ref _simpleMaterialSearchVersion) &&
                IsSimpleMaterialsPage &&
                SelectedTopic is not null)
            {
                await ReloadSimpleMaterialsPagedAsync(_viewCancellationToken);
            }
        }
        catch (OperationCanceledException) when (_viewCancellationToken.IsCancellationRequested)
        {
            // ignore
        }
    }

    private void ApplySimpleMaterialFilterAndSort()
    {
        IEnumerable<LibraryManagementOrderItemViewModel> materials =
            Materials.Where(material =>
                material.IsTopLevelMaterial);

        if (!string.IsNullOrWhiteSpace(
                SimpleMaterialSearchText))
        {
            string search =
                SimpleMaterialSearchText.Trim();

            materials =
                materials.Where(material =>
                    material.Name.Contains(
                        search,
                        StringComparison.OrdinalIgnoreCase));
        }

        materials = SimpleMaterialFilter switch
        {
            LibraryManagementMaterialFilter.Articles =>
                materials.Where(material =>
                    material.IsArticle),

            LibraryManagementMaterialFilter.Questions =>
                materials.Where(material =>
                    string.Equals(
                        material.Material?.Type,
                        "Question",
                        StringComparison.OrdinalIgnoreCase)),

            _ => materials,
        };

        materials =
            SelectedSimpleMaterialSortOption.Sort switch
            {
                LibraryManagementMaterialSort.Custom =>
                    materials.OrderBy(material =>
                        material.Position),

                LibraryManagementMaterialSort.RecentActivity =>
                    materials
                        .OrderByDescending(material =>
                            material.Material?.UpdatedAt ??
                            DateTime.MinValue)
                        .ThenBy(
                            material => material.Name,
                            StringComparer.OrdinalIgnoreCase),

                LibraryManagementMaterialSort.Name =>
                    materials.OrderBy(
                        material => material.Name,
                        StringComparer.OrdinalIgnoreCase),

                LibraryManagementMaterialSort.Newest =>
                    materials
                        .OrderByDescending(material =>
                            material.Material?.CreatedAt ??
                            DateTime.MinValue)
                        .ThenBy(
                            material => material.Name,
                            StringComparer.OrdinalIgnoreCase),

                _ =>
                    materials.OrderBy(material =>
                        material.Position),
            };

        LibraryManagementOrderItemViewModel[] filteredMaterials =
            materials.ToArray();

        _simpleMaterialsFilteredTotalCount =
            filteredMaterials.Length;

        int visibleCount =
            Math.Min(
                _simpleMaterialVisibleCount,
                _simpleMaterialsFilteredTotalCount);

        SimpleMaterials.Clear();

        foreach (LibraryManagementOrderItemViewModel material
                 in filteredMaterials.Take(visibleCount))
        {
            SimpleMaterials.Add(
                material);
        }

        NotifySimpleMaterialsStateChanged();
    }

    private void ResetSimpleMaterialPage()
    {
        _simpleMaterialVisibleCount =
            SimpleMaterialPageSize;
    }

    [RelayCommand(CanExecute = nameof(CanLoadNextSimpleMaterialPage))]
    private Task LoadNextSimpleMaterialPageAsync(CancellationToken cancellationToken)
    {
        return LoadNextSimpleMaterialWindowAsync(cancellationToken);
    }

    private bool CanLoadNextSimpleMaterialPage() =>
        IsSimpleMaterialsPage &&
        !IsContextLoading &&
        !_isSimpleMaterialsLoadingNextPage &&
        SimpleMaterialsHasMore;

    private ObservableCollection<LibraryManagementOrderItemViewModel> GetOrderCollection(
        LibraryOrderTarget target)
    {
        return target switch
        {
            LibraryOrderTarget.Sections => Sections,
            LibraryOrderTarget.Topics => Topics,
            LibraryOrderTarget.Materials => Materials,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };
    }

    private static void ApplyOrderToCollection(
        ObservableCollection<LibraryManagementOrderItemViewModel> collection,
        IReadOnlyList<Guid> orderedIds)
    {
        var itemsById = collection.ToDictionary(item => item.Id);
        var orderedItems = new List<LibraryManagementOrderItemViewModel>(collection.Count);

        foreach (Guid id in orderedIds)
        {
            if (itemsById.Remove(id, out LibraryManagementOrderItemViewModel? item))
            {
                orderedItems.Add(item);
            }
        }

        orderedItems.AddRange(collection.Where(item => itemsById.ContainsKey(item.Id)));

        collection.Clear();

        for (int index = 0; index < orderedItems.Count; index++)
        {
            orderedItems[index].Position = index + 1;
            collection.Add(orderedItems[index]);
        }
    }

    private void RestoreOrderSnapshot(LibraryOrderTarget target)
    {
        if (_orderSnapshot is null)
        {
            return;
        }

        ObservableCollection<LibraryManagementOrderItemViewModel> collection =
            GetOrderCollection(target);

        var itemsById = collection.ToDictionary(item => item.Id);
        var restored = new List<LibraryManagementOrderItemViewModel>(collection.Count);

        foreach (Guid id in _orderSnapshot)
        {
            if (itemsById.Remove(id, out LibraryManagementOrderItemViewModel? item))
            {
                restored.Add(item);
            }
        }

        restored.AddRange(collection.Where(item => itemsById.ContainsKey(item.Id)));

        collection.Clear();

        for (int index = 0; index < restored.Count; index++)
        {
            restored[index].Position = index + 1;
            collection.Add(restored[index]);
        }
    }

    private void ClearPendingOrder(LibraryOrderTarget target)
    {
        switch (target)
        {
            case LibraryOrderTarget.Sections:
                _pendingSectionOrder = null;
                break;

            case LibraryOrderTarget.Topics when SelectedSection is not null:
                _pendingTopicOrders.Remove(SelectedSection.Id);
                break;

            case LibraryOrderTarget.Materials when SelectedTopic is not null:
                _pendingMaterialOrders.Remove(SelectedTopic.Id);
                break;
        }
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
        OnPropertyChanged(nameof(SimpleSectionsHasPrevious));
        OnPropertyChanged(nameof(IsSimpleSectionsPaging));
        OnPropertyChanged(nameof(SimpleSectionWindowStartOffset));
        OnPropertyChanged(nameof(SimpleSectionWindowEndOffset));
        OnPropertyChanged(nameof(SimpleSectionCurrentPageOffset));
        OnPropertyChanged(nameof(SimpleSectionCachedPageCount));
        LoadNextSimpleSectionPageCommand.NotifyCanExecuteChanged();
    }

    private void NotifySimpleTopicsStateChanged()
    {
        OnPropertyChanged(nameof(HasSimpleTopicSource));
        OnPropertyChanged(nameof(HasSimpleTopics));
        OnPropertyChanged(nameof(IsSimpleTopicsEmpty));
        OnPropertyChanged(nameof(HasNoSimpleTopicSearchResults));
        OnPropertyChanged(nameof(SimpleTopicsShownCountText));
        OnPropertyChanged(nameof(SimpleTopicsHasMore));
        OnPropertyChanged(nameof(SimpleTopicsHasPrevious));
        OnPropertyChanged(nameof(IsSimpleTopicsPaging));
        OnPropertyChanged(nameof(SimpleTopicsTotalCount));
        OnPropertyChanged(nameof(SimpleTopicWindowStartOffset));
        OnPropertyChanged(nameof(SimpleTopicWindowEndOffset));
        OnPropertyChanged(nameof(SimpleTopicCurrentPageOffset));
        OnPropertyChanged(nameof(SimpleTopicCachedPageCount));
        LoadNextSimpleTopicPageCommand.NotifyCanExecuteChanged();
    }

    private void NotifySimpleMaterialsStateChanged()
    {
        OnPropertyChanged(nameof(HasSimpleMaterialSource));
        OnPropertyChanged(nameof(HasSimpleMaterials));
        OnPropertyChanged(nameof(IsSimpleMaterialsEmpty));
        OnPropertyChanged(nameof(HasNoSimpleMaterialResults));
        OnPropertyChanged(nameof(SimpleMaterialsShownCountText));
        OnPropertyChanged(nameof(SimpleMaterialsHasMore));
        OnPropertyChanged(nameof(SimpleMaterialsHasPrevious));
        OnPropertyChanged(nameof(IsSimpleMaterialsLoadingNextPage));
        OnPropertyChanged(nameof(IsSimpleMaterialsLoadingPreviousPage));
        OnPropertyChanged(nameof(IsSimpleMaterialsPaging));
        OnPropertyChanged(nameof(SimpleMaterialWindowStartOffset));
        OnPropertyChanged(nameof(SimpleMaterialWindowEndOffset));
        OnPropertyChanged(nameof(SimpleMaterialCachedPageCount));
        LoadNextSimpleMaterialPageCommand.NotifyCanExecuteChanged();
        LoadPreviousSimpleMaterialPageCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCollectionStateChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasTopics));
        OnPropertyChanged(nameof(HasMaterials));
        OnPropertyChanged(nameof(LoadedMaterialsCount));
        OnPropertyChanged(nameof(TotalMaterialsCount));
        OnPropertyChanged(nameof(MaterialsShownCountText));
        OnPropertyChanged(nameof(HasSimpleMaterialSource));
        OnPropertyChanged(nameof(SimpleMaterialsShownCountText));
        OnPropertyChanged(nameof(SimpleMaterialsHasMore));
        OnPropertyChanged(nameof(SimpleMaterialsHasPrevious));
        LoadNextSimpleMaterialPageCommand.NotifyCanExecuteChanged();
        LoadPreviousSimpleMaterialPageCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedPath));
    }
}
