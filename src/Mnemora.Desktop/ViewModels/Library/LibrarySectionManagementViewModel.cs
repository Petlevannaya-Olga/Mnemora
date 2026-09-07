using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Application.Library.GetHierarchyFoldersPage;
using Mnemora.Application.Library.GetSectionManagementItemsPage;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed record LibrarySectionManagementSortOption(
    string Name,
    LibrarySectionManagementItemSort Sort);

public sealed partial class LibrarySectionManagementViewModel(
    IQueryDispatcher queryDispatcher)
    : ObservableObject
{
    private const int PageSize = LibraryPagingDefaults.PageSize;
    private const int VisiblePageLimit = 7;
    private const int CachePageLimit = 10;

    private readonly BoundedPagedWindow<LibrarySectionManagementItemDto> _materialWindow =
        new(PageSize, VisiblePageLimit, CachePageLimit);

    private int _materialLoadVersion;
    private CancellationTokenSource? _flatListReloadCancellationTokenSource;

    public ObservableCollection<LibrarySectionManagementTreeNodeViewModel> Roots { get; } = [];
    public ObservableCollection<LibraryManagementOrderItemViewModel> Materials { get; } = [];

    public IReadOnlyList<LibrarySectionManagementSortOption> SortOptions { get; } =
    [
        new("Мой порядок", LibrarySectionManagementItemSort.Custom),
        new("Последняя активность", LibrarySectionManagementItemSort.RecentActivity),
        new("По названию", LibrarySectionManagementItemSort.Name),
        new("Сначала новые", LibrarySectionManagementItemSort.Newest),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSearchResults))]
    [NotifyPropertyChangedFor(nameof(IsSectionEmpty))]
    private string? _searchText;

    [ObservableProperty]
    private LibrarySectionManagementSortOption _selectedSortOption =
        new("Мой порядок", LibrarySectionManagementItemSort.Custom);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAllContentFilter))]
    [NotifyPropertyChangedFor(nameof(IsFoldersContentFilter))]
    [NotifyPropertyChangedFor(nameof(IsArticlesContentFilter))]
    [NotifyPropertyChangedFor(nameof(IsQuestionsContentFilter))]
    [NotifyPropertyChangedFor(nameof(HasActiveContentFilter))]
    [NotifyPropertyChangedFor(nameof(IsSectionEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSearchResults))]
    [NotifyPropertyChangedFor(nameof(EmptyResultsDescription))]
    private LibrarySectionManagementItemFilter _selectedContentFilter =
        LibrarySectionManagementItemFilter.All;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSection))]
    [NotifyPropertyChangedFor(nameof(IsSectionEmpty))]
    private LibrarySectionOverviewDto? _section;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedContainerName))]
    [NotifyPropertyChangedFor(nameof(IsRootSelected))]
    [NotifyPropertyChangedFor(nameof(IsSelectedFolder))]
    [NotifyPropertyChangedFor(nameof(EmptyMaterialsTitle))]
    [NotifyPropertyChangedFor(nameof(EmptyMaterialsDescription))]
    private LibrarySectionManagementTreeNodeViewModel? _selectedNode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRootSelected))]
    [NotifyPropertyChangedFor(nameof(HasFolders))]
    [NotifyPropertyChangedFor(nameof(IsSectionEmpty))]
    private LibrarySectionManagementTreeNodeViewModel? _rootNode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRootMaterials))]
    [NotifyPropertyChangedFor(nameof(IsSectionEmpty))]
    private int _rootMaterialsCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTreeError))]
    [NotifyPropertyChangedFor(nameof(IsSectionEmpty))]
    private string? _treeErrorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMaterialsError))]
    [NotifyPropertyChangedFor(nameof(IsMaterialsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsSectionEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSearchResults))]
    private string? _materialsErrorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSectionEmpty))]
    private bool _isLoadingTree;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSectionEmpty))]
    private bool _isLoadingFolders;

    [ObservableProperty]
    private bool _isTreeCollapsed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMaterialsEmpty))]
    [NotifyPropertyChangedFor(nameof(IsSectionEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSearchResults))]
    private bool _isLoadingMaterials;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSectionEmpty))]
    [NotifyPropertyChangedFor(nameof(HasNoSearchResults))]
    private bool _hasCompletedInitialLoad;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMaterialsPaging))]
    private bool _isLoadingNextMaterialsPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMaterialsPaging))]
    private bool _isLoadingPreviousMaterialsPage;

    public bool HasSection => Section is not null;
    public bool IsRootSelected => RootNode is not null && ReferenceEquals(SelectedNode, RootNode);
    public bool IsSelectedFolder => SelectedNode?.IsFolder == true;
    public bool HasFolders => RootNode is not null &&
                              (RootNode.ChildFoldersCount > 0 ||
                               RootNode.Children.Any(child => child.IsFolder));
    public bool HasRootMaterials => RootMaterialsCount > 0;
    public bool IsAllContentFilter => SelectedContentFilter == LibrarySectionManagementItemFilter.All;
    public bool IsFoldersContentFilter => SelectedContentFilter == LibrarySectionManagementItemFilter.Folders;
    public bool IsArticlesContentFilter => SelectedContentFilter == LibrarySectionManagementItemFilter.Articles;
    public bool IsQuestionsContentFilter => SelectedContentFilter == LibrarySectionManagementItemFilter.Questions;
    public bool HasActiveContentFilter => !IsAllContentFilter;
    public bool IsSectionEmpty =>
        HasSection &&
        HasCompletedInitialLoad &&
        !IsLoadingMaterials &&
        !HasMaterialsError &&
        string.IsNullOrWhiteSpace(SearchText) &&
        !HasActiveContentFilter &&
        _materialWindow.TotalCount == 0;
    public bool HasNoSearchResults =>
        HasSection &&
        HasCompletedInitialLoad &&
        !IsLoadingMaterials &&
        !HasMaterialsError &&
        (!string.IsNullOrWhiteSpace(SearchText) || HasActiveContentFilter) &&
        _materialWindow.TotalCount == 0;
    public bool HasTreeError => !string.IsNullOrWhiteSpace(TreeErrorMessage);
    public bool HasMaterialsError => !string.IsNullOrWhiteSpace(MaterialsErrorMessage);
    public bool HasMaterials => Materials.Count > 0;
    public bool IsMaterialsEmpty => !IsLoadingMaterials && !HasMaterialsError && !HasMaterials;
    public bool IsMaterialsPaging => IsLoadingNextMaterialsPage || IsLoadingPreviousMaterialsPage;
    public bool MaterialsHasMore => _materialWindow.HasNext && !IsLoadingNextMaterialsPage && !IsLoadingPreviousMaterialsPage;
    public bool MaterialsHasPrevious => _materialWindow.HasPrevious && !IsLoadingPreviousMaterialsPage && !IsLoadingNextMaterialsPage;
    public int MaterialsWindowStartOffset => _materialWindow.WindowStartOffset;
    public int MaterialsWindowEndOffset => _materialWindow.WindowEndOffset;

    public string SelectedContainerName => SelectedNode?.Name ?? Section?.Name ?? string.Empty;

    public string EmptyMaterialsTitle => IsSelectedFolder
        ? "В этой папке пока нет материалов"
        : "Здесь пока нет материалов";

    public string EmptyMaterialsDescription => IsSelectedFolder
        ? "Создайте материал или выберите другое место в структуре."
        : "Создайте материал прямо в разделе или выберите папку.";

    public string EmptyResultsDescription
    {
        get
        {
            bool hasSearch = !string.IsNullOrWhiteSpace(SearchText);

            return (hasSearch, HasActiveContentFilter) switch
            {
                (true, true) => "Попробуйте изменить запрос или фильтр.",
                (true, false) => "Попробуйте изменить запрос.",
                (false, true) => "По выбранному фильтру ничего не найдено.",
                _ => string.Empty,
            };
        }
    }

    public string MaterialsShownCountText
    {
        get
        {
            if (_materialWindow.TotalCount == 0)
            {
                return string.Empty;
            }

            int visibleCount = Math.Min(
                PageSize,
                Math.Max(0, _materialWindow.TotalCount - _materialWindow.CurrentPageOffset));

            return LibraryRangeTextFormatter.FormatEntity(
                "Элементы",
                "Ничего не найдено",
                _materialWindow.CurrentPageOffset,
                visibleCount,
                _materialWindow.TotalCount,
                isSearchResult: !string.IsNullOrWhiteSpace(SearchText) || HasActiveContentFilter);
        }
    }

    public async Task InitializeAsync(
        LibrarySectionOverviewDto section,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(section);

        _flatListReloadCancellationTokenSource?.Cancel();
        _flatListReloadCancellationTokenSource?.Dispose();
        _flatListReloadCancellationTokenSource = null;
        Interlocked.Increment(ref _materialLoadVersion);

        HasCompletedInitialLoad = false;
        IsLoadingMaterials = true;
        Section = section;
        IsTreeCollapsed = false;
        TreeErrorMessage = null;
        MaterialsErrorMessage = null;
        RootMaterialsCount = 0;
        Roots.Clear();
        Materials.Clear();
        _materialWindow.Reset();

        LibrarySectionManagementTreeNodeViewModel root =
            LibrarySectionManagementTreeNodeViewModel.CreateSection(section);

        root.IsExpanded = true;
        root.IsSelected = true;
        RootNode = root;
        SelectedNode = root;
        Roots.Add(root);

        await ReloadMaterialsAsync(root, cancellationToken);
        OnPropertyChanged(nameof(IsSectionEmpty));
    }

    public async Task ExpandAsync(
        LibrarySectionManagementTreeNodeViewModel? node,
        CancellationToken cancellationToken = default,
        bool forceReload = false)
    {
        if (node is null ||
            !node.IsNavigationNode ||
            node.ContainerId is null ||
            node.IsLoading ||
            (!forceReload && node.ChildrenLoaded))
        {
            return;
        }

        await LoadFoldersPageAsync(node, 0, cancellationToken);
    }

    public async Task SelectNodeAsync(
        LibrarySectionManagementTreeNodeViewModel? node,
        CancellationToken cancellationToken = default)
    {
        if (node is null || !node.IsNavigationNode || node.ContainerId is null)
        {
            return;
        }

        if (ReferenceEquals(SelectedNode, node))
        {
            return;
        }

        if (SelectedNode is not null)
        {
            SelectedNode.IsSelected = false;
        }

        node.IsSelected = true;
        SelectedNode = node;
        await ReloadMaterialsAsync(node, cancellationToken);
    }

    public async Task LoadMoreFoldersAsync(
        LibrarySectionManagementTreeNodeViewModel? loadMoreNode,
        CancellationToken cancellationToken = default)
    {
        if (loadMoreNode is not { IsLoadMore: true, Parent: { } parent } ||
            parent.IsLoading ||
            !parent.HasMore)
        {
            return;
        }

        RestoreNavigationSelection(loadMoreNode);
        await LoadFoldersPageAsync(parent, parent.NextOffset, cancellationToken);
    }

    public async Task RetryFoldersAsync(
        LibrarySectionManagementTreeNodeViewModel? errorNode,
        CancellationToken cancellationToken = default)
    {
        if (errorNode is not { IsError: true, Parent: { } parent })
        {
            return;
        }

        RestoreNavigationSelection(errorNode);
        await ExpandAsync(parent, cancellationToken, forceReload: true);
    }

    public async Task LoadNextMaterialsWindowAsync(
        CancellationToken cancellationToken = default)
    {
        if (!MaterialsHasMore || Section is null)
        {
            return;
        }

        int version = _materialLoadVersion;
        int offset = _materialWindow.NextOffset;

        if (_materialWindow.TryGetCachedPage(
                offset,
                out IReadOnlyList<LibrarySectionManagementItemDto> cached))
        {
            _materialWindow.ShowPage(offset, cached, PageWindowInsert.Append);
            RebuildMaterials();
            NotifyMaterialsStateChanged();
            return;
        }

        IsLoadingNextMaterialsPage = true;
        MaterialsErrorMessage = null;
        NotifyMaterialsStateChanged();

        try
        {
            await YieldForPagingLoaderAsync(cancellationToken);

            LibrarySectionManagementItemsPageDto? page = await GetMaterialsPageAsync(
                offset,
                version,
                cancellationToken);

            if (page is null || version != _materialLoadVersion)
            {
                return;
            }

            _materialWindow.SetTotalCount(page.TotalCount);
            _materialWindow.ShowPage(offset, page.Items, PageWindowInsert.Append);
            RebuildMaterials();
            NotifyMaterialsStateChanged();
        }
        finally
        {
            if (version == _materialLoadVersion)
            {
                IsLoadingNextMaterialsPage = false;
                NotifyMaterialsStateChanged();
            }
        }
    }

    public async Task LoadPreviousMaterialsWindowAsync(
        CancellationToken cancellationToken = default)
    {
        if (!MaterialsHasPrevious || Section is null)
        {
            return;
        }

        int version = _materialLoadVersion;
        int offset = _materialWindow.PreviousOffset;

        if (_materialWindow.TryGetCachedPage(
                offset,
                out IReadOnlyList<LibrarySectionManagementItemDto> cached))
        {
            _materialWindow.ShowPage(offset, cached, PageWindowInsert.Prepend);
            RebuildMaterials();
            NotifyMaterialsStateChanged();
            return;
        }

        IsLoadingPreviousMaterialsPage = true;
        MaterialsErrorMessage = null;
        NotifyMaterialsStateChanged();

        try
        {
            await YieldForPagingLoaderAsync(cancellationToken);

            LibrarySectionManagementItemsPageDto? page = await GetMaterialsPageAsync(
                offset,
                version,
                cancellationToken);

            if (page is null || version != _materialLoadVersion)
            {
                return;
            }

            _materialWindow.SetTotalCount(page.TotalCount);
            _materialWindow.ShowPage(offset, page.Items, PageWindowInsert.Prepend);
            RebuildMaterials();
            NotifyMaterialsStateChanged();
        }
        finally
        {
            if (version == _materialLoadVersion)
            {
                IsLoadingPreviousMaterialsPage = false;
                NotifyMaterialsStateChanged();
            }
        }
    }

    [RelayCommand]
    private void SelectAllContent() =>
        SelectedContentFilter = LibrarySectionManagementItemFilter.All;

    [RelayCommand]
    private void SelectFoldersContent() =>
        SelectedContentFilter = LibrarySectionManagementItemFilter.Folders;

    [RelayCommand]
    private void SelectArticlesContent() =>
        SelectedContentFilter = LibrarySectionManagementItemFilter.Articles;

    [RelayCommand]
    private void SelectQuestionsContent() =>
        SelectedContentFilter = LibrarySectionManagementItemFilter.Questions;

    [RelayCommand]
    private void ToggleTree()
    {
        IsTreeCollapsed = !IsTreeCollapsed;
    }

    [RelayCommand]
    private async Task SelectRootMaterialsAsync(CancellationToken cancellationToken)
    {
        if (RootNode is not null)
        {
            await SelectNodeAsync(RootNode, cancellationToken);
        }
    }

    [RelayCommand]
    private async Task RetryMaterialsAsync(CancellationToken cancellationToken)
    {
        if (SelectedNode is not null)
        {
            await ReloadMaterialsAsync(SelectedNode, cancellationToken);
        }
    }

    public async Task RefreshSelectedMaterialsAsync(
        CancellationToken cancellationToken = default)
    {
        if (SelectedNode is not null)
        {
            await ReloadMaterialsAsync(SelectedNode, cancellationToken);
        }
    }

    public async Task ReloadChildrenAndSelectAsync(
        LibrarySectionManagementTreeNodeViewModel parent,
        Guid? childToSelectId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parent);

        parent.Children.Clear();
        parent.ChildrenLoaded = false;
        parent.NextOffset = 0;
        parent.HasMore = false;

        await LoadFoldersPageAsync(parent, 0, cancellationToken);

        OnPropertyChanged(nameof(HasFolders));
        OnPropertyChanged(nameof(IsSectionEmpty));

        if (childToSelectId is not Guid targetId)
        {
            return;
        }

        LibrarySectionManagementTreeNodeViewModel? child =
            parent.Children.FirstOrDefault(item =>
                item.IsFolder && item.ContainerId == targetId);

        if (child is not null)
        {
            await SelectNodeAsync(child, cancellationToken);
        }
    }

    public void UpdateMaterialsViewport(double logicalItemOffset)
    {
        if (!_materialWindow.UpdateViewport(logicalItemOffset))
        {
            return;
        }

        OnPropertyChanged(nameof(MaterialsShownCountText));
    }

    private async Task LoadFoldersPageAsync(
        LibrarySectionManagementTreeNodeViewModel parent,
        int offset,
        CancellationToken cancellationToken)
    {
        if (parent.ContainerId is null || parent.IsLoading)
        {
            return;
        }

        parent.IsLoading = true;
        IsLoadingFolders = true;
        bool isPaging = offset > 0;

        try
        {
            RemoveAuxiliaryChildren(
                parent,
                keepPlaceholder: !isPaging,
                keepLoadMore: isPaging);

            if (!isPaging && !parent.Children.Any(child => child.IsPlaceholder))
            {
                parent.Children.Add(
                    LibrarySectionManagementTreeNodeViewModel.CreateLoading(parent));
            }

            var result = await queryDispatcher.SendAsync<
                GetLibraryHierarchyFoldersPageQuery,
                LibraryHierarchyFoldersPageDto>(
                new GetLibraryHierarchyFoldersPageQuery(
                    parent.ContainerId.Value,
                    offset,
                    PageSize),
                cancellationToken);

            RemoveAuxiliaryChildren(parent);

            if (result.IsFailure)
            {
                TreeErrorMessage = result.Error.FirstOrDefault()?.Message
                                   ?? "Не удалось загрузить папки";
                AddFolderError(parent, "Не удалось загрузить папки. Нажмите, чтобы повторить.");
                return;
            }

            TreeErrorMessage = null;

            foreach (LibraryHierarchyFolderDto folder in result.Value.Items)
            {
                if (parent.Children.Any(child =>
                        child.IsFolder && child.ContainerId == folder.Id))
                {
                    continue;
                }

                parent.Children.Add(
                    LibrarySectionManagementTreeNodeViewModel.CreateFolder(
                        folder,
                        parent));
            }

            parent.ChildrenLoaded = true;
            parent.NextOffset = result.Value.NextOffset;
            parent.HasMore = result.Value.HasMore;

            if (parent.HasMore)
            {
                parent.Children.Add(
                    LibrarySectionManagementTreeNodeViewModel.CreateLoadMore(parent));
            }

            if (ReferenceEquals(parent, RootNode))
            {
                OnPropertyChanged(nameof(HasFolders));
                OnPropertyChanged(nameof(IsSectionEmpty));
            }
        }
        finally
        {
            parent.IsLoading = false;
            IsLoadingFolders = false;
        }
    }

    private async Task ReloadMaterialsAsync(
        LibrarySectionManagementTreeNodeViewModel node,
        CancellationToken cancellationToken)
    {
        if (Section is null)
        {
            return;
        }

        int version = Interlocked.Increment(ref _materialLoadVersion);
        _materialWindow.Reset();
        Materials.Clear();
        MaterialsErrorMessage = null;
        IsLoadingMaterials = true;
        IsLoadingNextMaterialsPage = false;
        IsLoadingPreviousMaterialsPage = false;
        NotifyMaterialsStateChanged();

        try
        {
            LibrarySectionManagementItemsPageDto? page = await GetMaterialsPageAsync(
                0,
                version,
                cancellationToken);

            if (page is null || version != _materialLoadVersion)
            {
                return;
            }

            _materialWindow.SetTotalCount(page.TotalCount);

            if (page.Items.Count > 0)
            {
                _materialWindow.ShowPage(0, page.Items, PageWindowInsert.Append);
            }

            RebuildMaterials();
        }
        finally
        {
            if (version == _materialLoadVersion)
            {
                HasCompletedInitialLoad = true;
                IsLoadingMaterials = false;
                NotifyMaterialsStateChanged();
            }
        }
    }

    private async Task<LibrarySectionManagementItemsPageDto?> GetMaterialsPageAsync(
        int offset,
        int version,
        CancellationToken cancellationToken)
    {
        if (version != _materialLoadVersion || Section is null)
        {
            return null;
        }

        if (_materialWindow.TryGetCachedPage(
                offset,
                out IReadOnlyList<LibrarySectionManagementItemDto> cached))
        {
            return new LibrarySectionManagementItemsPageDto(
                cached,
                offset + cached.Count,
                offset + cached.Count < _materialWindow.TotalCount,
                _materialWindow.TotalCount,
                _materialWindow.TotalCount);
        }

        var result = await queryDispatcher.SendAsync<
            GetLibrarySectionManagementItemsPageQuery,
            LibrarySectionManagementItemsPageDto>(
            new GetLibrarySectionManagementItemsPageQuery(
                Section.Id,
                SearchText,
                SelectedContentFilter,
                SelectedSortOption.Sort,
                offset,
                PageSize),
            cancellationToken);

        if (version != _materialLoadVersion || cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        if (result.IsFailure)
        {
            MaterialsErrorMessage = result.Error.FirstOrDefault()?.Message
                                    ?? "Не удалось загрузить содержимое раздела";
            return null;
        }

        _materialWindow.StorePage(offset, result.Value.Items);
        return result.Value;
    }

    private void RebuildMaterials()
    {
        Materials.Clear();

        foreach (int offset in _materialWindow.VisibleOffsets)
        {
            if (!_materialWindow.TryGetCachedPage(
                    offset,
                    out IReadOnlyList<LibrarySectionManagementItemDto> page))
            {
                continue;
            }

            for (int index = 0; index < page.Count; index++)
            {
                Materials.Add(
                    new LibraryManagementOrderItemViewModel(
                        page[index],
                        offset + index + 1));
            }
        }
    }

    private void NotifyMaterialsStateChanged()
    {
        OnPropertyChanged(nameof(HasMaterials));
        OnPropertyChanged(nameof(IsMaterialsEmpty));
        OnPropertyChanged(nameof(MaterialsHasMore));
        OnPropertyChanged(nameof(MaterialsHasPrevious));
        OnPropertyChanged(nameof(MaterialsWindowStartOffset));
        OnPropertyChanged(nameof(MaterialsWindowEndOffset));
        OnPropertyChanged(nameof(MaterialsShownCountText));
        OnPropertyChanged(nameof(IsSectionEmpty));
        OnPropertyChanged(nameof(HasNoSearchResults));
    }


    private void RestoreNavigationSelection(
        LibrarySectionManagementTreeNodeViewModel auxiliaryNode)
    {
        auxiliaryNode.IsSelected = false;

        if (SelectedNode is not null)
        {
            SelectedNode.IsSelected = true;
        }
    }

    private static void RemoveAuxiliaryChildren(
        LibrarySectionManagementTreeNodeViewModel parent,
        bool keepPlaceholder = false,
        bool keepLoadMore = false)
    {
        for (int index = parent.Children.Count - 1; index >= 0; index--)
        {
            LibrarySectionManagementTreeNodeViewModel child = parent.Children[index];

            if ((!keepPlaceholder && child.IsPlaceholder) ||
                (!keepLoadMore && child.IsLoadMore) ||
                child.IsError)
            {
                parent.Children.RemoveAt(index);
            }
        }
    }

    private static void AddFolderError(
        LibrarySectionManagementTreeNodeViewModel parent,
        string message)
    {
        if (parent.Children.Any(child => child.IsError))
        {
            return;
        }

        parent.Children.Add(
            LibrarySectionManagementTreeNodeViewModel.CreateError(
                parent,
                message));
    }

    partial void OnSearchTextChanged(string? value)
    {
        OnPropertyChanged(nameof(EmptyResultsDescription));
        ScheduleFlatListReload(useDebounce: true);
    }

    partial void OnSelectedContentFilterChanged(LibrarySectionManagementItemFilter value)
    {
        OnPropertyChanged(nameof(MaterialsShownCountText));
        OnPropertyChanged(nameof(EmptyResultsDescription));
        ScheduleFlatListReload(useDebounce: false);
    }

    partial void OnSelectedSortOptionChanged(LibrarySectionManagementSortOption value) =>
        ScheduleFlatListReload(useDebounce: false);

    private void ScheduleFlatListReload(bool useDebounce)
    {
        if (Section is null || RootNode is null)
        {
            return;
        }

        _flatListReloadCancellationTokenSource?.Cancel();
        _flatListReloadCancellationTokenSource?.Dispose();
        _flatListReloadCancellationTokenSource = new CancellationTokenSource();
        Interlocked.Increment(ref _materialLoadVersion);

        // Empty-state must never be evaluated between changing UI parameters
        // and starting the actual reload.
        IsLoadingMaterials = true;
        IsLoadingNextMaterialsPage = false;
        IsLoadingPreviousMaterialsPage = false;
        NotifyMaterialsStateChanged();

        _ = ReloadFlatListFromUiAsync(
            _flatListReloadCancellationTokenSource.Token,
            useDebounce);
    }

    private async Task ReloadFlatListFromUiAsync(
        CancellationToken cancellationToken,
        bool useDebounce)
    {
        try
        {
            if (useDebounce)
            {
                await Task.Delay(250, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await YieldForPagingLoaderAsync(cancellationToken);

            if (RootNode is not null)
            {
                await ReloadMaterialsAsync(RootNode, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
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

}
