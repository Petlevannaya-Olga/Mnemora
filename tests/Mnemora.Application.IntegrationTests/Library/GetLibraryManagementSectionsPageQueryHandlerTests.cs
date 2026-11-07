using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.GetManagementSectionsPage;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class GetLibraryManagementSectionsPageQueryHandlerTests
{
    [Fact]
    public async Task Paging_31Sections_Returns30Then1()
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
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var first = await sut.Handle(Query(offset: 0));
        var second = await sut.Handle(Query(offset: 30));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.Items.Should().HaveCount(30);
        second.Value.Items.Should().ContainSingle();
        first.Value.TotalCount.Should().Be(31);
        first.Value.HasMore.Should().BeTrue();
        second.Value.HasMore.Should().BeFalse();
        first.Value.Items.Should().OnlyContain(
            item => item.RootContainerId != Guid.Empty);
    }

    [Fact]
    public async Task SectionCounters_UseContainerHierarchy()
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

        LibraryContainer nested =
            LibraryContainer.CreateFolder(
                level1,
                FolderName.Create("Nested").Value,
                FolderColor.TEAL,
                FolderIcon.FOLDER).Value;

        db.Context.LibraryContainers.Add(nested);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        Article article =
            db.CreateArticle(folder.Id, "Article");
        article.MoveToContainer(root.Id)
            .IsSuccess.Should().BeTrue();

        Question standalone =
            db.CreateStandaloneQuestion(folder.Id, "Standalone");
        standalone.MoveToContainer(nested.Id)
            .IsSuccess.Should().BeTrue();

        Question[] linked = Enumerable.Range(0, 20)
            .Select(index =>
                db.CreateLinkedQuestion(
                    article,
                    $"Linked {index}"))
            .ToArray();

        await db.AddMaterialsAsync(
            new Material[] { article, standalone }
                .Concat(linked)
                .ToArray());

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query());

        result.IsSuccess.Should().BeTrue();
        LibrarySectionOverviewDto item =
            result.Value.Items.Single(x => x.Id == section.Id.Value);

        item.RootContainerId.Should().Be(root.Id.Value);
        item.FoldersCount.Should().Be(2);
        item.RootFoldersCount.Should().Be(1);
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
    public async Task NegativeOffsetAndOversizedPage_AreNormalized()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        for (int index = 0; index < 120; index++)
        {
            Section section = Section.Create(
                SectionName.Create($"Section {index:D3}").Value,
                Enum.GetValues<SectionColor>()[0],
                Enum.GetValues<SectionIcon>()[0]);

            db.AddSectionWithRoot(section);
        }

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var sut = CreateHandler(db);
        var result = await sut.Handle(
            new GetLibraryManagementSectionsPageQuery(
                Search: null,
                LibraryManagementSectionSort.NAME,
                Offset: -10,
                PageSize: 1_000));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(
            LibraryPagingDefaults.MAX_QUERY_PAGE_SIZE);
        result.Value.NextOffset.Should().Be(
            LibraryPagingDefaults.MAX_QUERY_PAGE_SIZE);
        result.Value.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task Search_IsCaseInsensitiveAndRunsBeforePaging()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        for (int index = 0; index < 40; index++)
        {
            Section section = Section.Create(
                SectionName.Create($"A Section {index:D2}").Value,
                Enum.GetValues<SectionColor>()[0],
                Enum.GetValues<SectionIcon>()[0]);

            db.AddSectionWithRoot(section);
        }

        Section target = Section.Create(
            SectionName.Create("ZZZ Backend").Value,
            Enum.GetValues<SectionColor>()[0],
            Enum.GetValues<SectionIcon>()[0]);

        LibraryContainer targetRoot =
            db.AddSectionWithRoot(target);

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var sut = CreateHandler(db);
        var result = await sut.Handle(
            new GetLibraryManagementSectionsPageQuery(
                Search: "zzz BACKEND",
                LibraryManagementSectionSort.NAME,
                Offset: 0,
                PageSize: 30));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Id.Should().Be(target.Id.Value);
        result.Value.Items[0].RootContainerId.Should().Be(
            targetRoot.Id.Value);
        result.Value.TotalCount.Should().Be(1);
        result.Value.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task DeepOffset_5000Sections_ReturnsOnlyRequestedPage()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        for (int index = 0; index < 5_000; index++)
        {
            Section section = Section.Create(
                SectionName.Create($"Section {index:D5}").Value,
                Enum.GetValues<SectionColor>()[0],
                Enum.GetValues<SectionIcon>()[0]);

            db.AddSectionWithRoot(section);
        }

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var result = await sut.Handle(
            Query(offset: 4_950));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(30);
        result.Value.TotalCount.Should().Be(5_000);
        result.Value.NextOffset.Should().Be(4_980);
        result.Value.HasMore.Should().BeTrue();
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "LargeLoad")]
    public async Task LargeLoad_50000Sections_DeepPageRemainsBounded()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        for (int index = 0; index < 50_000; index++)
        {
            Section section = Section.Create(
                SectionName.Create($"Section {index:D5}").Value,
                Enum.GetValues<SectionColor>()[0],
                Enum.GetValues<SectionIcon>()[0]);

            db.AddSectionWithRoot(section);
        }

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var result = await sut.Handle(
            Query(offset: 49_950));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(30);
        result.Value.TotalCount.Should().Be(50_000);
        result.Value.NextOffset.Should().Be(49_980);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
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
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
        db.CommandCounter.Count.Should().BeLessThanOrEqualTo(7);
    }

    private static GetLibraryManagementSectionsPageQueryHandler CreateHandler(
        SqliteLibraryTestDatabase db) =>
        new(
            db.Context,
            NullLogger<GetLibraryManagementSectionsPageQueryHandler>.Instance);

    private static GetLibraryManagementSectionsPageQuery Query(
        int offset = 0,
        int pageSize = LibraryPagingDefaults.PAGE_SIZE) =>
        new(
            Search: null,
            LibraryManagementSectionSort.NAME,
            offset,
            pageSize);
}
