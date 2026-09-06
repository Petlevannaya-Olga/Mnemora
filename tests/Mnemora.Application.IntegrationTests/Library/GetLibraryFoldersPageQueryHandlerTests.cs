using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.GetFoldersPage;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class GetLibraryFoldersPageQueryHandlerTests
{
    [Fact]
    public async Task Paging_31Folders_Returns30Then1()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        (Section section, Topic topic) =
            await db.CreateSectionAndTopicAsync();

        LibraryContainer root =
            await GetRootAsync(db, section);

        // Тема уже дала одну depth-1 папку. Добавляем ещё 30.
        for (int index = 1; index < 31; index++)
        {
            LibraryContainer folder =
                LibraryContainer.CreateFolder(
                    root,
                    FolderName.Create($"Folder {index:D2}").Value,
                    FolderColor.Teal,
                    FolderIcon.Folder).Value;

            folder.ChangeDisplayOrder(index);
            db.Context.LibraryContainers.Add(folder);
        }

        LibraryContainer legacyFolder =
            await db.Context.LibraryContainers.SingleAsync(
                container =>
                    container.Id ==
                    LibraryContainerId.Create(topic.Id.Value).Value);

        legacyFolder.ChangeDisplayOrder(0);

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);

        var first = await sut.Handle(
            Query(root.Id.Value, offset: 0));
        var second = await sut.Handle(
            Query(root.Id.Value, offset: 30));

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
    public async Task Query_ReturnsOnlyImmediateChildren_AndDirectCounts()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        (Section section, Topic topic) =
            await db.CreateSectionAndTopicAsync();

        LibraryContainer root =
            await GetRootAsync(db, section);

        LibraryContainer level1 =
            await db.Context.LibraryContainers.SingleAsync(
                container =>
                    container.Id ==
                    LibraryContainerId.Create(topic.Id.Value).Value);

        LibraryContainer level2 =
            LibraryContainer.CreateFolder(
                level1,
                FolderName.Create("Memory").Value,
                FolderColor.Teal,
                FolderIcon.Folder).Value;

        LibraryContainer level3 =
            LibraryContainer.CreateFolder(
                level2,
                FolderName.Create("LOH").Value,
                FolderColor.Teal,
                FolderIcon.Folder).Value;

        db.Context.LibraryContainers.AddRange(
            level2,
            level3);

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        Article directArticle =
            db.CreateArticle(topic.Id, "Direct article");

        Question standalone =
            db.CreateStandaloneQuestion(topic.Id, "Direct question");

        Question linked =
            db.CreateLinkedQuestion(
                directArticle,
                "Linked question");

        Article nestedArticle =
            db.CreateArticle(topic.Id, "Nested article");

        nestedArticle
            .MoveToContainer(level2.Id)
            .IsSuccess
            .Should()
            .BeTrue();

        db.Context.Materials.AddRange(
            directArticle,
            standalone,
            linked,
            nestedArticle);

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var sut = CreateHandler(db);
        var result = await sut.Handle(
            Query(root.Id.Value));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();

        LibraryFolderDto folder =
            result.Value.Items.Single();

        folder.Id.Should().Be(level1.Id.Value);
        folder.ChildFoldersCount.Should().Be(1);
        folder.MaterialsCount.Should().Be(2,
            "linked questions are not top-level materials");
        folder.CanCreateChildFolder.Should().BeTrue();

        result.Value.Items.Should().NotContain(
            item => item.Id == level2.Id.Value);
        result.Value.Items.Should().NotContain(
            item => item.Id == level3.Id.Value);
    }

    [Fact]
    public async Task Search_IsCaseInsensitiveAndRunsBeforePaging()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        (Section section, _) =
            await db.CreateSectionAndTopicAsync(
                topicName: "CLR");

        LibraryContainer root =
            await GetRootAsync(db, section);

        for (int index = 0; index < 40; index++)
        {
            db.Context.LibraryContainers.Add(
                LibraryContainer.CreateFolder(
                    root,
                    FolderName.Create($"A Folder {index:D2}").Value,
                    FolderColor.Teal,
                    FolderIcon.Folder).Value);
        }

        LibraryContainer target =
            LibraryContainer.CreateFolder(
                root,
                FolderName.Create("ZZZ Memory Model").Value,
                FolderColor.Teal,
                FolderIcon.Folder).Value;

        db.Context.LibraryContainers.Add(target);

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var sut = CreateHandler(db);
        var result = await sut.Handle(
            new GetLibraryFoldersPageQuery(
                root.Id.Value,
                "zzz MEMORY model",
                LibraryFolderSort.Name,
                Offset: 0,
                PageSize: 30));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Id.Should().Be(target.Id.Value);
        result.Value.TotalCount.Should().Be(1);
        result.Value.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task DeepOffset_5000Folders_ReturnsOnlyRequestedWindow()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        Section section = Section.Create(
            SectionName.Create("Section").Value,
            SectionColor.Teal,
            SectionIcon.Folder);

        LibraryContainer root =
            db.AddSectionWithRoot(section);

        for (int index = 0; index < 5_000; index++)
        {
            LibraryContainer folder =
                LibraryContainer.CreateFolder(
                    root,
                    FolderName.Create($"Folder {index:D5}").Value,
                    FolderColor.Teal,
                    FolderIcon.Folder).Value;

            folder.ChangeDisplayOrder(index);
            db.Context.LibraryContainers.Add(folder);
        }

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var result = await sut.Handle(
            Query(
                root.Id.Value,
                offset: 4_980));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(20);
        result.Value.Items.First().Name.Should().Be("Folder 04980");
        result.Value.Items.Last().Name.Should().Be("Folder 04999");
        result.Value.TotalCount.Should().Be(5_000);
        result.Value.HasMore.Should().BeFalse();
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
        db.CommandCounter.Count.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    [Trait("Category", "LargeLoad")]
    public async Task LargeLoad_50000Folders_DeepPageRemainsBounded()
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
            LibraryContainer folder =
                LibraryContainer.CreateFolder(
                    root,
                    FolderName.Create($"Folder {index:D5}").Value,
                    FolderColor.Teal,
                    FolderIcon.Folder).Value;

            folder.ChangeDisplayOrder(index);
            db.Context.LibraryContainers.Add(folder);
        }

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var result = await sut.Handle(
            Query(
                root.Id.Value,
                offset: 49_950));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(30);
        result.Value.TotalCount.Should().Be(50_000);
        result.Value.NextOffset.Should().Be(49_980);
        result.Value.HasMore.Should().BeTrue();
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
        db.CommandCounter.Count.Should().BeLessThanOrEqualTo(5);
    }

    private static GetLibraryFoldersPageQueryHandler CreateHandler(
        SqliteLibraryTestDatabase db) =>
        new(
            db.Context,
            NullLogger<GetLibraryFoldersPageQueryHandler>.Instance);

    private static GetLibraryFoldersPageQuery Query(
        Guid containerId,
        int offset = 0,
        int pageSize = LibraryPagingDefaults.PageSize) =>
        new(
            containerId,
            Search: null,
            LibraryFolderSort.Custom,
            offset,
            pageSize);

    private static Task<LibraryContainer> GetRootAsync(
        SqliteLibraryTestDatabase db,
        Section section) =>
        db.Context.LibraryContainers.SingleAsync(
            container =>
                container.SectionId == section.Id &&
                container.ParentId == null);
}
