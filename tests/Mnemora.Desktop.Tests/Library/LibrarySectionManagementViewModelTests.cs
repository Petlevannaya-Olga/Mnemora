using CSharpFunctionalExtensions;
using FluentAssertions;
using Mnemora.Application.Library.GetHierarchyFoldersPage;
using Mnemora.Application.Library.GetSectionManagementItemsPage;
using Mnemora.Application.Queries;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Desktop.ViewModels.Library;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;
using Xunit;

namespace Mnemora.Desktop.Tests.Library;

public sealed class LibrarySectionManagementViewModelTests
{
    [Fact]
    public async Task InitializeAsync_LoadsSectionItemsWithoutExpandingFolderTree()
    {
        Guid sectionId = Guid.NewGuid();
        Guid rootId = Guid.NewGuid();
        Guid folderId = Guid.NewGuid();
        Guid materialId = Guid.NewGuid();

        LibrarySectionOverviewDto section = CreateSection(sectionId, rootId, foldersCount: 1);

        var dispatcher = new StubQueryDispatcher(query => query switch
        {
            GetLibrarySectionManagementItemsPageQuery items when
                items.SectionId == sectionId && items.Offset == 0 =>
                CreateItemsPage(
                    CreateFolderItem(folderId, rootId, "EF Core"),
                    CreateMaterialItem(materialId, rootId, "Root material")),
            _ => throw Unexpected(query),
        });

        var viewModel = new LibrarySectionManagementViewModel(dispatcher);

        await viewModel.InitializeAsync(section);

        LibrarySectionManagementTreeNodeViewModel root = viewModel.Roots.Should()
            .ContainSingle().Subject;
        root.IsSection.Should().BeTrue();
        root.ContainerId.Should().Be(rootId);
        root.Children.Should().ContainSingle(child => child.IsPlaceholder);
        viewModel.SelectedNode.Should().BeSameAs(root);
        viewModel.Materials.Should().HaveCount(2);
        viewModel.Materials.Should().ContainSingle(item => item.Id == folderId && item.IsManagementFolder);
        viewModel.Materials.Should().ContainSingle(item => item.Id == materialId && !item.IsManagementFolder);
    }

    [Fact]
    public async Task ExpandAsync_LoadsOnlyRequestedFolderLevel()
    {
        Guid sectionId = Guid.NewGuid();
        Guid rootId = Guid.NewGuid();
        Guid folderId = Guid.NewGuid();
        Guid nestedFolderId = Guid.NewGuid();
        int nestedRequests = 0;

        LibrarySectionOverviewDto section = CreateSection(sectionId, rootId, foldersCount: 1);

        var dispatcher = new StubQueryDispatcher(query => query switch
        {
            GetLibrarySectionManagementItemsPageQuery items when items.SectionId == sectionId =>
                EmptyItemsPage(),
            GetLibraryHierarchyFoldersPageQuery folders when folders.ContainerId == rootId =>
                new LibraryHierarchyFoldersPageDto(
                    [CreateFolder(folderId, sectionId, rootId, "EF Core", 1)],
                    1,
                    false),
            GetLibraryHierarchyFoldersPageQuery folders when folders.ContainerId == folderId =>
                CreateNestedFolderPage(
                    folders,
                    ref nestedRequests,
                    nestedFolderId,
                    sectionId,
                    folderId),
            _ => throw Unexpected(query),
        });

        var viewModel = new LibrarySectionManagementViewModel(dispatcher);
        await viewModel.InitializeAsync(section);

        LibrarySectionManagementTreeNodeViewModel root = viewModel.Roots.Single();
        root.Children.Should().ContainSingle(child => child.IsPlaceholder);
        nestedRequests.Should().Be(0);

        // Дерево загружается по требованию: сначала корневые папки, затем выбранная папка.
        await viewModel.ExpandAsync(root);
        LibrarySectionManagementTreeNodeViewModel folder = root.Children.Single(node => node.IsFolder);

        nestedRequests.Should().Be(0);
        folder.Children.Should().ContainSingle(node => node.IsPlaceholder);

        await viewModel.ExpandAsync(folder);

        nestedRequests.Should().Be(1);
        folder.Children.Single(node => node.ContainerId == nestedFolderId)
            .Name.Should().Be("Change Tracking");
    }

