using System.Text.Json.Serialization;
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
using Mnemora.Desktop.Navigation;
using Mnemora.Desktop.Notifications;
using Mnemora.Desktop.Settings;
using Mnemora.Desktop.ViewModels.Common;
using Mnemora.Desktop.ViewModels.Sections;
using Mnemora.Desktop.ViewModels.Folders;

namespace Mnemora.Desktop.ViewModels.Library;

#pragma warning disable CA1707
public enum LibraryManagementSimplePage
{
    [JsonStringEnumMemberName("Sections")]
    SECTIONS,
    [JsonStringEnumMemberName("SectionStructure")]
    SECTION_STRUCTURE,
    [JsonStringEnumMemberName("Folders")]
    FOLDERS,
    [JsonStringEnumMemberName("Materials")]
    MATERIALS,
}
#pragma warning restore CA1707

#pragma warning disable CA1707
public enum LibraryManagementFolderSort
{
    [JsonStringEnumMemberName("Custom")]
    CUSTOM,
    [JsonStringEnumMemberName("RecentActivity")]
    RECENT_ACTIVITY,
    [JsonStringEnumMemberName("Name")]
    NAME,
    [JsonStringEnumMemberName("Newest")]
    NEWEST,
}
#pragma warning restore CA1707

public sealed record LibraryManagementFolderSortOption(
    string Name,
    LibraryManagementFolderSort Sort);

public enum LibraryManagementMaterialFilter
{
    [JsonStringEnumMemberName("All")]
    ALL,
    [JsonStringEnumMemberName("Articles")]
    ARTICLES,
    [JsonStringEnumMemberName("Questions")]
    QUESTIONS,
}

#pragma warning disable CA1707
public enum LibraryManagementMaterialSort
{
    [JsonStringEnumMemberName("Custom")]
    CUSTOM,
    [JsonStringEnumMemberName("RecentActivity")]
    RECENT_ACTIVITY,
    [JsonStringEnumMemberName("Name")]
    NAME,
    [JsonStringEnumMemberName("Newest")]
    NEWEST,
}
#pragma warning restore CA1707

public sealed record LibraryManagementMaterialSortOption(
    string Name,
    LibraryManagementMaterialSort Sort);

