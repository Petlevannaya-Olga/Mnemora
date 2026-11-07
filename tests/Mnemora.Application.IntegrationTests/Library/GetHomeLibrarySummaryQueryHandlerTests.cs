using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Database;
using Mnemora.Application.Home.GetLibrarySummary;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class GetHomeLibrarySummaryQueryHandlerTests
{
    [Fact]
    public async Task EmptyLibrary_ReturnsZeroCountsAndNoSuggestedSection()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();

        var sut = CreateHandler(db.Context);
        var result = await sut.Handle(new GetHomeLibrarySummaryQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.SectionsCount.Should().Be(0);
        result.Value.FoldersCount.Should().Be(0);
        result.Value.SuggestedSection.Should().BeNull();
        db.CommandCounter.Count.Should().Be(1);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task Summary_CountsFoldersWithoutLegacyTopicRecords()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        Section section = CreateSection("Containers only");
        LibraryContainer root = db.AddSectionWithRoot(section);
        LibraryContainer folder = LibraryContainer.CreateFolder(
            root,
            FolderName.Create("C#").Value,
            FolderColor.Teal,
            FolderIcon.Bookmark).Value;
        db.Context.LibraryContainers.Add(folder);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var result = await CreateHandler(db.Context)
            .Handle(new GetHomeLibrarySummaryQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.SectionsCount.Should().Be(1);
        result.Value.FoldersCount.Should().Be(1);
        result.Value.SuggestedSection!.Id.Should().Be(section.Id.Value);
    }

    [Fact]
    public async Task Summary_CountsAllNestedFoldersWithoutCountingSectionRoot()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (_, LibraryContainer firstLevelFolder) =
            await db.CreateSectionAndFolderAsync();

        LibraryContainer nestedFolder = LibraryContainer.CreateFolder(
            firstLevelFolder,
            FolderName.Create("Nested folder").Value,
            FolderColor.Teal,
            FolderIcon.Bookmark).Value;

        db.Context.LibraryContainers.Add(nestedFolder);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var result = await CreateHandler(db.Context)
            .Handle(new GetHomeLibrarySummaryQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.SectionsCount.Should().Be(1);
        result.Value.FoldersCount.Should().Be(2,
            "the section root is not a folder, but nested folders are counted");
    }

    [Fact]
    public async Task SectionWithoutFolders_IsPreferredForSuggestedSection()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        await db.CreateSectionAndFolderAsync(
            sectionName: "Populated section",
            folderName: "Folder");

        Section emptySection = CreateSection("Empty section");
        db.Context.Sections.Add(emptySection);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db.Context);
        var result = await sut.Handle(new GetHomeLibrarySummaryQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.SectionsCount.Should().Be(2);
        result.Value.FoldersCount.Should().Be(1);
        var suggestedSection = result.Value.SuggestedSection;
        suggestedSection.Should().NotBeNull();
        suggestedSection!.Id.Should().Be(emptySection.Id.Value);
        suggestedSection!.Name.Should().Be(emptySection.Name.Value);
        db.CommandCounter.Count.Should().Be(3);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task AllSectionsHaveFolders_FallsBackToFirstSection()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (Section section, _) = await db.CreateSectionAndFolderAsync();

        var sut = CreateHandler(db.Context);
        var result = await sut.Handle(new GetHomeLibrarySummaryQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.SectionsCount.Should().Be(1);
        result.Value.FoldersCount.Should().Be(1);
        result.Value.SuggestedSection.Should().NotBeNull();
        result.Value.SuggestedSection!.Id.Should().Be(section.Id.Value);
        db.CommandCounter.Count.Should().Be(4);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task Summary_DoesNotAccessMaterialsQuery()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        await db.CreateSectionAndFolderAsync();

        var guardedContext = new MaterialsForbiddenReadDbContext(db.Context);
        var sut = CreateHandler(guardedContext);

        var result = await sut.Handle(new GetHomeLibrarySummaryQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.SectionsCount.Should().Be(1);
        result.Value.FoldersCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "LargeLoad")]
    public async Task LargeLoad_50000SectionsAndFolders_RemainsAggregateBounded()
    {
        const int targetCount = 50_000;
        const int batchSize = 2_000;

        await using var db = await SqliteLibraryTestDatabase.CreateAsync();

        SectionId? hotSectionId = null;

        for (int start = 0; start < targetCount; start += batchSize)
        {
            int end = Math.Min(start + batchSize, targetCount);
            var batch = new List<Section>(end - start);

            for (int index = start; index < end; index++)
            {
                Section section = CreateSection($"Section {index:D5}");
                hotSectionId ??= section.Id;
                batch.Add(section);
            }

            db.Context.Sections.AddRange(batch);
            await db.Context.SaveChangesAsync();
            db.Context.ChangeTracker.Clear();
        }

        hotSectionId.Should().NotBeNull();

        LibraryContainer root = LibraryContainer.CreateRoot(hotSectionId!).Value;
        db.Context.LibraryContainers.Add(root);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        for (int start = 0; start < targetCount; start += batchSize)
        {
            int end = Math.Min(start + batchSize, targetCount);
            var batch = new List<LibraryContainer>(end - start);

            for (int index = start; index < end; index++)
            {
                batch.Add(LibraryContainer.CreateFolder(
                    root,
                    FolderName.Create($"Folder {index:D5}").Value,
                    FolderColor.Teal,
                    FolderIcon.Bookmark).Value);
            }

            db.Context.LibraryContainers.AddRange(batch);
            await db.Context.SaveChangesAsync();
            db.Context.ChangeTracker.Clear();
        }

        db.CommandCounter.Reset();

        var guardedContext = new MaterialsForbiddenReadDbContext(db.Context);
        var sut = CreateHandler(guardedContext);
        var result = await sut.Handle(new GetHomeLibrarySummaryQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.SectionsCount.Should().Be(targetCount);
        result.Value.FoldersCount.Should().Be(targetCount);
        result.Value.SuggestedSection.Should().NotBeNull();
        result.Value.SuggestedSection!.Id.Should().NotBe(hotSectionId!.Value);
        db.CommandCounter.Count.Should().Be(3);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
    }

    private static GetHomeLibrarySummaryQueryHandler CreateHandler(
        IReadDbContext readDbContext) =>
        new(
            readDbContext,
            NullLogger<GetHomeLibrarySummaryQueryHandler>.Instance);

    private static Section CreateSection(string name) =>
        Section.Create(
            SectionName.Create(name).Value,
            Enum.GetValues<SectionColor>()[0],
            Enum.GetValues<SectionIcon>()[0]);

    private sealed class MaterialsForbiddenReadDbContext(
        IReadDbContext inner) : IReadDbContext
    {
        public IQueryable<Section> SectionsRead => inner.SectionsRead;

        public IQueryable<LibraryContainer> LibraryContainersRead =>
            inner.LibraryContainersRead;

        public IQueryable<Material> MaterialsRead =>
            throw new InvalidOperationException(
                "Главная страница не должна обращаться к материалам.");
    }
}
