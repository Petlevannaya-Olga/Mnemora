using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.GetContainerContents;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class GetLibraryContainerContentsQueryHandlerTests
{
    [Fact]
    public async Task Root_ReturnsMetadataAndDirectCounts_NotChildCollection()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        (Section section, Topic topic) =
            await db.CreateSectionAndTopicAsync();

        LibraryContainer root =
            await db.Context.LibraryContainers.SingleAsync(
                container =>
                    container.SectionId == section.Id &&
                    container.ParentId == null);

        LibraryContainer topicFolder =
            await db.Context.LibraryContainers.SingleAsync(
                container =>
                    container.Id ==
                    LibraryContainerId.Create(topic.Id.Value).Value);

        LibraryContainer secondFolder =
            LibraryContainer.CreateFolder(
                root,
                FolderName.Create("Algorithms").Value,
                FolderColor.Teal,
                FolderIcon.Folder).Value;

        LibraryContainer nested =
            LibraryContainer.CreateFolder(
                topicFolder,
                FolderName.Create("Memory").Value,
                FolderColor.Teal,
                FolderIcon.Folder).Value;

        db.Context.LibraryContainers.AddRange(
            secondFolder,
            nested);

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        Article rootArticle =
            db.CreateArticle(topic.Id, "Root article");

        rootArticle.MoveToContainer(root.Id)
            .IsSuccess.Should().BeTrue();

        Question linked =
            db.CreateLinkedQuestion(
                rootArticle,
                "Linked question");

        Article nestedArticle =
            db.CreateArticle(topic.Id, "Nested article");

        nestedArticle.MoveToContainer(nested.Id)
            .IsSuccess.Should().BeTrue();

        db.Context.Materials.AddRange(
            rootArticle,
            linked,
            nestedArticle);

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var result = await sut.Handle(
            new GetLibraryContainerContentsQuery(root.Id.Value));

        result.IsSuccess.Should().BeTrue();
        result.Value.Container.Id.Should().Be(root.Id.Value);
        result.Value.Container.IsRoot.Should().BeTrue();
        result.Value.Container.Name.Should().Be(section.Name.Value);
        result.Value.Section.Id.Should().Be(section.Id.Value);
        result.Value.FoldersCount.Should().Be(2,
            "only immediate child folders belong to root contents");
        result.Value.MaterialsCount.Should().Be(1,
            "linked questions and nested-folder materials are not direct root materials");
        result.Value.CanCreateChildFolder.Should().BeTrue();
        db.CommandCounter.Count.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task ThirdLevelFolder_CannotCreateAnotherFolder()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        var (_, topic) =
            await db.CreateSectionAndTopicAsync();

        LibraryContainer level1 =
            await db.Context.LibraryContainers.SingleAsync(
                container =>
                    container.Id ==
                    LibraryContainerId.Create(topic.Id.Value).Value);

        LibraryContainer level2 =
            LibraryContainer.CreateFolder(
                level1,
                FolderName.Create("Level 2").Value,
                FolderColor.Teal,
                FolderIcon.Folder).Value;

        LibraryContainer level3 =
            LibraryContainer.CreateFolder(
                level2,
                FolderName.Create("Level 3").Value,
                FolderColor.Teal,
                FolderIcon.Folder).Value;

        db.Context.LibraryContainers.AddRange(
            level2,
            level3);

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var sut = CreateHandler(db);
        var result = await sut.Handle(
            new GetLibraryContainerContentsQuery(level3.Id.Value));

        result.IsSuccess.Should().BeTrue();
        result.Value.Container.Depth.Should().Be(
            LibraryContainer.MaxFolderDepth);
        result.Value.Container.IsFolder.Should().BeTrue();
        result.Value.CanCreateChildFolder.Should().BeFalse();
        result.Value.FoldersCount.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "LargeLoad")]
    public async Task RootWith50000Folders_MetadataQueryRemainsBounded()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        Section section = Section.Create(
            SectionName.Create("Section").Value,
            SectionColor.Teal,
            SectionIcon.Folder);

        LibraryContainer root =
            db.AddSectionWithRoot(section);

        for (int index = 0; index < 50_000; index++)
        {
            db.Context.LibraryContainers.Add(
                LibraryContainer.CreateFolder(
                    root,
                    FolderName.Create($"Folder {index:D5}").Value,
                    FolderColor.Teal,
                    FolderIcon.Folder).Value);
        }

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var result = await sut.Handle(
            new GetLibraryContainerContentsQuery(root.Id.Value));

        result.IsSuccess.Should().BeTrue();
        result.Value.FoldersCount.Should().Be(50_000);
        result.Value.MaterialsCount.Should().Be(0);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
        db.CommandCounter.Count.Should().BeLessThanOrEqualTo(3);
    }

    private static GetLibraryContainerContentsQueryHandler CreateHandler(
        SqliteLibraryTestDatabase db) =>
        new(
            db.Context,
            NullLogger<GetLibraryContainerContentsQueryHandler>.Instance);
}