public sealed partial class LibraryManagementViewModel(
    IQueryDispatcher queryDispatcher,
    ICommandDispatcher commandDispatcher,
    IDialogService dialogService,
    ISettingsService settingsService,
    INotificationService notificationService,
    IMaterialNavigationService materialNavigationService,
    CreateMaterialViewModel createMaterialViewModel,
    ILogger<LibraryManagementViewModel> logger)
    : ViewModelBase
{
    private readonly List<LibrarySectionDto> _library = [];
    private readonly Dictionary<Guid, Guid[]> _pendingFolderOrders = new();
    private readonly Dictionary<Guid, Guid[]> _pendingMaterialOrders = new();

    public CreateMaterialViewModel CreateMaterial { get; } = createMaterialViewModel;

    public LibrarySectionManagementViewModel SectionStructure { get; } =
        new(queryDispatcher, materialNavigationService);

    private const int SIMPLE_SECTION_PAGE_SIZE = LibraryPagingDefaults.PAGE_SIZE;
    private const int SIMPLE_MATERIAL_PAGE_SIZE = LibraryPagingDefaults.PAGE_SIZE;
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(350);

    private Guid[]? _pendingSectionOrder;
    private Guid[]? _orderSnapshot;

    private CancellationToken _viewCancellationToken;
    private int _simpleSectionLoadVersion;
    private int _simpleSectionSearchVersion;
    private int _simpleFolderSearchVersion;
    private int _simpleMaterialSearchVersion;
    private int _simpleMaterialVisibleCount = SIMPLE_MATERIAL_PAGE_SIZE;
    private int _simpleMaterialsFilteredTotalCount;
    private bool _isSimpleSectionsLoaded;
    private bool _isSimpleViewModeLoaded;
    private bool _isSimpleSortSettingsLoaded;
    private bool _isApplyingSimpleSortSettings;
    private bool _isManagementColumnSettingsLoaded;
    private bool _isApplyingManagementColumnSettings;

    // Сортировка папок зависит от раздела, а сортировка материалов — от папки.
    // Поэтому эти настройки хранятся отдельно для каждого родительского контекста.
    private readonly Dictionary<Guid, LibraryManagementSortMode> _folderSortBySection = new();
    private readonly Dictionary<Guid, LibraryManagementSortMode> _materialSortByFolder = new();

    private Task? _loadTask;
    private bool _reloadRequested;
    private bool _isLoaded;
    private bool _suppressSelectionReload;
    private int _contextLoadVersion;
    private Guid? _preferredSectionId;
    private Guid? _preferredFolderId;
    private bool _isCreatingMaterialFromSectionStructure;

    public ObservableCollection<LibraryManagementOrderItemViewModel> Sections { get; } = [];

    public ObservableCollection<LibraryManagementOrderItemViewModel> Folders { get; } = [];

    public ObservableCollection<LibraryManagementOrderItemViewModel> Materials { get; } = [];

    public ObservableCollection<LibraryManagementSectionViewModel> SimpleSections { get; } = [];

    public ObservableCollection<LibraryManagementSectionRowViewModel> SimpleSectionRows { get; } = [];

    public ObservableCollection<LibraryManagementSectionRowViewModel> SimpleCompactSectionRows { get; } = [];

    public ObservableCollection<LibraryManagementOrderItemViewModel> SimpleFolders { get; } = [];

    public ObservableCollection<LibraryManagementOrderItemViewModel> SimpleMaterials { get; } = [];

    public ObservableCollection<LibraryManagementFolderRowViewModel> SimpleFolderRows { get; } = [];

    public ObservableCollection<LibraryManagementFolderRowViewModel> SimpleCompactFolderRows { get; } = [];

    public IReadOnlyList<LibraryManagementSectionSortOption> SimpleSectionSortOptions { get; } =
    [
        new("Мой порядок", LibraryManagementSectionSort.CUSTOM),
        new("Последняя активность", LibraryManagementSectionSort.RECENT_ACTIVITY),
        new("По названию", LibraryManagementSectionSort.NAME),
        new("Сначала новые", LibraryManagementSectionSort.NEWEST),
    ];

    public IReadOnlyList<LibraryManagementFolderSortOption> SimpleFolderSortOptions { get; } =
    [
        new("Мой порядок", LibraryManagementFolderSort.CUSTOM),
        new("Последняя активность", LibraryManagementFolderSort.RECENT_ACTIVITY),
        new("По названию", LibraryManagementFolderSort.NAME),
        new("Сначала новые", LibraryManagementFolderSort.NEWEST),
    ];

    public IReadOnlyList<LibraryManagementMaterialSortOption> SimpleMaterialSortOptions { get; } =
    [
        new("Мой порядок", LibraryManagementMaterialSort.CUSTOM),
        new("Последняя активность", LibraryManagementMaterialSort.RECENT_ACTIVITY),
        new("По названию", LibraryManagementMaterialSort.NAME),
        new("Сначала новые", LibraryManagementMaterialSort.NEWEST),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsSimpleFoldersEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSimpleFolderSearchResults))]
    [NotifyPropertyChangedFor(nameof(IsSimpleMaterialsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSimpleMaterialResults))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyCanExecuteChangedFor(nameof(SaveOrderCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartCreateMaterialCommand))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSimpleFoldersEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSimpleFolderSearchResults))]
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
    [NotifyCanExecuteChangedFor(nameof(AddFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartCreateMaterialCommand))]
    private LibraryManagementOrderItemViewModel? _selectedSection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedFolder))]
    [NotifyPropertyChangedFor(nameof(SelectedPath))]
    [NotifyPropertyChangedFor(nameof(OrderContextName))]
    [NotifyPropertyChangedFor(nameof(HasOrderContext))]
    [NotifyCanExecuteChangedFor(nameof(StartCreateMaterialCommand))]
    private LibraryManagementOrderItemViewModel? _selectedFolder;

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

    private LibraryManagementViewMode _simpleSectionsViewMode = LibraryManagementViewMode.TILES;
    private LibraryManagementViewMode _simpleFoldersViewMode = LibraryManagementViewMode.TILES;
    private LibraryManagementViewMode _simpleMaterialsViewMode = LibraryManagementViewMode.TABLE;

    [ObservableProperty]
    private LibraryManagementSectionSortOption _selectedSimpleSectionSortOption =
        new("Мой порядок", LibraryManagementSectionSort.CUSTOM);

    [ObservableProperty]
    private LibraryManagementFolderSortOption _selectedSimpleFolderSortOption =
        new("Мой порядок", LibraryManagementFolderSort.CUSTOM);

    [ObservableProperty]
    private string? _simpleFolderSearchText;

    [ObservableProperty]
    private LibraryManagementMaterialSortOption _selectedSimpleMaterialSortOption =
        new("Мой порядок", LibraryManagementMaterialSort.CUSTOM);

    [ObservableProperty]
    private string? _simpleMaterialSearchText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSimpleAllMaterialsFilter))]
    [NotifyPropertyChangedFor(nameof(IsSimpleArticlesFilter))]
    [NotifyPropertyChangedFor(nameof(IsSimpleQuestionsFilter))]
    [NotifyPropertyChangedFor(nameof(IsSimpleMaterialsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSimpleMaterialResults))]
    private LibraryManagementMaterialFilter _simpleMaterialFilter = LibraryManagementMaterialFilter.ALL;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private bool _showManagementSectionFoldersColumn = true;

    [ObservableProperty]
    private bool _showManagementSectionMaterialsColumn = true;

    [ObservableProperty]
    private bool _showManagementSectionArticlesColumn = true;

    [ObservableProperty]
    private bool _showManagementSectionQuestionsColumn = true;

    [ObservableProperty]
    private bool _showManagementSectionCreatedColumn = true;

    [ObservableProperty]
    private bool _showManagementSectionUpdatedColumn = true;

    [ObservableProperty]
    private bool _showManagementSectionActivityColumn = true;

    [ObservableProperty]
    private bool _showManagementContentTypeColumn = true;

    [ObservableProperty]
    private bool _showManagementContentLocationColumn = true;

    [ObservableProperty]
    private bool _showManagementContentQuestionsColumn = true;

    [ObservableProperty]
    private bool _showManagementContentDifficultyColumn = true;

    [ObservableProperty]
    private bool _showManagementContentUpdatedColumn = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOrderMode))]
    [NotifyPropertyChangedFor(nameof(IsSimpleMode))]
    [NotifyPropertyChangedFor(nameof(IsSimpleSectionsPage))]
    [NotifyPropertyChangedFor(nameof(IsSectionStructurePage))]
    [NotifyPropertyChangedFor(nameof(IsSimpleFoldersPage))]
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
    [NotifyPropertyChangedFor(nameof(IsSectionStructurePage))]
    [NotifyPropertyChangedFor(nameof(IsSimpleFoldersPage))]
    [NotifyPropertyChangedFor(nameof(IsSimpleMaterialsPage))]
    [NotifyPropertyChangedFor(nameof(IsSimpleTilesView))]
    [NotifyPropertyChangedFor(nameof(IsSimpleCompactTilesView))]
    [NotifyPropertyChangedFor(nameof(IsSimpleTableView))]
    [NotifyCanExecuteChangedFor(nameof(LoadNextSimpleSectionPageCommand))]
    private LibraryManagementSimplePage _simplePage = LibraryManagementSimplePage.SECTIONS;

    public bool IsOrderMode => ActiveOrderTarget is not null;

    public bool IsSimpleMode => !IsOrderMode;

    public bool IsSimpleSectionsPage => IsSimpleMode && SimplePage == LibraryManagementSimplePage.SECTIONS;

    public bool IsSectionStructurePage => IsSimpleMode && SimplePage == LibraryManagementSimplePage.SECTION_STRUCTURE;

    public bool IsSimpleFoldersPage => IsSimpleMode && SimplePage == LibraryManagementSimplePage.FOLDERS;

    public bool IsSimpleMaterialsPage => IsSimpleMode && SimplePage == LibraryManagementSimplePage.MATERIALS;

    public IEnumerable<LibraryManagementOrderItemViewModel> OrderItems =>
        ActiveOrderTarget switch
        {
            LibraryOrderTarget.SECTIONS => Sections,
            LibraryOrderTarget.FOLDERS => Folders,
            LibraryOrderTarget.MATERIALS => Materials,
            _ => Array.Empty<LibraryManagementOrderItemViewModel>(),
        };

    public string OrderTargetTag =>
        ActiveOrderTarget switch
        {
            LibraryOrderTarget.SECTIONS => "Sections",
            LibraryOrderTarget.FOLDERS => "Folders",
            LibraryOrderTarget.MATERIALS => "Materials",
            _ => string.Empty,
        };

    public string OrderTitle =>
        ActiveOrderTarget switch
        {
            LibraryOrderTarget.SECTIONS => "Настроить порядок разделов",
            LibraryOrderTarget.FOLDERS => "Настроить порядок папок",
            LibraryOrderTarget.MATERIALS => "Настроить порядок материалов",
            _ => "Настроить порядок",
        };

    public string OrderDescription =>
        ActiveOrderTarget switch
        {
            LibraryOrderTarget.SECTIONS => "Перетащите разделы в нужном порядке.",
            LibraryOrderTarget.FOLDERS => "Перетащите папки внутри выбранного раздела.",
            LibraryOrderTarget.MATERIALS => "Перетащите материалы внутри выбранной папки.",
            _ => string.Empty,
        };

    public string OrderContextName =>
        ActiveOrderTarget switch
        {
            LibraryOrderTarget.FOLDERS => SelectedSection?.Name ?? string.Empty,
            LibraryOrderTarget.MATERIALS => SelectedFolder?.Name ?? string.Empty,
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
            LibraryManagementSimplePage.SECTIONS => _simpleSectionsViewMode,
            LibraryManagementSimplePage.FOLDERS => _simpleFoldersViewMode,
            LibraryManagementSimplePage.MATERIALS => _simpleMaterialsViewMode,
            _ => LibraryManagementViewMode.TILES,
        };

    public bool IsSimpleTilesView => CurrentSimpleViewMode == LibraryManagementViewMode.TILES;

    public bool IsSimpleCompactTilesView => CurrentSimpleViewMode == LibraryManagementViewMode.COMPACT_TILES;

    public bool IsSimpleTableView => CurrentSimpleViewMode == LibraryManagementViewMode.TABLE;

    public string SimpleSectionsShownCountText
    {
        get
        {
            int visibleCount = Math.Min(
                SIMPLE_SECTION_PAGE_SIZE,
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

    public bool HasSimpleFolderSource => _simpleFolderSourceTotalCount > 0;

    public bool HasSimpleFolders => SimpleFolders.Count > 0;

    public bool IsSimpleFoldersEmpty =>
        !IsContextLoading &&
        !HasError &&
        !HasSimpleFolderSource &&
        string.IsNullOrWhiteSpace(SimpleFolderSearchText);

    public bool HasNoSimpleFolderSearchResults =>
        !IsContextLoading &&
        !HasError &&
        HasSimpleFolderSource &&
        !HasSimpleFolders &&
        !string.IsNullOrWhiteSpace(SimpleFolderSearchText);

    public string SimpleFoldersShownCountText
    {
        get
        {
            int visibleCount = Math.Min(
                SIMPLE_FOLDER_PAGE_SIZE,
                Math.Max(0, _simpleFoldersTotalCount - SimpleFolderCurrentPageOffset));

            return LibraryRangeTextFormatter.FormatEntity(
                "Папки",
                "Папки не найдены",
                SimpleFolderCurrentPageOffset,
                visibleCount,
                _simpleFoldersTotalCount,
                !string.IsNullOrWhiteSpace(SimpleFolderSearchText));
        }
    }

    public bool HasSimpleMaterialSource =>
        _simpleMaterialSourceTotalCount > 0;

    public bool HasSimpleMaterials => SimpleMaterials.Count > 0;

    public bool SimpleMaterialsHasMore =>
        _simpleMaterialWindowEndOffset < _simpleMaterialsFilteredTotalCount &&
        !_isSimpleMaterialsLoadingNextPage;

    public bool IsSimpleAllMaterialsFilter => SimpleMaterialFilter == LibraryManagementMaterialFilter.ALL;

    public bool IsSimpleArticlesFilter => SimpleMaterialFilter == LibraryManagementMaterialFilter.ARTICLES;

    public bool IsSimpleQuestionsFilter => SimpleMaterialFilter == LibraryManagementMaterialFilter.QUESTIONS;

    public bool IsSimpleMaterialsEmpty =>
        !IsContextLoading &&
        !HasError &&
        !HasSimpleMaterialSource &&
        string.IsNullOrWhiteSpace(SimpleMaterialSearchText) &&
        SimpleMaterialFilter == LibraryManagementMaterialFilter.ALL;

    public bool HasNoSimpleMaterialResults =>
        !IsContextLoading &&
        !HasError &&
        HasSimpleMaterialSource &&
        !HasSimpleMaterials &&
        (!string.IsNullOrWhiteSpace(SimpleMaterialSearchText) ||
         SimpleMaterialFilter != LibraryManagementMaterialFilter.ALL);

    public string SimpleMaterialsShownCountText =>
        FormatSimpleMaterialRangeText();

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsEmpty => !IsLoading && !HasError && Sections.Count == 0;

    public bool HasSelectedSection => SelectedSection is not null;

    public bool HasSelectedFolder => SelectedFolder is not null;

    public bool HasFolders => Folders.Count > 0;

    public bool HasMaterials => Materials.Count > 0;

    public int LoadedMaterialsCount => Materials.Count;

    public int TotalMaterialsCount =>
        SelectedFolder is null
            ? 0
            : GetFolderMaterials(FindFolder(SelectedFolder.Id)).Count;

    public string MaterialsShownCountText =>
        $"Показано {LoadedMaterialsCount} из {TotalMaterialsCount}";

    public bool HasUnsavedOrder =>
        _pendingSectionOrder is not null ||
        _pendingFolderOrders.Count > 0 ||
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

            return SelectedFolder is null
                ? SelectedSection.Name
                : $"{SelectedSection.Name} / {SelectedFolder.Name}";
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _viewCancellationToken = cancellationToken;

        await EnsureSimpleViewModeLoadedAsync(cancellationToken);
        await EnsureSimpleSortSettingsLoadedAsync(cancellationToken);
        await EnsureManagementColumnSettingsLoadedAsync(cancellationToken);

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

    public void MoveFolder(LibraryManagementOrderItemViewModel item, int targetIndex)
    {
        if (SelectedSection is null || !MoveItem(Folders, item, targetIndex))
        {
            return;
        }

        _pendingFolderOrders[SelectedSection.Id] = Folders.Select(folder => folder.Id).ToArray();
        NotifyOrderChanged();
    }

    public void MoveMaterial(LibraryManagementOrderItemViewModel item, int targetIndex)
    {
        if (SelectedFolder is null || !MoveItem(Materials, item, targetIndex))
        {
            return;
        }

        _pendingMaterialOrders[SelectedFolder.Id] = Materials.Select(material => material.Id).ToArray();
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
            case LibraryOrderTarget.SECTIONS:
                break;

            case LibraryOrderTarget.FOLDERS:
                if (SelectedSection is null)
                {
                    return Array.Empty<LibraryManagementOrderItemViewModel>();
                }

                await LoadFoldersAsync(SelectedFolder?.Id, cancellationToken);
                break;

            case LibraryOrderTarget.MATERIALS:
                if (SelectedFolder is null)
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

        return target == LibraryOrderTarget.MATERIALS
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
            LibraryOrderTarget.SECTIONS => null,
            LibraryOrderTarget.FOLDERS => SelectedSection?.Id,
            LibraryOrderTarget.MATERIALS => SelectedFolder?.Id,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
        };

        if (target != LibraryOrderTarget.SECTIONS && parentId is null)
        {
            return false;
        }

        IsSavingOrder = true;
        ErrorMessage = null;

        try
        {
            IReadOnlyList<Guid> effectiveOrderedIds =
                target == LibraryOrderTarget.MATERIALS
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
                case LibraryOrderTarget.SECTIONS
                    when SelectedSimpleSectionSortOption.Sort == LibraryManagementSectionSort.CUSTOM:
                    await ReloadSimpleSectionsCoreAsync(cancellationToken);
                    break;

                case LibraryOrderTarget.FOLDERS:
                    if (IsSimpleFoldersPage)
                    {
                        await ReloadSimpleFoldersPagedAsync(cancellationToken);
                    }
                    break;

                case LibraryOrderTarget.MATERIALS:
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

        BeginOrder(LibraryOrderTarget.SECTIONS);
    }

    [RelayCommand]
    private async Task ConfigureFoldersOrderAsync(CancellationToken cancellationToken)
    {
        if (SelectedSection is null)
        {
            return;
        }

        await EnsureOrderLibraryLoadedAsync(cancellationToken);
        await LoadFoldersAsync(selectedFolderId: null, cancellationToken: cancellationToken);

        if (Folders.Count == 0)
        {
            return;
        }

        BeginOrder(LibraryOrderTarget.FOLDERS);
    }

    [RelayCommand]
    private async Task ConfigureMaterialsOrderAsync(CancellationToken cancellationToken)
    {
        if (SelectedFolder is null)
        {
            return;
        }

        await EnsureOrderLibraryLoadedAsync(cancellationToken);
        await LoadMaterialsAsync(cancellationToken);

        if (Materials.Count == 0)
        {
            return;
        }

        BeginOrder(LibraryOrderTarget.MATERIALS);
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

        if (IsSimpleFoldersPage && SelectedSection is not null)
        {
            _ = ReloadSimpleFoldersPagedAsync(_viewCancellationToken);
        }
        else if (IsSimpleMaterialsPage && SelectedFolder is not null)
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

        SelectedFolder = null;
        SelectedSection = new LibraryManagementOrderItemViewModel(item.Source, position: 1);

        Task initializationTask = SectionStructure.InitializeAsync(item.Source, cancellationToken);
        SimplePage = LibraryManagementSimplePage.SECTION_STRUCTURE;

        await initializationTask;
    }

    [RelayCommand]
    private void StartCreateSectionStructureMaterial()
    {
        LibrarySectionManagementTreeNodeViewModel? target =
            SectionStructure.GetCreationTarget(forFolder: false);
        LibrarySectionOverviewDto? section = SectionStructure.Section;

        if (target?.ContainerId is not Guid containerId || section is null)
        {
            return;
        }

        var context = new LibraryManagementOrderItemViewModel(
            new LibraryManagementFolderOverviewDto(
                containerId,
                section.Id,
                target.Name,
                target.Color ?? section.Color,
                target.Icon ?? "FolderOutline",
                section.CreatedAt,
                section.UpdatedAt,
                section.LastActivityAt,
                0,
                0,
                0,
                0),
            position: 1);

        _isCreatingMaterialFromSectionStructure = true;
        CreateMaterial.Initialize(
            context,
            CancelCreateMaterial,
            CompleteCreateMaterial);
        IsCreatingMaterial = true;
    }

    [RelayCommand]
    private async Task CreateSectionStructureFolderAsync(
        CancellationToken cancellationToken)
    {
        LibrarySectionManagementTreeNodeViewModel? parent =
            SectionStructure.GetCreationTarget(forFolder: true);
        LibrarySectionOverviewDto? section = SectionStructure.Section;

        if (parent?.ContainerId is not Guid parentContainerId || section is null)
        {
            return;
        }

        Guid? folderId = dialogService.Show<CreateLibraryFolderDialogViewModel, Guid?>(
            viewModel => viewModel.Initialize(
                parentContainerId,
                parent.Name,
                parent.Color ?? section.Color));

        if (folderId is null)
        {
            return;
        }

        notificationService.ShowSuccess("Папка создана");
        await SectionStructure.ReloadChildrenAndSelectAsync(parent, folderId, cancellationToken);
        await SectionStructure.RefreshSelectedMaterialsAsync(cancellationToken);
        await ReloadSimpleSectionsCoreAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task OpenSimpleFolderAsync(
        LibraryManagementOrderItemViewModel? item,
        CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        SimpleMaterialSearchText = null;
        SimpleMaterialFilter = LibraryManagementMaterialFilter.ALL;
        SelectedFolder = item;
        SimplePage = LibraryManagementSimplePage.MATERIALS;

        await ReloadSimpleMaterialsPagedAsync(cancellationToken);
    }

    [RelayCommand]
    private void BackSimple()
    {
        switch (SimplePage)
        {
            case LibraryManagementSimplePage.MATERIALS:
                SimplePage = LibraryManagementSimplePage.FOLDERS;
                break;
            case LibraryManagementSimplePage.FOLDERS:
            case LibraryManagementSimplePage.SECTION_STRUCTURE:
                SelectedFolder = null;
                SimplePage = LibraryManagementSimplePage.SECTIONS;
                break;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartCreateMaterial))]
    private void StartCreateMaterial()
    {
        if (!CanStartCreateMaterial() ||
            SelectedFolder is null)
        {
            return;
        }

        CreateMaterial.Initialize(
            SelectedFolder,
            CancelCreateMaterial,
            CompleteCreateMaterial);

        IsCreatingMaterial = true;
    }

    private bool CanStartCreateMaterial() =>
        SelectedSection is not null &&
        SelectedFolder is not null &&
        !IsLoading &&
        !IsContextLoading;

    [RelayCommand]
    private void CancelCreateMaterial()
    {
        _isCreatingMaterialFromSectionStructure = false;
        IsCreatingMaterial = false;
        CreateMaterial.Reset();
    }

    private async void CompleteCreateMaterial()
    {
        string message =
            CreateMaterial.IsQuestionMaterial
                ? "Вопрос создан"
                : "Статья создана";

        bool refreshSectionStructure = _isCreatingMaterialFromSectionStructure;

        if (!refreshSectionStructure)
        {
            _preferredSectionId = SelectedSection?.Id;
            _preferredFolderId = SelectedFolder?.Id;
        }

        _isCreatingMaterialFromSectionStructure = false;
        IsCreatingMaterial = false;
        CreateMaterial.Reset();

        notificationService.ShowSuccess(message);

        try
        {
            if (refreshSectionStructure)
            {
                await SectionStructure.RefreshSelectedMaterialsAsync(CancellationToken.None);
                await ReloadSimpleSectionsCoreAsync(CancellationToken.None);
                return;
            }

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
        SimpleMaterialFilter = LibraryManagementMaterialFilter.ALL;
    }

    [RelayCommand]
    private void SelectSimpleArticles()
    {
        SimpleMaterialFilter = LibraryManagementMaterialFilter.ARTICLES;
    }

    [RelayCommand]
    private void SelectSimpleQuestions()
    {
        SimpleMaterialFilter = LibraryManagementMaterialFilter.QUESTIONS;
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
        _preferredFolderId = null;
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

        _pendingFolderOrders.Remove(section.Id);
        _preferredSectionId = null;
        _preferredFolderId = null;
        notificationService.ShowSuccess($"Раздел «{section.Name}» удалён");
        await RefreshAfterMutationAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanAddFolder))]
    private async Task AddFolderAsync(
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

        var folderId = dialogService.Show<CreateFolderDialogViewModel, Guid?>(
            viewModel => viewModel.Initialize(section.Id, section.Name));

        if (folderId is null)
        {
            return;
        }

        _preferredSectionId = section.Id;
        _preferredFolderId = folderId.Value;
        notificationService.ShowSuccess("Папка создана");
        await RefreshAfterMutationAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task EditFolderAsync(
        LibraryManagementOrderItemViewModel? item,
        CancellationToken cancellationToken)
    {
        LibraryFolderSnapshotDto? folder = item?.Folder ?? FindFolder(item?.Id);

        if (folder is null && item is not null)
        {
            await EnsureOrderLibraryLoadedAsync(cancellationToken);
            folder = FindFolder(item.Id);
        }

        if (folder is null)
        {
            return;
        }

        var folderId = dialogService.Show<EditFolderDialogViewModel, Guid?>(
            viewModel => viewModel.Initialize(folder));

        if (folderId is null)
        {
            return;
        }

        _preferredFolderId = folder.Id;
        notificationService.ShowSuccess("Изменения папки сохранены");
        await RefreshAfterMutationAsync(cancellationToken);
    }

    [RelayCommand]
    private async Task DeleteFolderAsync(
        LibraryManagementOrderItemViewModel? item,
        CancellationToken cancellationToken)
    {
        LibraryFolderSnapshotDto? folder = item?.Folder ?? FindFolder(item?.Id);

        if (folder is null && item is not null)
        {
            await EnsureOrderLibraryLoadedAsync(cancellationToken);
            folder = FindFolder(item.Id);
        }

        if (folder is null)
        {
            return;
        }

        bool wasDeleted = dialogService.Show<DeleteFolderDialogViewModel, bool>(
            viewModel => viewModel.Initialize(folder));

        if (!wasDeleted)
        {
            return;
        }

        _pendingMaterialOrders.Remove(folder.Id);
        _preferredFolderId = null;
        notificationService.ShowSuccess($"Папка «{folder.Name}» удалена");
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
                        LibraryOrderTarget.SECTIONS,
                        parentId: null,
                        _pendingSectionOrder,
                        cancellationToken))
                {
                    return;
                }

                _pendingSectionOrder = null;
            }

            foreach (var pendingOrder in _pendingFolderOrders.ToArray())
            {
                if (!await SaveOrderCoreAsync(
                        LibraryOrderTarget.FOLDERS,
                        pendingOrder.Key,
                        pendingOrder.Value,
                        cancellationToken))
                {
                    return;
                }

                _pendingFolderOrders.Remove(pendingOrder.Key);
            }

            foreach (var pendingOrder in _pendingMaterialOrders.ToArray())
            {
                if (!await SaveOrderCoreAsync(
                        LibraryOrderTarget.MATERIALS,
                        pendingOrder.Key,
                        pendingOrder.Value,
                        cancellationToken))
                {
                    return;
                }

                _pendingMaterialOrders.Remove(pendingOrder.Key);
            }

            notificationService.ShowSuccess("Порядок сохранён");

            if (savedTarget == LibraryOrderTarget.SECTIONS &&
                SelectedSimpleSectionSortOption.Sort == LibraryManagementSectionSort.CUSTOM)
            {
                await ReloadSimpleSectionsCoreAsync(cancellationToken);
            }
            else if (savedTarget == LibraryOrderTarget.FOLDERS && IsSimpleFoldersPage)
            {
                await ReloadSimpleFoldersPagedAsync(cancellationToken);
            }
            else if (savedTarget == LibraryOrderTarget.MATERIALS && IsSimpleMaterialsPage)
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
            _preferredFolderId = SelectedFolder?.Id;
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
        return SetSimpleViewModeAsync(LibraryManagementViewMode.TILES, cancellationToken);
    }

    [RelayCommand]
    private Task ShowSimpleCompactTilesViewAsync(CancellationToken cancellationToken)
    {
        return SetSimpleViewModeAsync(LibraryManagementViewMode.COMPACT_TILES, cancellationToken);
    }

    [RelayCommand]
    private Task ShowSimpleTableViewAsync(CancellationToken cancellationToken)
    {
        return SetSimpleViewModeAsync(LibraryManagementViewMode.TABLE, cancellationToken);
    }

    [RelayCommand]
    private async Task AddSimpleFolderAsync(
        LibraryManagementSectionViewModel? item,
        CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        var folderId = dialogService.Show<CreateFolderDialogViewModel, Guid?>(
            viewModel => viewModel.Initialize(item.Id, item.Name));

        if (folderId is null)
        {
            return;
        }

        _preferredSectionId = item.Id;
        _preferredFolderId = folderId.Value;
        notificationService.ShowSuccess("Папка создана");
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

    partial void OnShowManagementSectionFoldersColumnChanged(bool value) => PersistManagementSectionColumns();
    partial void OnShowManagementSectionMaterialsColumnChanged(bool value) => PersistManagementSectionColumns();
    partial void OnShowManagementSectionArticlesColumnChanged(bool value) => PersistManagementSectionColumns();
    partial void OnShowManagementSectionQuestionsColumnChanged(bool value) => PersistManagementSectionColumns();
    partial void OnShowManagementSectionCreatedColumnChanged(bool value) => PersistManagementSectionColumns();
    partial void OnShowManagementSectionUpdatedColumnChanged(bool value) => PersistManagementSectionColumns();
    partial void OnShowManagementSectionActivityColumnChanged(bool value) => PersistManagementSectionColumns();

    partial void OnShowManagementContentTypeColumnChanged(bool value) => PersistManagementContentColumns();
    partial void OnShowManagementContentLocationColumnChanged(bool value) => PersistManagementContentColumns();
    partial void OnShowManagementContentQuestionsColumnChanged(bool value) => PersistManagementContentColumns();
    partial void OnShowManagementContentDifficultyColumnChanged(bool value) => PersistManagementContentColumns();
    partial void OnShowManagementContentUpdatedColumnChanged(bool value) => PersistManagementContentColumns();

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

    partial void OnSimpleFolderSearchTextChanged(string? value)
    {
        int searchVersion = Interlocked.Increment(ref _simpleFolderSearchVersion);
        _ = ApplySimpleFolderFilterAfterDelayAsync(searchVersion);
    }

    partial void OnSelectedSimpleFolderSortOptionChanged(LibraryManagementFolderSortOption value)
    {
        if (_isApplyingSimpleSortSettings)
        {
            return;
        }

        if (SelectedSection is { } section)
        {
            LibraryManagementSortMode sortMode = ToSettingsSort(value.Sort);
            _folderSortBySection[section.Id] = sortMode;
            _ = SaveSimpleFolderSortAsync(section.Id, value.Sort);
        }

        if (IsSimpleFoldersPage && SelectedSection is not null)
        {
            _ = ReloadSimpleFoldersPagedAsync(_viewCancellationToken);
        }
        else
        {
            ApplySimpleFolderFilterAndSort();
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

        if (SelectedFolder is { } folder)
        {
            LibraryManagementSortMode sortMode = ToSettingsSort(value.Sort);
            _materialSortByFolder[folder.Id] = sortMode;
            _ = SaveSimpleMaterialSortAsync(folder.Id, value.Sort);
        }

        if (IsSimpleMaterialsPage && SelectedFolder is not null)
        {
            _ = ReloadSimpleMaterialsPagedAsync(_viewCancellationToken);
        }
    }

    partial void OnSimpleMaterialFilterChanged(LibraryManagementMaterialFilter value)
    {
        if (IsSimpleMaterialsPage && SelectedFolder is not null)
        {
            _ = ReloadSimpleMaterialsPagedAsync(_viewCancellationToken);
        }
    }

    partial void OnSelectedSectionChanged(LibraryManagementOrderItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedPath));
        ApplyFolderSortForSection(value?.Id);

        if (!_isLoaded || _suppressSelectionReload || !IsOrderMode)
        {
            return;
        }

        _ = ChangeSectionContextSafelyAsync(value);
    }

    partial void OnSelectedFolderChanged(LibraryManagementOrderItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedPath));
        ApplyMaterialSortForFolder(value?.Id);

        if (!_isLoaded || _suppressSelectionReload || !IsOrderMode)
        {
            return;
        }

        _ = ChangeFolderContextSafelyAsync(value);
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
               !_isSimpleSectionsLoadingPreviousPage &&
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
            _simpleSectionWindow.ShowPage(0, page.Items, PageWindowInsert.APPEND);
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

    private async Task EnsureManagementColumnSettingsLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isManagementColumnSettingsLoaded)
        {
            return;
        }

        try
        {
            AppSettings settings = await settingsService.LoadAsync(cancellationToken);
            HashSet<LibraryManagementSectionColumn> sectionColumns =
                settings.LibraryManagementSectionColumns ?? [];
            HashSet<LibraryManagementContentColumn> contentColumns =
                settings.LibraryManagementContentColumns ?? [];

            _isApplyingManagementColumnSettings = true;

            ShowManagementSectionFoldersColumn = sectionColumns.Contains(LibraryManagementSectionColumn.FOLDERS);
            ShowManagementSectionMaterialsColumn = sectionColumns.Contains(LibraryManagementSectionColumn.MATERIALS);
            ShowManagementSectionArticlesColumn = sectionColumns.Contains(LibraryManagementSectionColumn.ARTICLES);
            ShowManagementSectionQuestionsColumn = sectionColumns.Contains(LibraryManagementSectionColumn.QUESTIONS);
            ShowManagementSectionCreatedColumn = sectionColumns.Contains(LibraryManagementSectionColumn.CREATED);
            ShowManagementSectionUpdatedColumn = sectionColumns.Contains(LibraryManagementSectionColumn.UPDATED);
            ShowManagementSectionActivityColumn = sectionColumns.Contains(LibraryManagementSectionColumn.ACTIVITY);

            ShowManagementContentTypeColumn = contentColumns.Contains(LibraryManagementContentColumn.TYPE);
            ShowManagementContentLocationColumn = contentColumns.Contains(LibraryManagementContentColumn.LOCATION);
            ShowManagementContentQuestionsColumn = contentColumns.Contains(LibraryManagementContentColumn.QUESTIONS);
            ShowManagementContentDifficultyColumn = contentColumns.Contains(LibraryManagementContentColumn.DIFFICULTY);
            ShowManagementContentUpdatedColumn = contentColumns.Contains(LibraryManagementContentColumn.UPDATED);

            _isManagementColumnSettingsLoaded = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось загрузить настройки колонок управления библиотекой");
            _isManagementColumnSettingsLoaded = true;
        }
        finally
        {
            _isApplyingManagementColumnSettings = false;
        }
    }

    private void PersistManagementSectionColumns()
    {
        if (!_isManagementColumnSettingsLoaded || _isApplyingManagementColumnSettings)
        {
            return;
        }

        _ = SaveManagementSectionColumnsAsync();
    }

    private async Task SaveManagementSectionColumnsAsync()
    {
        try
        {
            await settingsService.SaveLibraryManagementSectionColumnsAsync(
                BuildManagementSectionColumns(),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось сохранить настройки колонок разделов");
        }
    }

    private IReadOnlyCollection<LibraryManagementSectionColumn> BuildManagementSectionColumns()
    {
        var columns = new List<LibraryManagementSectionColumn>(7);

        if (ShowManagementSectionFoldersColumn) columns.Add(LibraryManagementSectionColumn.FOLDERS);
        if (ShowManagementSectionMaterialsColumn) columns.Add(LibraryManagementSectionColumn.MATERIALS);
        if (ShowManagementSectionArticlesColumn) columns.Add(LibraryManagementSectionColumn.ARTICLES);
        if (ShowManagementSectionQuestionsColumn) columns.Add(LibraryManagementSectionColumn.QUESTIONS);
        if (ShowManagementSectionCreatedColumn) columns.Add(LibraryManagementSectionColumn.CREATED);
        if (ShowManagementSectionUpdatedColumn) columns.Add(LibraryManagementSectionColumn.UPDATED);
        if (ShowManagementSectionActivityColumn) columns.Add(LibraryManagementSectionColumn.ACTIVITY);

        return columns;
    }

    private void PersistManagementContentColumns()
    {
        if (!_isManagementColumnSettingsLoaded || _isApplyingManagementColumnSettings)
        {
            return;
        }

        _ = SaveManagementContentColumnsAsync();
    }

    private async Task SaveManagementContentColumnsAsync()
    {
        try
        {
            await settingsService.SaveLibraryManagementContentColumnsAsync(
                BuildManagementContentColumns(),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось сохранить настройки колонок содержимого раздела");
        }
    }

    private IReadOnlyCollection<LibraryManagementContentColumn> BuildManagementContentColumns()
    {
        var columns = new List<LibraryManagementContentColumn>(5);

        if (ShowManagementContentTypeColumn) columns.Add(LibraryManagementContentColumn.TYPE);
        if (ShowManagementContentLocationColumn) columns.Add(LibraryManagementContentColumn.LOCATION);
        if (ShowManagementContentQuestionsColumn) columns.Add(LibraryManagementContentColumn.QUESTIONS);
        if (ShowManagementContentDifficultyColumn) columns.Add(LibraryManagementContentColumn.DIFFICULTY);
        if (ShowManagementContentUpdatedColumn) columns.Add(LibraryManagementContentColumn.UPDATED);

        return columns;
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
            _simpleFoldersViewMode = settings.LibraryManagementFoldersViewMode;
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
            _simpleSectionsViewMode = LibraryManagementViewMode.TILES;
            _simpleFoldersViewMode = LibraryManagementViewMode.TILES;
            _simpleMaterialsViewMode = LibraryManagementViewMode.TABLE;
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

            _folderSortBySection.Clear();
            foreach (var pair in settings.LibraryManagementFolderSortBySection)
            {
                _folderSortBySection[pair.Key] = pair.Value;
            }

            _materialSortByFolder.Clear();
            foreach (var pair in settings.LibraryManagementMaterialSortByFolder)
            {
                _materialSortByFolder[pair.Key] = pair.Value;
            }

            _isApplyingSimpleSortSettings = true;

            SelectedSimpleSectionSortOption = SimpleSectionSortOptions.First(option =>
                option.Sort == ToSectionSort(settings.LibraryManagementSectionSort));

            // До выбора конкретного раздела/папки дочерние сортировки остаются
            // в значении по умолчанию. При смене контекста они восстанавливаются
            // из словарей выше.
            SelectedSimpleFolderSortOption = SimpleFolderSortOptions.First(option =>
                option.Sort == LibraryManagementFolderSort.CUSTOM);

            SelectedSimpleMaterialSortOption = SimpleMaterialSortOptions.First(option =>
                option.Sort == LibraryManagementMaterialSort.CUSTOM);

            _isSimpleSortSettingsLoaded = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось загрузить настройки сортировки управления библиотекой");

            _folderSortBySection.Clear();
            _materialSortByFolder.Clear();

            _isApplyingSimpleSortSettings = true;
            SelectedSimpleSectionSortOption = SimpleSectionSortOptions.First(option =>
                option.Sort == LibraryManagementSectionSort.CUSTOM);
            SelectedSimpleFolderSortOption = SimpleFolderSortOptions.First(option =>
                option.Sort == LibraryManagementFolderSort.CUSTOM);
            SelectedSimpleMaterialSortOption = SimpleMaterialSortOptions.First(option =>
                option.Sort == LibraryManagementMaterialSort.CUSTOM);

            _isSimpleSortSettingsLoaded = true;
        }
        finally
        {
            _isApplyingSimpleSortSettings = false;
        }
    }

    private void ApplyFolderSortForSection(Guid? sectionId)
    {
        if (!_isSimpleSortSettingsLoaded || sectionId is null)
        {
            return;
        }

        LibraryManagementSortMode sortMode =
            _folderSortBySection.TryGetValue(sectionId.Value, out LibraryManagementSortMode savedSort)
                ? savedSort
                : LibraryManagementSortMode.CUSTOM;

        bool wasApplying = _isApplyingSimpleSortSettings;
        _isApplyingSimpleSortSettings = true;

        try
        {
            SelectedSimpleFolderSortOption = SimpleFolderSortOptions.First(option =>
                option.Sort == ToFolderSort(sortMode));
        }
        finally
        {
            _isApplyingSimpleSortSettings = wasApplying;
        }
    }

    private void ApplyMaterialSortForFolder(Guid? folderId)
    {
        if (!_isSimpleSortSettingsLoaded || folderId is null)
        {
            return;
        }

        LibraryManagementSortMode sortMode =
            _materialSortByFolder.TryGetValue(folderId.Value, out LibraryManagementSortMode savedSort)
                ? savedSort
                : LibraryManagementSortMode.CUSTOM;

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

    private async Task SaveSimpleFolderSortAsync(
        Guid sectionId,
        LibraryManagementFolderSort sort)
    {
        try
        {
            await settingsService.SaveLibraryManagementFolderSortAsync(
                sectionId,
                ToSettingsSort(sort),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Не удалось сохранить сортировку папок раздела {SectionId}",
                sectionId);
        }
    }

    private async Task SaveSimpleMaterialSortAsync(
        Guid folderId,
        LibraryManagementMaterialSort sort)
    {
        try
        {
            await settingsService.SaveLibraryManagementMaterialSortAsync(
                folderId,
                ToSettingsSort(sort),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Не удалось сохранить сортировку материалов папки {FolderId}",
                folderId);
        }
    }

    private static LibraryManagementSectionSort ToSectionSort(LibraryManagementSortMode sort) =>
        sort switch
        {
            LibraryManagementSortMode.CUSTOM => LibraryManagementSectionSort.CUSTOM,
            LibraryManagementSortMode.RECENT_ACTIVITY => LibraryManagementSectionSort.RECENT_ACTIVITY,
            LibraryManagementSortMode.NAME => LibraryManagementSectionSort.NAME,
            LibraryManagementSortMode.NEWEST => LibraryManagementSectionSort.NEWEST,
            _ => LibraryManagementSectionSort.CUSTOM,
        };

    private static LibraryManagementFolderSort ToFolderSort(LibraryManagementSortMode sort) =>
        sort switch
        {
            LibraryManagementSortMode.CUSTOM => LibraryManagementFolderSort.CUSTOM,
            LibraryManagementSortMode.RECENT_ACTIVITY => LibraryManagementFolderSort.RECENT_ACTIVITY,
            LibraryManagementSortMode.NAME => LibraryManagementFolderSort.NAME,
            LibraryManagementSortMode.NEWEST => LibraryManagementFolderSort.NEWEST,
            _ => LibraryManagementFolderSort.CUSTOM,
        };

    private static LibraryManagementMaterialSort ToMaterialSort(LibraryManagementSortMode sort) =>
        sort switch
        {
            LibraryManagementSortMode.CUSTOM => LibraryManagementMaterialSort.CUSTOM,
            LibraryManagementSortMode.RECENT_ACTIVITY => LibraryManagementMaterialSort.RECENT_ACTIVITY,
            LibraryManagementSortMode.NAME => LibraryManagementMaterialSort.NAME,
            LibraryManagementSortMode.NEWEST => LibraryManagementMaterialSort.NEWEST,
            _ => LibraryManagementMaterialSort.CUSTOM,
        };

    private static LibraryManagementSortMode ToSettingsSort(LibraryManagementSectionSort sort) =>
        sort switch
        {
            LibraryManagementSectionSort.CUSTOM => LibraryManagementSortMode.CUSTOM,
            LibraryManagementSectionSort.RECENT_ACTIVITY => LibraryManagementSortMode.RECENT_ACTIVITY,
            LibraryManagementSectionSort.NAME => LibraryManagementSortMode.NAME,
            LibraryManagementSectionSort.NEWEST => LibraryManagementSortMode.NEWEST,
            _ => LibraryManagementSortMode.CUSTOM,
        };

    private static LibraryManagementSortMode ToSettingsSort(LibraryManagementFolderSort sort) =>
        sort switch
        {
            LibraryManagementFolderSort.CUSTOM => LibraryManagementSortMode.CUSTOM,
            LibraryManagementFolderSort.RECENT_ACTIVITY => LibraryManagementSortMode.RECENT_ACTIVITY,
            LibraryManagementFolderSort.NAME => LibraryManagementSortMode.NAME,
            LibraryManagementFolderSort.NEWEST => LibraryManagementSortMode.NEWEST,
            _ => LibraryManagementSortMode.CUSTOM,
        };

    private static LibraryManagementSortMode ToSettingsSort(LibraryManagementMaterialSort sort) =>
        sort switch
        {
            LibraryManagementMaterialSort.CUSTOM => LibraryManagementSortMode.CUSTOM,
            LibraryManagementMaterialSort.RECENT_ACTIVITY => LibraryManagementSortMode.RECENT_ACTIVITY,
            LibraryManagementMaterialSort.NAME => LibraryManagementSortMode.NAME,
            LibraryManagementMaterialSort.NEWEST => LibraryManagementSortMode.NEWEST,
            _ => LibraryManagementSortMode.CUSTOM,
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
            case LibraryManagementSimplePage.SECTIONS:
                _simpleSectionsViewMode = viewMode;
                break;
            case LibraryManagementSimplePage.FOLDERS:
                _simpleFoldersViewMode = viewMode;
                break;
            case LibraryManagementSimplePage.MATERIALS:
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
                case LibraryManagementSimplePage.SECTIONS:
                    await settingsService.SaveLibraryManagementSectionsViewModeAsync(viewMode, cancellationToken);
                    break;
                case LibraryManagementSimplePage.FOLDERS:
                    await settingsService.SaveLibraryManagementFoldersViewModeAsync(viewMode, cancellationToken);
                    break;
                case LibraryManagementSimplePage.MATERIALS:
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
        Folders.Clear();
        Materials.Clear();

        switch (SimplePage)
        {
            case LibraryManagementSimplePage.FOLDERS when SelectedSection is not null:
                await ReloadSimpleFoldersPagedAsync(cancellationToken);
                break;

            case LibraryManagementSimplePage.MATERIALS when SelectedFolder is not null:
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
        Guid? selectedFolderId = _preferredFolderId ?? SelectedFolder?.Id;

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
            await LoadFoldersAsync(selectedFolderId, cancellationToken);
            await LoadMaterialsAsync(cancellationToken);
        }
        finally
        {
            _isLoaded = true;
            _preferredSectionId = null;
            _preferredFolderId = null;
            NotifyCollectionStateChanged();
        }
    }

    private async Task LoadSectionsAsync(Guid? selectedSectionId, CancellationToken cancellationToken)
    {
        var result = await LoadOrderItemsAsync(
            LibraryOrderTarget.SECTIONS,
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
                LibraryOrderTarget.SECTIONS,
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

    private async Task LoadFoldersAsync(Guid? selectedFolderId, CancellationToken cancellationToken)
    {
        Folders.Clear();
        SimpleFolders.Clear();
        SimpleFolderRows.Clear();
        SimpleCompactFolderRows.Clear();
        NotifySimpleFoldersStateChanged();

        _suppressSelectionReload = true;
        try
        {
            SelectedFolder = null;
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
            LibraryOrderTarget.FOLDERS,
            SelectedSection.Id,
            cancellationToken);

        if (result is null)
        {
            return;
        }

        _pendingFolderOrders.TryGetValue(SelectedSection.Id, out Guid[]? pendingOrder);
        IReadOnlyList<LibraryOrderItemDto> orderedItems = ApplyPendingOrder(result, pendingOrder);

        int position = 1;

        foreach (var orderItem in orderedItems)
        {
            LibraryFolderSnapshotDto? folder = FindFolder(orderItem.Id);

            Folders.Add(new LibraryManagementOrderItemViewModel(
                orderItem,
                LibraryOrderTarget.FOLDERS,
                position++,
                folder: folder));
        }

        if (pendingOrder is not null)
        {
            _pendingFolderOrders[SelectedSection.Id] = Folders.Select(folder => folder.Id).ToArray();
        }

        _suppressSelectionReload = true;
        try
        {
            SelectedFolder = Folders.FirstOrDefault(folder => folder.Id == selectedFolderId);
        }
        finally
        {
            _suppressSelectionReload = false;
        }

        ApplySimpleFolderFilterAndSort();
    }

    private async Task LoadMaterialsAsync(CancellationToken cancellationToken)
    {
        ResetSimpleMaterialPage();
        Materials.Clear();
        SimpleMaterials.Clear();
        SelectedMaterial = null;

        if (SelectedFolder is null)
        {
            NotifySimpleMaterialsStateChanged();
            NotifyCollectionStateChanged();
            return;
        }

        var result = await LoadOrderItemsAsync(
            LibraryOrderTarget.MATERIALS,
            SelectedFolder.Id,
            cancellationToken);

        if (result is null)
        {
            NotifySimpleMaterialsStateChanged();
            return;
        }

        _pendingMaterialOrders.TryGetValue(SelectedFolder.Id, out Guid[]? pendingOrder);
        IReadOnlyList<LibraryOrderItemDto> orderedItems = ApplyPendingOrder(result, pendingOrder);

        IReadOnlyList<LibraryMaterialDto> folderMaterials =
            GetFolderMaterials(
                FindFolder(
                    SelectedFolder.Id));

        var materialsById =
            folderMaterials.ToDictionary(
                material => material.Id);

        var questionCountsByArticleId =
            folderMaterials
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
                    LibraryOrderTarget.MATERIALS,
                    position++,
                    material: material,
                    articleQuestionCount: articleQuestionCount));
        }

        if (pendingOrder is not null)
        {
            _pendingMaterialOrders[SelectedFolder.Id] = Materials.Select(material => material.Id).ToArray();
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
            Guid? preferredFolderId = SelectedFolder?.Id;
            await LoadFoldersAsync(preferredFolderId, CancellationToken.None);

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

    private async Task ChangeFolderContextSafelyAsync(LibraryManagementOrderItemViewModel? folder)
    {
        int loadVersion = Interlocked.Increment(ref _contextLoadVersion);
        IsContextLoading = true;

        try
        {
            await LoadMaterialsAsync(CancellationToken.None);

            if (loadVersion != _contextLoadVersion || SelectedFolder?.Id != folder?.Id)
            {
                return;
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось сменить папку управления библиотекой");
            ErrorMessage = "Не удалось загрузить выбранную папку";
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

    private LibraryFolderSnapshotDto? FindFolder(Guid? id)
    {
        if (id is null)
        {
            return null;
        }

        return _library
            .SelectMany(section => section.Folders)
            .FirstOrDefault(folder => folder.Id == id.Value);
    }

    private static IReadOnlyList<LibraryMaterialDto> GetFolderMaterials(LibraryFolderSnapshotDto? folder) =>
        folder?.Materials ?? [];

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

    private async Task ApplySimpleFolderFilterAfterDelayAsync(int searchVersion)
    {
        try
        {
            await Task.Delay(SearchDelay, _viewCancellationToken);

            if (searchVersion == Volatile.Read(ref _simpleFolderSearchVersion) &&
                IsSimpleFoldersPage &&
                SelectedSection is not null)
            {
                await ReloadSimpleFoldersPagedAsync(_viewCancellationToken);
            }
        }
        catch (OperationCanceledException) when (_viewCancellationToken.IsCancellationRequested)
        {
            // ignore
        }
    }

    private void ApplySimpleFolderFilterAndSort()
    {
        IEnumerable<LibraryManagementOrderItemViewModel> folders = Folders;

        if (!string.IsNullOrWhiteSpace(SimpleFolderSearchText))
        {
            string search = SimpleFolderSearchText.Trim();
            folders = folders.Where(folder =>
                folder.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        folders = SelectedSimpleFolderSortOption.Sort switch
        {
            LibraryManagementFolderSort.CUSTOM => folders
                .OrderBy(folder => folder.Position),

            LibraryManagementFolderSort.RECENT_ACTIVITY => folders
                .OrderByDescending(folder => folder.FolderLastActivityAt)
                .ThenBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase),

            LibraryManagementFolderSort.NAME => folders
                .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase),

            LibraryManagementFolderSort.NEWEST => folders
                .OrderByDescending(folder => folder.FolderCreatedAt)
                .ThenBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase),

            _ => folders.OrderBy(folder => folder.Position),
        };

        SimpleFolders.Clear();
        SimpleFolderRows.Clear();
        SimpleCompactFolderRows.Clear();

        foreach (LibraryManagementOrderItemViewModel folder in folders)
        {
            SimpleFolders.Add(folder);
            AddSimpleFolderToRows(SimpleFolderRows, folder, 3);
            AddSimpleFolderToRows(SimpleCompactFolderRows, folder, 4);
        }

        NotifySimpleFoldersStateChanged();
    }

    private static void AddSimpleFolderToRows(
        ObservableCollection<LibraryManagementFolderRowViewModel> rows,
        LibraryManagementOrderItemViewModel folder,
        int capacity)
    {
        LibraryManagementFolderRowViewModel? row = rows.LastOrDefault();

        if (row is null)
        {
            // В первой строке одно место занимает плитка «Создать папку».
            row = new LibraryManagementFolderRowViewModel(
                Math.Max(1, capacity - 1),
                isFirstRow: true);
            rows.Add(row);
        }
        else if (row.IsFull)
        {
            row = new LibraryManagementFolderRowViewModel(capacity);
            rows.Add(row);
        }

        row.Add(folder);
    }

    private async Task ApplySimpleMaterialFilterAfterDelayAsync(int searchVersion)
    {
        try
        {
            await Task.Delay(SearchDelay, _viewCancellationToken);

            if (searchVersion == Volatile.Read(ref _simpleMaterialSearchVersion) &&
                IsSimpleMaterialsPage &&
                SelectedFolder is not null)
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
            LibraryManagementMaterialFilter.ARTICLES =>
                materials.Where(material =>
                    material.IsArticle),

            LibraryManagementMaterialFilter.QUESTIONS =>
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
                LibraryManagementMaterialSort.CUSTOM =>
                    materials.OrderBy(material =>
                        material.Position),

                LibraryManagementMaterialSort.RECENT_ACTIVITY =>
                    materials
                        .OrderByDescending(material =>
                            material.Material?.UpdatedAt ??
                            DateTime.MinValue)
                        .ThenBy(
                            material => material.Name,
                            StringComparer.OrdinalIgnoreCase),

                LibraryManagementMaterialSort.NAME =>
                    materials.OrderBy(
                        material => material.Name,
                        StringComparer.OrdinalIgnoreCase),

                LibraryManagementMaterialSort.NEWEST =>
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
            SIMPLE_MATERIAL_PAGE_SIZE;
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
        !_isSimpleMaterialsLoadingPreviousPage &&
        SimpleMaterialsHasMore;

    private ObservableCollection<LibraryManagementOrderItemViewModel> GetOrderCollection(
        LibraryOrderTarget target)
    {
        return target switch
        {
            LibraryOrderTarget.SECTIONS => Sections,
            LibraryOrderTarget.FOLDERS => Folders,
            LibraryOrderTarget.MATERIALS => Materials,
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
            case LibraryOrderTarget.SECTIONS:
                _pendingSectionOrder = null;
                break;

            case LibraryOrderTarget.FOLDERS when SelectedSection is not null:
                _pendingFolderOrders.Remove(SelectedSection.Id);
                break;

            case LibraryOrderTarget.MATERIALS when SelectedFolder is not null:
                _pendingMaterialOrders.Remove(SelectedFolder.Id);
                break;
        }
    }

    private bool CanAddFolder(LibraryManagementOrderItemViewModel? item)
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

    private void NotifySimpleFoldersStateChanged()
    {
        OnPropertyChanged(nameof(HasSimpleFolderSource));
        OnPropertyChanged(nameof(HasSimpleFolders));
        OnPropertyChanged(nameof(IsSimpleFoldersEmpty));
        OnPropertyChanged(nameof(HasNoSimpleFolderSearchResults));
        OnPropertyChanged(nameof(SimpleFoldersShownCountText));
        OnPropertyChanged(nameof(SimpleFoldersHasMore));
        OnPropertyChanged(nameof(SimpleFoldersHasPrevious));
        OnPropertyChanged(nameof(IsSimpleFoldersPaging));
        OnPropertyChanged(nameof(SimpleFoldersTotalCount));
        OnPropertyChanged(nameof(SimpleFolderWindowStartOffset));
        OnPropertyChanged(nameof(SimpleFolderWindowEndOffset));
        OnPropertyChanged(nameof(SimpleFolderCurrentPageOffset));
        OnPropertyChanged(nameof(SimpleFolderCachedPageCount));
        LoadNextSimpleFolderPageCommand.NotifyCanExecuteChanged();
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
        OnPropertyChanged(nameof(HasFolders));
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
