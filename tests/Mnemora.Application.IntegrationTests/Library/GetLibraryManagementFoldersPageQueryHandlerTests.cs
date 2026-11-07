using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.GetManagementFoldersPage;
using Mnemora.Contracts;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

// Query and fixtures use LibraryContainer directly.
public sealed class GetLibraryManagementFoldersPageQueryHandlerTests
{
    [Fact]
    public async Task Paging_31Folders_Returns30Then1()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (Section section, LibraryContainer root, _) =
            await CreateSectionAndFolderAsync(db, "Folder 00000");

        await AddFoldersAsync(db, root, 1, 31);

        var sut = CreateHandler(db);
        var first = await sut.Handle(Query(section.Id.Value, offset: 0));
        var second = await sut.Handle(Query(section.Id.Value, offset: 30));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.Items.Should().HaveCount(30);
        second.Value.Items.Should().ContainSingle();
        first.Value.TotalCount.Should().Be(31);
        second.Value.TotalCount.Should().Be(31);
        first.Value.HasMore.Should().BeTrue();
        second.Value.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task FirstLevelFolders_ExcludeNestedFolders()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (Section section, _, LibraryContainer folder) =
            await CreateSectionAndFolderAsync(db, "Parent");

        LibraryContainer nested = LibraryContainer.CreateFolder(
            folder,
            FolderName.Create("Nested").Value,
            FolderColor.TEAL,
            FolderIcon.FOLDER).Value;