    [Fact]
    public async Task SelectNodeAsync_ChangesSelectionAndReloadsSectionItems()
    {
        Guid sectionId = Guid.NewGuid();
        Guid rootId = Guid.NewGuid();
        Guid folderId = Guid.NewGuid();
        Guid materialId = Guid.NewGuid();
        var requestedSections = new List<Guid>();

        LibrarySectionOverviewDto section = CreateSection(sectionId, rootId, foldersCount: 1);

        var dispatcher = new StubQueryDispatcher(query => query switch
        {
            GetLibrarySectionManagementItemsPageQuery items =>
                TrackItemsRequest(items, requestedSections, materialId, rootId),
            GetLibraryHierarchyFoldersPageQuery folders when folders.ContainerId == rootId =>
                new LibraryHierarchyFoldersPageDto(
                    [CreateFolder(folderId, sectionId, rootId, "EF Core", 0)],
                    1,
                    false),
            _ => throw Unexpected(query),
        });

        var viewModel = new LibrarySectionManagementViewModel(dispatcher);
        await viewModel.InitializeAsync(section);

        LibrarySectionManagementTreeNodeViewModel root = viewModel.Roots.Single();
        await viewModel.ExpandAsync(root);
        LibrarySectionManagementTreeNodeViewModel folder = root.Children.Single(node => node.IsFolder);

        await viewModel.SelectNodeAsync(folder);

        // Плоский список управления относится к разделу, а не к выбранной папке.
        requestedSections.Should().Equal(sectionId, sectionId);
        viewModel.SelectedNode.Should().BeSameAs(folder);
        viewModel.SelectedContainerName.Should().Be("EF Core");
        viewModel.Materials.Should().ContainSingle(item => item.Id == materialId);
    }

    [Fact]
    public async Task CreationTarget_UsesSelectedFolderAndKeepsSectionRootAvailable()
    {
        Guid sectionId = Guid.NewGuid();
        Guid rootId = Guid.NewGuid();
        Guid folderId = Guid.NewGuid();
        var dispatcher = new StubQueryDispatcher(query => query switch
        {
            GetLibrarySectionManagementItemsPageQuery => EmptyItemsPage(),
            GetLibraryHierarchyFoldersPageQuery folders when folders.ContainerId == rootId =>
                new LibraryHierarchyFoldersPageDto(
                    [CreateFolder(folderId, sectionId, rootId, "EF Core", 0)],
                    1,
                    false),
            _ => throw Unexpected(query),
        });
        var viewModel = new LibrarySectionManagementViewModel(dispatcher);
        await viewModel.InitializeAsync(CreateSection(sectionId, rootId, 1));

        viewModel.GetCreationTarget(forFolder: false)?.ContainerId.Should().Be(rootId);
        viewModel.GetCreationTarget(forFolder: true)?.ContainerId.Should().Be(rootId);

        LibrarySectionManagementTreeNodeViewModel root = viewModel.Roots.Single();
        await viewModel.ExpandAsync(root);
        LibrarySectionManagementTreeNodeViewModel folder = root.Children.Single(node => node.IsFolder);
        await viewModel.SelectNodeAsync(folder);

        viewModel.GetCreationTarget(forFolder: false).Should().BeSameAs(folder);
        viewModel.GetCreationTarget(forFolder: true).Should().BeSameAs(folder);
    }

