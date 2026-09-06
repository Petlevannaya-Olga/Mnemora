using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Mnemora.Application.Library.GetHierarchyFoldersPage;
using Mnemora.Application.Library.GetHierarchySectionsPage;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;

namespace Mnemora.Desktop.ViewModels.Library;

public sealed partial class LibraryHierarchyViewModel : ObservableObject
{
    private const int PageSize = LibraryPagingDefaults.PageSize;
    private readonly IQueryDispatcher _queryDispatcher;

    public LibraryHierarchyViewModel(IQueryDispatcher queryDispatcher)
    {
        _queryDispatcher = queryDispatcher;
    }

    public ObservableCollection<LibraryHierarchyNodeViewModel> Roots { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task InitializeAsync(
        IReadOnlyList<LibraryContainerContentsDto> currentPath,
        Guid currentContainerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentPath);

        if (currentPath.Count == 0)
        {
            throw new ArgumentException(
                "Путь текущего контейнера не может быть пустым.",
                nameof(currentPath));
        }

        ErrorMessage = null;
        Roots.Clear();

        LibraryHierarchyNodeViewModel libraryRoot =
            LibraryHierarchyNodeViewModel.CreateLibrary();

        libraryRoot.IsExpanded = true;
        Roots.Add(libraryRoot);

        if (!currentPath[0].Container.IsRoot)
        {
            ErrorMessage = "Не удалось определить корень текущего раздела";
            AddErrorIfMissing(
                libraryRoot,
                "Не удалось построить дерево раздела");
            return;
        }

        await LoadSectionsPageAsync(
            libraryRoot,
            0,
            cancellationToken);

        await EnsureCurrentPathAsync(
            libraryRoot,
            currentPath,
            currentContainerId,
            cancellationToken);
    }

    public async Task ExpandAsync(
        LibraryHierarchyNodeViewModel? node,
        CancellationToken cancellationToken = default,
        bool forceReload = false)
    {
        if (node is null ||
            node.IsPlaceholder ||
            node.IsLoadMore ||
            node.IsError)
        {
            return;
        }

        if (!forceReload && node.ChildrenLoaded)
        {
            return;
        }

        if (node.IsLibrary)
        {
            await LoadSectionsPageAsync(node, 0, cancellationToken);
            return;
        }

        if (node.ContainerId is null || node.ChildFoldersCount <= 0)
        {
            node.ChildrenLoaded = true;
            return;
        }

        await LoadFoldersPageAsync(
            node,
            0,
            cancellationToken);
    }

    public async Task LoadMoreAsync(
        LibraryHierarchyNodeViewModel? loadMoreNode,
        CancellationToken cancellationToken = default)
    {
        if (loadMoreNode is not { IsLoadMore: true, Parent: { } parent } ||
            parent.IsLoading ||
            !parent.HasMore)
        {
            return;
        }

        if (parent.IsLibrary)
        {
            await LoadSectionsPageAsync(
                parent,
                parent.NextOffset,
                cancellationToken);
            return;
        }

        if (parent.ContainerId is not null)
        {
            await LoadFoldersPageAsync(
                parent,
                parent.NextOffset,
                cancellationToken);
        }
    }

    public async Task RetryAsync(
        LibraryHierarchyNodeViewModel? errorNode,
        CancellationToken cancellationToken = default)
    {
        if (errorNode is not { IsError: true, Parent: { } parent })
        {
            return;
        }

        await ExpandAsync(
            parent,
            cancellationToken,
            forceReload: true);
    }

    private async Task LoadSectionsPageAsync(
        LibraryHierarchyNodeViewModel root,
        int offset,
        CancellationToken cancellationToken)
    {
        if (root.IsLoading)
        {
            return;
        }

        root.IsLoading = true;

        try
        {
            RemoveAuxiliaryChildren(root);

            var result = await _queryDispatcher.SendAsync<
                GetLibraryHierarchySectionsPageQuery,
                LibraryHierarchySectionsPageDto>(
                new GetLibraryHierarchySectionsPageQuery(
                    offset,
                    PageSize),
                cancellationToken);

            if (result.IsFailure)
            {
                ErrorMessage = result.Error.FirstOrDefault()?.Message
                               ?? "Не удалось загрузить структуру библиотеки";

                AddErrorIfMissing(root, "Не удалось загрузить разделы");
                return;
            }

            foreach (LibraryHierarchySectionDto section in result.Value.Items)
            {
                if (root.Children.Any(child =>
                        child.IsSection &&
                        child.ContainerId == section.RootContainerId))
                {
                    continue;
                }

                root.Children.Add(
                    LibraryHierarchyNodeViewModel.CreateSection(
                        section,
                        root));
            }

            root.ChildrenLoaded = true;
            root.NextOffset = result.Value.NextOffset;
            root.HasMore = result.Value.HasMore;

            if (root.HasMore)
            {
                root.Children.Add(
                    LibraryHierarchyNodeViewModel.CreateLoadMore(
                        root,
                        "Показать ещё разделы"));
            }
        }
        finally
        {
            root.IsLoading = false;
        }
    }

