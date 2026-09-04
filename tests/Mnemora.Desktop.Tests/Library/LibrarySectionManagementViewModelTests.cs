using CSharpFunctionalExtensions;
using FluentAssertions;
using Mnemora.Application.Library.GetHierarchyFoldersPage;
using Mnemora.Application.Library.GetManagementMaterialsPage;
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
    public async Task InitializeAsync_LoadsOnlySelectedSectionRoot()
    {
        Guid sectionId = Guid.NewGuid();
        Guid rootId = Guid.NewGuid();
        Guid folderId = Guid.NewGuid();
        Guid materialId = Guid.NewGuid();

        LibrarySectionOverviewDto section = CreateSection(sectionId, rootId, foldersCount: 1);

        var dispatcher = new StubQueryDispatcher(query => query switch
        {
            GetLibraryHierarchyFoldersPageQuery folders when folders.ContainerId == rootId =>
                new LibraryHierarchyFoldersPageDto(
                    [CreateFolder(folderId, sectionId, rootId, "EF Core", 0)],
                    1,
                    false),
            GetLibraryManagementMaterialsPageQuery materials when materials.ContainerId == rootId =>
                CreateMaterialsPage(materialId, rootId, "Root material"),
            _ => throw Unexpected(query),
        });

        var viewModel = new LibrarySectionManagementViewModel(dispatcher);

        await viewModel.InitializeAsync(section);

        LibrarySectionManagementTreeNodeViewModel root = viewModel.Roots.Should().ContainSingle().Subject;
        root.IsSection.Should().BeTrue();
        root.ContainerId.Should().Be(rootId);
        root.Children.Where(node => node.IsFolder).Should().ContainSingle();
        viewModel.SelectedNode.Should().BeSameAs(root);
        viewModel.Materials.Should().ContainSingle(item => item.Id == materialId);
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
            GetLibraryManagementMaterialsPageQuery materials when materials.ContainerId == rootId =>
                EmptyMaterialsPage(),
            _ => throw Unexpected(query),
        });

        var viewModel = new LibrarySectionManagementViewModel(dispatcher);
        await viewModel.InitializeAsync(section);

        LibrarySectionManagementTreeNodeViewModel root = viewModel.Roots.Single();
        LibrarySectionManagementTreeNodeViewModel folder = root.Children.Single(node => node.IsFolder);

        nestedRequests.Should().Be(0);
        folder.Children.Should().ContainSingle(node => node.IsPlaceholder);

        await viewModel.ExpandAsync(folder);

        nestedRequests.Should().Be(1);
        folder.Children.Single(node => node.ContainerId == nestedFolderId)
            .Name.Should().Be("Change Tracking");
    }

    [Fact]
    public async Task SelectNodeAsync_LoadsMaterialsForSelectedFolder()
    {
        Guid sectionId = Guid.NewGuid();
        Guid rootId = Guid.NewGuid();
        Guid folderId = Guid.NewGuid();
        Guid rootMaterialId = Guid.NewGuid();
        Guid folderMaterialId = Guid.NewGuid();
        var requestedContainers = new List<Guid>();

        LibrarySectionOverviewDto section = CreateSection(sectionId, rootId, foldersCount: 1);

        var dispatcher = new StubQueryDispatcher(query => query switch
        {
            GetLibraryHierarchyFoldersPageQuery folders when folders.ContainerId == rootId =>
                new LibraryHierarchyFoldersPageDto(
                    [CreateFolder(folderId, sectionId, rootId, "EF Core", 0)],
                    1,
                    false),
            GetLibraryManagementMaterialsPageQuery materials =>
                TrackMaterialsRequest(
                    materials,
                    requestedContainers,
                    rootId,
                    rootMaterialId,
                    folderId,
                    folderMaterialId),
            _ => throw Unexpected(query),
        });

        var viewModel = new LibrarySectionManagementViewModel(dispatcher);
        await viewModel.InitializeAsync(section);

        LibrarySectionManagementTreeNodeViewModel folder = viewModel.Roots[0]
            .Children.Single(node => node.IsFolder);

        await viewModel.SelectNodeAsync(folder);

        requestedContainers.Should().Equal(rootId, folderId);
        viewModel.SelectedNode.Should().BeSameAs(folder);
        viewModel.Materials.Should().ContainSingle(item => item.Id == folderMaterialId);
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

    private static LibraryManagementMaterialsPageDto TrackMaterialsRequest(
        GetLibraryManagementMaterialsPageQuery query,
        ICollection<Guid> requestedContainers,
        Guid rootId,
        Guid rootMaterialId,
        Guid folderId,
        Guid folderMaterialId)
    {
        requestedContainers.Add(query.ContainerId);

        return query.ContainerId switch
        {
            var id when id == rootId => CreateMaterialsPage(rootMaterialId, rootId, "Root material"),
            var id when id == folderId => CreateMaterialsPage(folderMaterialId, folderId, "Folder material"),
            _ => EmptyMaterialsPage(),
        };
    }

    private static LibraryManagementMaterialsPageDto CreateMaterialsPage(
        Guid materialId,
        Guid containerId,
        string title)
    {
        var material = new LibraryManagementMaterialOverviewDto(
            materialId,
            Guid.NewGuid(),
            title,
            "Article",
            "Medium",
            "FileDocumentOutline",
            DateTime.UtcNow,
            DateTime.UtcNow,
            1,
            0)
        {
            ContainerId = containerId,
        };

        return new LibraryManagementMaterialsPageDto(
            [material],
            1,
            false,
            1,
            1);
    }

    private static LibraryManagementMaterialsPageDto EmptyMaterialsPage() =>
        new([], 0, false, 0, 0);

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