    [Fact]
    public async Task CreationTarget_AtMaximumDepth_AllowsMaterialsButNotChildFolders()
    {
        Guid sectionId = Guid.NewGuid();
        Guid rootId = Guid.NewGuid();
        var dispatcher = new StubQueryDispatcher(query => query switch
        {
            GetLibrarySectionManagementItemsPageQuery => EmptyItemsPage(),
            _ => throw Unexpected(query),
        });
        var viewModel = new LibrarySectionManagementViewModel(dispatcher);
        await viewModel.InitializeAsync(CreateSection(sectionId, rootId, 0));

        LibrarySectionManagementTreeNodeViewModel parent = viewModel.Roots.Single();
        for (int depth = 1; depth <= 3; depth++)
        {
            Guid folderId = Guid.NewGuid();
            parent = LibrarySectionManagementTreeNodeViewModel.CreateFolder(
                CreateFolder(folderId, sectionId, parent.ContainerId!.Value, $"Level {depth}", 0),
                parent);
        }
        await viewModel.SelectNodeAsync(parent);

        viewModel.GetCreationTarget(forFolder: false).Should().BeSameAs(parent);
        viewModel.GetCreationTarget(forFolder: true).Should().BeNull();
    }

    private static LibrarySectionOverviewDto CreateSection(
        Guid sectionId,
        Guid rootId,
        int foldersCount) =>
        new(
            sectionId,
            rootId,
            "C#",
            "Teal",
            "BookOpenPageVariant",
            DateTime.UtcNow,
            DateTime.UtcNow,
            DateTime.UtcNow,
            foldersCount,
            0,
            0,
            0,
            0);

    private static LibraryHierarchyFolderDto CreateFolder(
        Guid id,
        Guid sectionId,
        Guid parentId,
        string name,
        int childFoldersCount) =>
        new(
            id,
            sectionId,
            parentId,
            name,
            "Teal",
            "Folder",
            childFoldersCount);

    private static LibraryHierarchyFoldersPageDto CreateNestedFolderPage(
        GetLibraryHierarchyFoldersPageQuery query,
        ref int requestCount,
        Guid nestedFolderId,
        Guid sectionId,
        Guid parentId)
    {
        requestCount++;

        return new LibraryHierarchyFoldersPageDto(
            [CreateFolder(nestedFolderId, sectionId, parentId, "Change Tracking", 0)],
            query.Offset + 1,
            false);
    }

    private static LibrarySectionManagementItemsPageDto TrackItemsRequest(
        GetLibrarySectionManagementItemsPageQuery query,
        ICollection<Guid> requestedSections,
        Guid materialId,
        Guid rootId)
    {
        requestedSections.Add(query.SectionId);
        return CreateItemsPage(CreateMaterialItem(materialId, rootId, "Root material"));
    }

    private static LibrarySectionManagementItemDto CreateFolderItem(
        Guid folderId,
        Guid rootId,
        string name) =>
        new(
            folderId,
            LibrarySectionManagementItemKind.FOLDER,
            name,
            "Раздел",
            "Folder",
            null,
            null,
            0,
            DateTime.UtcNow,
            DateTime.UtcNow,
            0,
            folderId,
            rootId);

    private static LibrarySectionManagementItemDto CreateMaterialItem(
        Guid materialId,
        Guid containerId,
        string title) =>
        new(
            materialId,
            LibrarySectionManagementItemKind.MATERIAL,
            title,
            "Раздел",
            "FileDocumentOutline",
            "Article",
            "Medium",
            0,
            DateTime.UtcNow,
            DateTime.UtcNow,
            0,
            containerId,
            null);

    private static LibrarySectionManagementItemsPageDto CreateItemsPage(
        params LibrarySectionManagementItemDto[] items) =>
        new(items, items.Length, false, items.Length, items.Length);

    private static LibrarySectionManagementItemsPageDto EmptyItemsPage() =>
        CreateItemsPage();

    private static Exception Unexpected(object query) =>
        new InvalidOperationException($"Неожиданный запрос {query.GetType().Name}");

    private sealed class StubQueryDispatcher(Func<object, object> handler) : IQueryDispatcher
    {
        public Task<Result<TResponse, Errors>> SendAsync<TQuery, TResponse>(
            TQuery query,
            CancellationToken cancellationToken = default)
            where TQuery : IQuery
        {
            cancellationToken.ThrowIfCancellationRequested();
            object response = handler(query);

            return Task.FromResult(
                Result.Success<TResponse, Errors>((TResponse)response));
        }
    }
}
