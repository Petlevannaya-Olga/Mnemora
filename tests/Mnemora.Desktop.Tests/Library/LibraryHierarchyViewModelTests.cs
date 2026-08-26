using CSharpFunctionalExtensions;
using FluentAssertions;
using Mnemora.Application.Library.GetHierarchyFoldersPage;
using Mnemora.Application.Library.GetHierarchySectionsPage;
using Mnemora.Application.Queries;
using Mnemora.Contracts.Library;
using Mnemora.Desktop.ViewModels.Library;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;
using Xunit;

namespace Mnemora.Desktop.Tests.Library;

public sealed class LibraryHierarchyViewModelTests
{
    [Fact]
    public async Task InitializeAsync_BuildsCurrentPathAndExpandsAncestors()
    {
        Guid sectionId = Guid.NewGuid();
        Guid rootId = Guid.NewGuid();
        Guid currentFolderId = Guid.NewGuid();
        Guid siblingFolderId = Guid.NewGuid();

        LibraryContainerContentsDto rootContents = CreateContents(
            rootId,
            sectionId,
            null,
            0,
            "C#",
            2);

        LibraryContainerContentsDto currentFolderContents = CreateContents(
            currentFolderId,
            sectionId,
            rootId,
            1,
            "EF Core",
            0);

        var dispatcher = new StubQueryDispatcher(query => query switch
        {
            GetLibraryHierarchySectionsPageQuery => CreateSectionsPage(
                sectionId,
                rootId,
                "C#",
                2),
            GetLibraryHierarchyFoldersPageQuery foldersQuery
                when foldersQuery.ContainerId == rootId =>
                new LibraryHierarchyFoldersPageDto(
                    [
                        CreateFolder(currentFolderId, sectionId, rootId, 1, "EF Core", 0),
                        CreateFolder(siblingFolderId, sectionId, rootId, 1, "ASP.NET Core", 0),
                    ],
                    2,
                    false),
            _ => throw new InvalidOperationException(
                $"Неожиданный запрос {query.GetType().Name}"),
        });

        var viewModel = new LibraryHierarchyViewModel(dispatcher);

        await viewModel.InitializeAsync(
            [rootContents, currentFolderContents],
            currentFolderId);

        LibraryHierarchyNodeViewModel library = viewModel.Roots.Should().ContainSingle().Subject;
        LibraryHierarchyNodeViewModel section = library.Children.Should()
            .ContainSingle(node => node.IsSection)
            .Subject;

        section.IsExpanded.Should().BeTrue();
        section.Children.Where(node => node.IsFolder).Should().HaveCount(2);
        section.Children.Single(node => node.ContainerId == currentFolderId)
            .IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task ExpandAsync_LoadsOnlyRequestedFolderLevel()
    {
        Guid sectionId = Guid.NewGuid();
        Guid rootId = Guid.NewGuid();
        Guid folderId = Guid.NewGuid();
        Guid nestedFolderId = Guid.NewGuid();

        LibraryContainerContentsDto rootContents = CreateContents(
            rootId,
            sectionId,
            null,
            0,
            "C#",
            1);

        int nestedRequests = 0;

        var dispatcher = new StubQueryDispatcher(query => query switch
        {
            GetLibraryHierarchySectionsPageQuery => CreateSectionsPage(
                sectionId,
                rootId,
                "C#",
                1),
            GetLibraryHierarchyFoldersPageQuery foldersQuery
                when foldersQuery.ContainerId == rootId =>
                new LibraryHierarchyFoldersPageDto(
                    [CreateFolder(folderId, sectionId, rootId, 1, "EF Core", 1)],
                    1,
                    false),
            GetLibraryHierarchyFoldersPageQuery foldersQuery
                when foldersQuery.ContainerId == folderId =>
                CreateNestedPage(
                    foldersQuery,
                    ref nestedRequests,
                    nestedFolderId,
                    sectionId,
                    folderId),
            _ => throw new InvalidOperationException(
                $"Неожиданный запрос {query.GetType().Name}"),
        });

        var viewModel = new LibraryHierarchyViewModel(dispatcher);

        await viewModel.InitializeAsync([rootContents], rootId);

        LibraryHierarchyNodeViewModel section = viewModel.Roots[0]
            .Children.Single(node => node.IsSection);

        await viewModel.ExpandAsync(section);

        LibraryHierarchyNodeViewModel folder = section.Children
            .Single(node => node.ContainerId == folderId);

        nestedRequests.Should().Be(0);
        folder.Children.Should().ContainSingle(node => node.IsPlaceholder);

        await viewModel.ExpandAsync(folder);

        nestedRequests.Should().Be(1);
        folder.Children.Single(node => node.ContainerId == nestedFolderId)
            .Name.Should().Be("Change Tracking");
    }

    [Fact]
    public async Task LoadMoreAsync_AppendsNextFolderPageWithoutDuplicates()
    {
        Guid sectionId = Guid.NewGuid();
        Guid rootId = Guid.NewGuid();
        Guid firstFolderId = Guid.NewGuid();
        Guid secondFolderId = Guid.NewGuid();

        LibraryContainerContentsDto rootContents = CreateContents(
            rootId,
            sectionId,
            null,
            0,
            "C#",
            2);

        var dispatcher = new StubQueryDispatcher(query => query switch
        {
            GetLibraryHierarchySectionsPageQuery => CreateSectionsPage(
                sectionId,
                rootId,
                "C#",
                2),
            GetLibraryHierarchyFoldersPageQuery foldersQuery
                when foldersQuery.ContainerId == rootId && foldersQuery.Offset == 0 =>
                new LibraryHierarchyFoldersPageDto(
                    [CreateFolder(firstFolderId, sectionId, rootId, 1, "A", 0)],
                    1,
                    true),
            GetLibraryHierarchyFoldersPageQuery foldersQuery
                when foldersQuery.ContainerId == rootId && foldersQuery.Offset == 1 =>
                new LibraryHierarchyFoldersPageDto(
                    [
                        CreateFolder(firstFolderId, sectionId, rootId, 1, "A", 0),
                        CreateFolder(secondFolderId, sectionId, rootId, 1, "B", 0),
                    ],
                    2,
                    false),
            _ => throw new InvalidOperationException(
                $"Неожиданный запрос {query.GetType().Name}"),
        });

        var viewModel = new LibraryHierarchyViewModel(dispatcher);

        await viewModel.InitializeAsync([rootContents], rootId);

        LibraryHierarchyNodeViewModel section = viewModel.Roots[0]
            .Children.Single(node => node.IsSection);

        await viewModel.ExpandAsync(section);

        LibraryHierarchyNodeViewModel loadMore = section.Children
            .Single(node => node.IsLoadMore);

        await viewModel.LoadMoreAsync(loadMore);

        section.Children.Where(node => node.IsFolder)
            .Select(node => node.ContainerId)
            .Should()
            .BeEquivalentTo([firstFolderId, secondFolderId]);
        section.Children.Should().NotContain(node => node.IsLoadMore);
    }

    [Fact]
    public async Task LoadMoreAsync_AppendsNextSectionPageWithoutUsingOverviewQuery()
    {
        Guid currentSectionId = Guid.NewGuid();
        Guid currentRootId = Guid.NewGuid();
        Guid secondSectionId = Guid.NewGuid();
        Guid secondRootId = Guid.NewGuid();

        LibraryContainerContentsDto rootContents = CreateContents(
            currentRootId,
            currentSectionId,
            null,
            0,
            "A",
            0);

        var dispatcher = new StubQueryDispatcher(query => query switch
        {
            GetLibraryHierarchySectionsPageQuery sectionsQuery
                when sectionsQuery.Offset == 0 =>
                new LibraryHierarchySectionsPageDto(
                    [
                        new LibraryHierarchySectionDto(
                            currentSectionId,
                            currentRootId,
                            "A",
                            "Teal",
                            "BookOpenPageVariant",
                            0),
                    ],
                    1,
                    true),
            GetLibraryHierarchySectionsPageQuery sectionsQuery
                when sectionsQuery.Offset == 1 =>
                new LibraryHierarchySectionsPageDto(
                    [
                        new LibraryHierarchySectionDto(
                            secondSectionId,
                            secondRootId,
                            "B",
                            "Teal",
                            "BookOpenPageVariant",
                            0),
                    ],
                    2,
                    false),
            _ => throw new InvalidOperationException(
                $"Неожиданный запрос {query.GetType().Name}"),
        });

        var viewModel = new LibraryHierarchyViewModel(dispatcher);

        await viewModel.InitializeAsync([rootContents], currentRootId);

        LibraryHierarchyNodeViewModel library = viewModel.Roots.Single();
        LibraryHierarchyNodeViewModel loadMore = library.Children
            .Single(node => node.IsLoadMore);

        await viewModel.LoadMoreAsync(loadMore);

        library.Children.Where(node => node.IsSection)
            .Select(node => node.ContainerId)
            .Should()
            .BeEquivalentTo([currentRootId, secondRootId]);
        library.Children.Should().NotContain(node => node.IsLoadMore);
    }

    private static LibraryHierarchySectionsPageDto CreateSectionsPage(
        Guid sectionId,
        Guid rootId,
        string name,
        int foldersCount) =>
        new(
            [
                new LibraryHierarchySectionDto(
                    sectionId,
                    rootId,
                    name,
                    "Teal",
                    "BookOpenPageVariant",
                    foldersCount),
            ],
            1,
            false);

    private static LibraryContainerContentsDto CreateContents(
        Guid containerId,
        Guid sectionId,
        Guid? parentId,
        int depth,
        string name,
        int foldersCount) =>
        new(
            new LibraryContainerHeaderDto(
                containerId,
                sectionId,
                "C#",
                parentId,
                depth,
                name,
                "Teal",
                "Folder",
                DateTime.UtcNow,
                DateTime.UtcNow),
            new LibrarySectionHeaderDto(
                sectionId,
                "C#",
                "Teal",
                "BookOpenPageVariant",
                DateTime.UtcNow,
                DateTime.UtcNow),
            foldersCount,
            0,
            depth < 3);

    private static LibraryHierarchyFolderDto CreateFolder(
        Guid id,
        Guid sectionId,
        Guid parentId,
        int depth,
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

    private static LibraryHierarchyFoldersPageDto CreateNestedPage(
        GetLibraryHierarchyFoldersPageQuery query,
        ref int requestCount,
        Guid nestedFolderId,
        Guid sectionId,
        Guid parentId)
    {
        requestCount++;

        return new LibraryHierarchyFoldersPageDto(
            [CreateFolder(nestedFolderId, sectionId, parentId, 2, "Change Tracking", 0)],
            query.Offset + 1,
            false);
    }

    private sealed class StubQueryDispatcher(
        Func<object, object> handler)
        : IQueryDispatcher
    {
        public Task<Result<TResponse, Errors>> SendAsync<TQuery, TResponse>(
            TQuery query,
            CancellationToken cancellationToken = default)
            where TQuery : IQuery
        {
            cancellationToken.ThrowIfCancellationRequested();

            object response = handler(query);

            return Task.FromResult(
                Result.Success<TResponse, Errors>(
                    (TResponse)response));
        }
    }
}