        db.Context.LibraryContainers.Add(nested);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var result = await CreateHandler(db).Handle(Query(section.Id.Value));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Id.Should().Be(folder.Id.Value);
    }

    [Fact]
    public async Task FolderCounters_ExcludeLinkedQuestionsFromTopLevelMaterials()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (Section section, _, LibraryContainer folder) =
            await CreateSectionAndFolderAsync(db);

        Article article = CreateArticle(folder.Id, "Article");
        Question[] standalone = Enumerable.Range(0, 2)
            .Select(index => CreateStandaloneQuestion(
                folder.Id,
                $"Standalone {index}"))
            .ToArray();
        Question[] linked = Enumerable.Range(0, 10)
            .Select(index => db.CreateLinkedQuestion(
                article,
                $"Linked {index}"))
            .ToArray();

        await db.AddMaterialsAsync(
            new Material[] { article }
                .Concat(standalone)
                .Concat(linked)
                .ToArray());

        var result = await CreateHandler(db).Handle(Query(section.Id.Value));

        result.IsSuccess.Should().BeTrue();
        var item = result.Value.Items.Single(x => x.Id == folder.Id.Value);
        item.ArticlesCount.Should().Be(1);
        item.QuestionsCount.Should().Be(2);
        item.MaterialsCount.Should().Be(3);
    }

    [Fact]
    public async Task Search_IsCaseInsensitiveAndRunsBeforePaging()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (Section section, LibraryContainer root, _) =
            await CreateSectionAndFolderAsync(db, "A Folder 00000");

        // More than one page sorts before the target: filtering must precede paging.
        await AddFoldersAsync(db, root, 1, 40, prefix: "A Folder");

        LibraryContainer target = LibraryContainer.CreateFolder(
            root,
            FolderName.Create("ZZZ EF Core").Value,
            FolderColor.TEAL,
            FolderIcon.FOLDER).Value;
        db.Context.LibraryContainers.Add(target);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var result = await CreateHandler(db).Handle(Query(
            section.Id.Value,
            search: "zzz ef CORE",
            offset: 0,
            pageSize: 30));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Id.Should().Be(target.Id.Value);
        result.Value.Items[0].Name.Should().Be("ZZZ EF Core");
        result.Value.TotalCount.Should().Be(1);
        result.Value.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task FolderWithoutMaterials_UsesFolderUpdatedAtAsLastActivity()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (Section section, _, LibraryContainer folder) =
            await CreateSectionAndFolderAsync(db, "Empty folder");

        var result = await CreateHandler(db).Handle(Query(section.Id.Value));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();

        var item = result.Value.Items.Single();
        item.Id.Should().Be(folder.Id.Value);
        item.MaterialsCount.Should().Be(0);
        item.ArticlesCount.Should().Be(0);
        item.QuestionsCount.Should().Be(0);
        item.LastActivityAt.Should().Be(folder.UpdatedAt);
    }

    [Fact]
    public async Task DeepOffset_5000Folders_ReturnsOnlyRequestedPage()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (Section section, LibraryContainer root, _) =
            await CreateSectionAndFolderAsync(db, "Folder 00000");

        await AddFoldersAsync(db, root, 1, 5_000);

        var result = await CreateHandler(db).Handle(Query(
            section.Id.Value,
            offset: 4_950));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(30);
        result.Value.Items.Select(item => item.Name).Should().Equal(
            Enumerable.Range(4_950, 30)
                .Select(index => $"Folder {index:D5}"));
        result.Value.TotalCount.Should().Be(5_000);
        result.Value.NextOffset.Should().Be(4_980);
        result.Value.HasMore.Should().BeTrue();
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "LargeLoad")]
    public async Task LargeLoad_50000Folders_DeepPageRemainsBounded()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (Section section, LibraryContainer root, _) =
            await CreateSectionAndFolderAsync(db, "Folder 00000");

        await AddFoldersAsync(db, root, 1, 50_000);

        var result = await CreateHandler(db).Handle(Query(
            section.Id.Value,
            offset: 49_950));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(30);
        result.Value.Items.Select(item => item.Name).Should().Equal(
            Enumerable.Range(49_950, 30)
                .Select(index => $"Folder {index:D5}"));
        result.Value.TotalCount.Should().Be(50_000);
        result.Value.NextOffset.Should().Be(49_980);
        result.Value.HasMore.Should().BeTrue();
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    private static async Task<(Section Section, LibraryContainer Root, LibraryContainer Folder)>
        CreateSectionAndFolderAsync(
            SqliteLibraryTestDatabase db,
            string folderName = "Folder")
    {
        Section section = Section.Create(
            SectionName.Create("Section").Value,
            SectionColor.TEAL,
            SectionIcon.FOLDER);

        LibraryContainer root = db.AddSectionWithRoot(section);
        LibraryContainer folder = LibraryContainer.CreateFolder(
            root,
            FolderName.Create(folderName).Value,
            FolderColor.TEAL,
            FolderIcon.FOLDER).Value;

        db.Context.LibraryContainers.Add(folder);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();
        return (section, root, folder);
    }

    private static async Task AddFoldersAsync(
        SqliteLibraryTestDatabase db,
        LibraryContainer root,
        int startIndex,
        int exclusiveEnd,
        string prefix = "Folder")
    {
        const int BATCH_SIZE = 1_000;
        for (int index = startIndex; index < exclusiveEnd; index++)
        {
            LibraryContainer folder = LibraryContainer.CreateFolder(
                root,
                FolderName.Create($"{prefix} {index:D5}").Value,
                FolderColor.TEAL,
                FolderIcon.FOLDER).Value;
            db.Context.LibraryContainers.Add(folder);

            if ((index - startIndex + 1) % BATCH_SIZE == 0)
            {
                await db.Context.SaveChangesAsync();
                db.Context.ChangeTracker.Clear();
            }
        }

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();
    }

    private static Article CreateArticle(LibraryContainerId containerId, string title) =>
        Article.CreateInContainer(
            containerId,
            MaterialTitle.Create(title).Value,
            MaterialDifficulty.MEDIUM,
            MaterialIcon.DefaultArticle,
            MaterialExperienceRewards.Create(50, 20).Value,
            Array.Empty<MaterialTag>()).Value;

    private static Question CreateStandaloneQuestion(
        LibraryContainerId containerId,
        string title) =>
        Question.CreateStandaloneInContainer(
            containerId,
            MaterialTitle.Create(title).Value,
            MaterialDifficulty.MEDIUM,
            MaterialIcon.DefaultQuestion,
            MaterialExperienceRewards.Create(50, 20).Value,
            Array.Empty<MaterialTag>()).Value;

    private static GetLibraryManagementFoldersPageQueryHandler CreateHandler(
        SqliteLibraryTestDatabase db) =>
        new(
            db.Context,
            NullLogger<GetLibraryManagementFoldersPageQueryHandler>.Instance);

    private static GetLibraryManagementFoldersPageQuery Query(
        Guid sectionId,
        string? search = null,
        int offset = 0,
        int pageSize = LibraryPagingDefaults.PAGE_SIZE) =>
        new(
            sectionId,
            search,
            LibraryManagementFolderPageSort.NAME,
            offset,
            pageSize);
}
