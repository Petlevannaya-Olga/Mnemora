using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.GetSectionsPage;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class GetLibrarySectionsPageQueryHandlerTests
{
    [Fact]
    public async Task Paging_31Sections_Returns30Then1AndRootIds()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        for (int index = 0; index < 31; index++)
        {
            Section section = Section.Create(
                SectionName.Create($"Section {index:D2}").Value,
                Enum.GetValues<SectionColor>()[0],
                Enum.GetValues<SectionIcon>()[0]);

            db.AddSectionWithRoot(section);
        }

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var sut = CreateHandler(db);
        var first = await sut.Handle(Query(offset: 0));
        var second = await sut.Handle(Query(offset: 30));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.Items.Should().HaveCount(30);
        second.Value.Items.Should().ContainSingle();
        first.Value.TotalCount.Should().Be(31);
        second.Value.TotalCount.Should().Be(31);
        first.Value.HasMore.Should().BeTrue();
        second.Value.HasMore.Should().BeFalse();
        first.Value.Items.Should().OnlyContain(
            item => item.RootContainerId != Guid.Empty);
    }

    [Fact]
    public async Task SectionCounters_UseContainersIncludingRootAndNestedFolders()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        (Section section, LibraryContainer folder) =
            await db.CreateSectionAndFolderAsync();

        LibraryContainer root =
            await db.Context.LibraryContainers.SingleAsync(
                container =>
                    container.SectionId == section.Id &&
                    container.ParentId == null);

        LibraryContainer level1 =
            await db.Context.LibraryContainers.SingleAsync(
                container =>
                    container.Id ==
                    folder.Id);

        LibraryContainer level2 =
            LibraryContainer.CreateFolder(
                level1,
                FolderName.Create("Nested").Value,
                FolderColor.TEAL,
                FolderIcon.FOLDER).Value;

        db.Context.LibraryContainers.Add(level2);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        Article rootArticle =
            db.CreateArticle(folder.Id, "Root article");
        rootArticle.MoveToContainer(root.Id)
            .IsSuccess.Should().BeTrue();

        Question nestedStandalone =
            db.CreateStandaloneQuestion(folder.Id, "Nested standalone");
        nestedStandalone.MoveToContainer(level2.Id)
            .IsSuccess.Should().BeTrue();

        Question linked =
            db.CreateLinkedQuestion(
                rootArticle,
                "Linked");

        await db.AddMaterialsAsync(
            rootArticle,
            nestedStandalone,
            linked);

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query());

        result.IsSuccess.Should().BeTrue();
        LibrarySectionOverviewDto item =
            result.Value.Items.Single(x => x.Id == section.Id.Value);

        item.RootContainerId.Should().Be(root.Id.Value);
        item.FoldersCount.Should().Be(2);
        item.RootFoldersCount.Should().Be(1,
            "only first-level folders belong to the root-folder page");
        item.ArticlesCount.Should().Be(1);
        item.QuestionsCount.Should().Be(1);
        item.MaterialsCount.Should().Be(2);
    }

    [Fact]
    public async Task SectionCounters_UseContainerSection()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        (Section sourceSection, LibraryContainer sourceFolder) =
            await db.CreateSectionAndFolderAsync(
                sectionName: "Source",
                folderName: "Nested folder");

        Section targetSection = Section.Create(
            SectionName.Create("Target").Value,
            SectionColor.TEAL,
            SectionIcon.FOLDER);

        LibraryContainer targetRoot =
            db.AddSectionWithRoot(targetSection);

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        Article movedArticle =
            db.CreateArticle(sourceFolder.Id, "Moved article");

        movedArticle.MoveToContainer(targetRoot.Id)
            .IsSuccess.Should().BeTrue();

        await db.AddMaterialsAsync(movedArticle);

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query());

        result.IsSuccess.Should().BeTrue();

        LibrarySectionOverviewDto source =
            result.Value.Items.Single(item =>
                item.Id == sourceSection.Id.Value);

        LibrarySectionOverviewDto target =
            result.Value.Items.Single(item =>
                item.Id == targetSection.Id.Value);

        source.MaterialsCount.Should().Be(0);
        source.ArticlesCount.Should().Be(0);
        target.MaterialsCount.Should().Be(1);
        target.ArticlesCount.Should().Be(1);
        target.RootContainerId.Should().Be(targetRoot.Id.Value);
    }

    [Fact]
    [Trait("Category", "LargeLoad")]
    public async Task SectionCounters_50000Folders_AreAggregatedInDatabase()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        Section section = Section.Create(
            SectionName.Create("Section").Value,
            SectionColor.TEAL,
            SectionIcon.FOLDER);

        LibraryContainer root =
            db.AddSectionWithRoot(section);

        for (int index = 0; index < 50_000; index++)
        {
            db.Context.LibraryContainers.Add(
                LibraryContainer.CreateFolder(
                    root,
                    FolderName.Create($"Folder {index:D5}").Value,
                    FolderColor.TEAL,
                    FolderIcon.FOLDER).Value);
        }

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query());

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].FoldersCount.Should().Be(50_000);
        result.Value.Items[0].RootContainerId.Should().Be(root.Id.Value);
        result.Value.TotalCount.Should().Be(1);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
        db.CommandCounter.Count.Should().BeLessThanOrEqualTo(7);
    }

    private static GetLibrarySectionsPageQueryHandler CreateHandler(
        SqliteLibraryTestDatabase db) =>
        new(
            db.Context,
            NullLogger<GetLibrarySectionsPageQueryHandler>.Instance);

    private static GetLibrarySectionsPageQuery Query(
        int offset = 0,
        int pageSize = LibraryPagingDefaults.PAGE_SIZE) =>
        new(
            Search: null,
            LibrarySectionSort.NAME,
            offset,
            pageSize);
}
