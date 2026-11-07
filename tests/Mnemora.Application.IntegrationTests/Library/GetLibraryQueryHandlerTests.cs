using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.Get;
using Mnemora.Contracts.Library;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class GetLibraryQueryHandlerTests
{
    [Fact]
    public async Task GetLibrary_IncludesRootMaterialsAndNestedFolders()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (Section section, LibraryContainer folder) =
            await db.CreateSectionAndFolderAsync("Section", "Level 1");

        LibraryContainer root = await db.Context.LibraryContainers.SingleAsync(
            container => container.SectionId == section.Id && container.ParentId == null);

        LibraryContainer nested = LibraryContainer.CreateFolder(
            folder,
            FolderName.Create("Level 2").Value,
            FolderColor.Teal,
            FolderIcon.Folder).Value;

        db.Context.LibraryContainers.Add(nested);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        Article rootArticle = db.CreateArticle(folder.Id, "At root");
        rootArticle.MoveToContainer(root.Id).IsSuccess.Should().BeTrue();
        Question linked = db.CreateLinkedQuestion(rootArticle, "Linked to root article");

        Question nestedQuestion = db.CreateStandaloneQuestion(folder.Id, "At depth 2");
        nestedQuestion.MoveToContainer(nested.Id).IsSuccess.Should().BeTrue();
        await db.AddMaterialsAsync(rootArticle, linked, nestedQuestion);

        var result = await CreateHandler(db).Handle(new GetLibraryQuery());

        result.IsSuccess.Should().BeTrue();
        LibrarySectionDto snapshot = result.Value.Single(item => item.Id == section.Id.Value);
        snapshot.Folders.Should().HaveCount(2);
        snapshot.Folders.Should().ContainSingle(item =>
            item.Id == folder.Id.Value && item.ParentId == root.Id.Value && item.Depth == 1);
        LibraryFolderSnapshotDto secondLevel = snapshot.Folders.Single(item => item.Id == nested.Id.Value);
        secondLevel.ParentId.Should().Be(folder.Id.Value);
        secondLevel.Depth.Should().Be(2);
        secondLevel.Materials.Should().ContainSingle(item => item.Id == nestedQuestion.Id.Value);
        snapshot.RootMaterials.Select(item => item.Id).Should().BeEquivalentTo(
            new[] { rootArticle.Id.Value, linked.Id.Value });
        snapshot.Folders.SelectMany(item => item.Materials)
            .Should().ContainSingle(item => item.Id == nestedQuestion.Id.Value);
    }

    [Fact]
    public async Task GetLibrary_DoesNotMixMaterialsBetweenSections()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        (Section first, LibraryContainer firstFolder) =
            await db.CreateSectionAndFolderAsync("First", "Folder");

        Section second = Section.Create(
            SectionName.Create("Second").Value,
            SectionColor.Teal,
            SectionIcon.Folder);
        LibraryContainer secondRoot = db.AddSectionWithRoot(second);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        Article firstArticle = db.CreateArticle(firstFolder.Id, "First material");
        Question secondQuestion = db.CreateStandaloneQuestion(secondRoot.Id, "Second material");
        await db.AddMaterialsAsync(firstArticle, secondQuestion);

        var result = await CreateHandler(db).Handle(new GetLibraryQuery());

        result.IsSuccess.Should().BeTrue();
        LibrarySectionDto firstSnapshot = result.Value.Single(item => item.Id == first.Id.Value);
        LibrarySectionDto secondSnapshot = result.Value.Single(item => item.Id == second.Id.Value);
        firstSnapshot.Folders.Should().ContainSingle();
        firstSnapshot.Folders[0].Materials.Should().ContainSingle(item => item.Id == firstArticle.Id.Value);
        firstSnapshot.RootMaterials.Should().BeEmpty();
        secondSnapshot.Folders.Should().BeEmpty();
        secondSnapshot.RootMaterials.Should().ContainSingle(item => item.Id == secondQuestion.Id.Value);
    }

    [Fact]
    public async Task GetLibrary_EmptySection_ReturnsEmptyFoldersAndMaterials()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        Section section = Section.Create(
            SectionName.Create("Empty").Value,
            SectionColor.Teal,
            SectionIcon.Folder);
        db.AddSectionWithRoot(section);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var result = await CreateHandler(db).Handle(new GetLibraryQuery());

        result.IsSuccess.Should().BeTrue();
        LibrarySectionDto snapshot = result.Value.Single();
        snapshot.Folders.Should().BeEmpty();
        snapshot.RootMaterials.Should().BeEmpty();
    }

    private static GetLibraryQueryHandler CreateHandler(SqliteLibraryTestDatabase db) =>
        new(db.Context, NullLogger<GetLibraryQueryHandler>.Instance);
}