    private async Task LoadFoldersPageAsync(
        LibraryHierarchyNodeViewModel parent,
        int offset,
        CancellationToken cancellationToken)
    {
        if (parent.IsLoading || parent.ContainerId is null)
        {
            return;
        }

        parent.IsLoading = true;

        try
        {
            RemoveAuxiliaryChildren(parent);

            var result = await _queryDispatcher.SendAsync<
                GetLibraryHierarchyFoldersPageQuery,
                LibraryHierarchyFoldersPageDto>(
                new GetLibraryHierarchyFoldersPageQuery(
                    parent.ContainerId.Value,
                    offset,
                    PageSize),
                cancellationToken);

            if (result.IsFailure)
            {
                ErrorMessage = result.Error.FirstOrDefault()?.Message
                               ?? "Не удалось загрузить часть дерева";

                AddErrorIfMissing(parent, "Не удалось загрузить папки");
                return;
            }

            foreach (LibraryHierarchyFolderDto folder in result.Value.Items)
            {
                if (parent.Children.Any(child =>
                        child.IsFolder &&
                        child.ContainerId == folder.Id))
                {
                    continue;
                }

                parent.Children.Add(
                    LibraryHierarchyNodeViewModel.CreateFolder(
                        folder,
                        parent));
            }

            parent.ChildrenLoaded = true;
            parent.NextOffset = result.Value.NextOffset;
            parent.HasMore = result.Value.HasMore;

            if (parent.HasMore)
            {
                parent.Children.Add(
                    LibraryHierarchyNodeViewModel.CreateLoadMore(
                        parent,
                        "Показать ещё папки"));
            }
        }
        finally
        {
            parent.IsLoading = false;
        }
    }

    private async Task EnsureCurrentPathAsync(
        LibraryHierarchyNodeViewModel libraryRoot,
        IReadOnlyList<LibraryContainerContentsDto> currentPath,
        Guid currentContainerId,
        CancellationToken cancellationToken)
    {
        LibraryContainerContentsDto sectionRootContents = currentPath[0];

        LibraryHierarchyNodeViewModel? currentSection =
            libraryRoot.Children.FirstOrDefault(child =>
                child.IsSection &&
                child.ContainerId == sectionRootContents.Container.Id);

        if (currentSection is null)
        {
            currentSection =
                LibraryHierarchyNodeViewModel.CreateSection(
                    sectionRootContents,
                    libraryRoot);

            InsertBeforeLoadMore(libraryRoot, currentSection);
        }

        if (currentPath.Count == 1)
        {
            currentSection.IsCurrent = true;
            return;
        }

        currentSection.IsExpanded = true;
        LibraryHierarchyNodeViewModel parent = currentSection;

        for (int index = 1; index < currentPath.Count; index++)
        {
            LibraryContainerContentsDto pathItem = currentPath[index];

            if (!parent.ChildrenLoaded)
            {
                await ExpandAsync(parent, cancellationToken);
            }

            LibraryHierarchyNodeViewModel? child =
                parent.Children.FirstOrDefault(item =>
                    item.IsFolder &&
                    item.ContainerId == pathItem.Container.Id);

            if (child is null)
            {
                child = LibraryHierarchyNodeViewModel.CreateFolder(
                    pathItem,
                    parent);

                InsertBeforeLoadMore(parent, child);
            }

            bool isCurrent = pathItem.Container.Id == currentContainerId;
            child.IsCurrent = isCurrent;

            if (!isCurrent)
            {
                child.IsExpanded = true;
            }

            parent = child;
        }
    }

    private static void InsertBeforeLoadMore(
        LibraryHierarchyNodeViewModel parent,
        LibraryHierarchyNodeViewModel node)
    {
        int loadMoreIndex = -1;

        for (int index = 0; index < parent.Children.Count; index++)
        {
            if (parent.Children[index].IsLoadMore)
            {
                loadMoreIndex = index;
                break;
            }
        }

        if (loadMoreIndex >= 0)
        {
            parent.Children.Insert(loadMoreIndex, node);
        }
        else
        {
            parent.Children.Add(node);
        }
    }

    private static void RemoveAuxiliaryChildren(
        LibraryHierarchyNodeViewModel parent)
    {
        for (int index = parent.Children.Count - 1; index >= 0; index--)
        {
            LibraryHierarchyNodeViewModel child = parent.Children[index];

            if (child.IsPlaceholder || child.IsLoadMore || child.IsError)
            {
                parent.Children.RemoveAt(index);
            }
        }
    }

    private static void AddErrorIfMissing(
        LibraryHierarchyNodeViewModel parent,
        string message)
    {
        if (parent.Children.Any(child => child.IsError))
        {
            return;
        }

        parent.Children.Add(
            LibraryHierarchyNodeViewModel.CreateError(
                parent,
                message));
    }
}
