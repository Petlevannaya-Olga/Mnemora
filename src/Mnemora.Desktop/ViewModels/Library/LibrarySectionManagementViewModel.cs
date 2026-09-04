using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mnemora.Application.Library.GetHierarchyFoldersPage;
using Mnemora.Application.Library.GetManagementMaterialsPage;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class LibrarySectionManagementViewModel(
    IQueryDispatcher queryDispatcher)
    : ObservableObject
{
    private const int PageSize = LibraryPagingDefaults.PageSize;
    private const int VisiblePageLimit = 7;
    private const int CachePageLimit = 10;

    private readonly BoundedPagedWindow<LibraryManagementMaterialOverviewDto> _materialWindow =
        new(PageSize, VisiblePageLimit, CachePageLimit);

    private int _materialLoadVersion;

    public ObservableCollection<LibrarySectionManagementTreeNodeViewModel> Roots { get; } = [];
    public ObservableCollection<LibraryManagementOrderItemViewModel> Materials { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSection))]
    private LibrarySectionOverviewDto? _section;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedContainerName))]
    [NotifyPropertyChangedFor(nameof(IsRootSelected))]
    private LibrarySectionManagementTreeNodeViewModel? _selectedNode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRootSelected))]
    [NotifyPropertyChangedFor(nameof(HasFolders))]
    private LibrarySectionManagementTreeNodeViewModel? _rootNode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRootMaterials))]
    private int _rootMaterialsCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTreeError))]
    private string? _treeErrorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMaterialsError))]
    [NotifyPropertyChangedFor(nameof(IsMaterialsEmpty))]
    private string? _materialsErrorMessage;

    [ObservableProperty]
    private bool _isLoadingTree;

    [ObservableProperty]
    private bool _isLoadingFolders;

    [ObservableProperty]
    private bool _isTreeCollapsed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMaterialsEmpty))]
    private bool _isLoadingMaterials;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMaterialsPaging))]
    private bool _isLoadingNextMaterialsPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMaterialsPaging))]
    private bool _isLoadingPreviousMaterialsPage;

    public bool HasSection => Section is not null;
    public bool IsRootSelected => RootNode is not null && ReferenceEquals(SelectedNode, RootNode);
    public bool HasFolders => RootNode?.ChildFoldersCount > 0;
    public bool HasRootMaterials => RootMaterialsCount > 0;
    public bool HasTreeError => !string.IsNullOrWhiteSpace(TreeErrorMessage);
    public bool HasMaterialsError => !string.IsNullOrWhiteSpace(MaterialsErrorMessage);
    public bool HasMaterials => Materials.Count > 0;
    public bool IsMaterialsEmpty => !IsLoadingMaterials && !HasMaterialsError && !HasMaterials;
    public bool IsMaterialsPaging => IsLoadingNextMaterialsPage || IsLoadingPreviousMaterialsPage;
    public bool MaterialsHasMore => _materialWindow.HasNext && !IsLoadingNextMaterialsPage;
    public bool MaterialsHasPrevious => _materialWindow.HasPrevious && !IsLoadingPreviousMaterialsPage;
    public int MaterialsWindowStartOffset => _materialWindow.WindowStartOffset;
    public int MaterialsWindowEndOffset => _materialWindow.WindowEndOffset;

    public string SelectedContainerName => SelectedNode?.Name ?? Section?.Name ?? string.Empty;

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
                "Материалы",
                "Материалы не найдены",
                _materialWindow.CurrentPageOffset,
                visibleCount,
                _materialWindow.TotalCount,
                isSearchResult: false);
        }
    }

    public async Task InitializeAsync(
        LibrarySectionOverviewDto section,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(section);

        Section = section;
        IsTreeCollapsed = false;
        TreeErrorMessage = null;
        MaterialsErrorMessage = null;
        RootNode = null;
        RootMaterialsCount = 0;
        Roots.Clear();
        Materials.Clear();
        _materialWindow.Reset();

        LibrarySectionManagementTreeNodeViewModel root =
            LibrarySectionManagementTreeNodeViewModel.CreateSection(section);

        root.IsExpanded = true;
        root.IsSelected = true;
        RootNode = root;
        Roots.Add(root);
        SelectedNode = root;

        IsLoadingTree = true;

        try
        {
            Task foldersTask = root.ChildrenLoaded
                ? Task.CompletedTask
                : LoadFoldersPageAsync(root, 0, cancellationToken);

            Task materialsTask = ReloadMaterialsAsync(root, cancellationToken);

            await Task.WhenAll(foldersTask, materialsTask);
        }
        finally
        {
            IsLoadingTree = false;
        }
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
        if (!MaterialsHasMore || SelectedNode?.ContainerId is not Guid containerId)
        {
            return;
        }

        int version = _materialLoadVersion;
        int offset = _materialWindow.NextOffset;
        IsLoadingNextMaterialsPage = true;
        MaterialsErrorMessage = null;

        try
        {
            LibraryManagementMaterialsPageDto? page = await GetMaterialsPageAsync(
                containerId,
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
        if (!MaterialsHasPrevious || SelectedNode?.ContainerId is not Guid containerId)
        {
            return;
        }

        int version = _materialLoadVersion;
        int offset = _materialWindow.PreviousOffset;
        IsLoadingPreviousMaterialsPage = true;
        MaterialsErrorMessage = null;

        try
        {
            LibraryManagementMaterialsPageDto? page = await GetMaterialsPageAsync(
                containerId,
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
        if (node.ContainerId is not Guid containerId)
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
            LibraryManagementMaterialsPageDto? page = await GetMaterialsPageAsync(
                containerId,
                0,
                version,
                cancellationToken);

            if (page is null || version != _materialLoadVersion)
            {
                return;
            }

            _materialWindow.SetTotalCount(page.TotalCount);

            if (ReferenceEquals(node, RootNode))
            {
                RootMaterialsCount = page.TotalCount;
            }

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
                IsLoadingMaterials = false;
                NotifyMaterialsStateChanged();
            }
        }
    }

    private async Task<LibraryManagementMaterialsPageDto?> GetMaterialsPageAsync(
        Guid containerId,
        int offset,
        int version,
        CancellationToken cancellationToken)
    {
        if (version != _materialLoadVersion)
        {
            return null;
        }

        if (_materialWindow.TryGetCachedPage(
                offset,
                out IReadOnlyList<LibraryManagementMaterialOverviewDto> cached))
        {
            return new LibraryManagementMaterialsPageDto(
                cached,
                offset + cached.Count,
                offset + cached.Count < _materialWindow.TotalCount,
                _materialWindow.TotalCount,
                _materialWindow.TotalCount);
        }

        var result = await queryDispatcher.SendAsync<
            GetLibraryManagementMaterialsPageQuery,
            LibraryManagementMaterialsPageDto>(
            new GetLibraryManagementMaterialsPageQuery(
                containerId,
                Search: null,
                LibraryManagementMaterialPageFilter.All,
                LibraryManagementMaterialPageSort.Custom,
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
                                    ?? "Не удалось загрузить материалы";
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
                    out IReadOnlyList<LibraryManagementMaterialOverviewDto> page))
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
}
