using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.GetHierarchyFoldersPage;
using Mnemora.Contracts;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class GetLibraryHierarchyFoldersPageQueryHandlerTests
{
    [Fact]
    public async Task Paging_31Folders_Returns30Then1()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        Section section = Section.Create(
            SectionName.Create("Section").Value,
            SectionColor.TEAL,
            SectionIcon.FOLDER);

        LibraryContainer root =
            db.AddSectionWithRoot(section);

        for (int index = 0; index < 31; index++)
        {
            db.Context.LibraryContainers.Add(
                LibraryContainer.CreateFolder(
                    root,
                    FolderName.Create($"Folder {index:D2}").Value,
                    FolderColor.TEAL,
                    FolderIcon.FOLDER).Value);
        }

        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var sut = CreateHandler(db);
        var first = await sut.Handle(Query(root.Id.Value, offset: 0));
        var second = await sut.Handle(Query(root.Id.Value, offset: 30));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.Items.Should().HaveCount(30);
        second.Value.Items.Should().ContainSingle();
        first.Value.HasMore.Should().BeTrue();
        second.Value.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task Page_ReturnsChildFolderCountWithoutMaterialCounters()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        Section section = Section.Create(
            SectionName.Create("Section").Value,
            SectionColor.TEAL,
            SectionIcon.FOLDER);

        LibraryContainer root =
            db.AddSectionWithRoot(section);

        LibraryContainer parent = LibraryContainer.CreateFolder(
            root,
            FolderName.Create("Parent").Value,
            FolderColor.TEAL,
            FolderIcon.FOLDER).Value;

        LibraryContainer child = LibraryContainer.CreateFolder(
            parent,
            FolderName.Create("Child").Value,
            FolderColor.TEAL,
            FolderIcon.FOLDER).Value;

        db.Context.LibraryContainers.AddRange(parent, child);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query(root.Id.Value));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].ChildFoldersCount.Should().Be(1);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
        db.CommandCounter.Count.Should().BeLessThanOrEqualTo(2);
    }

    private static GetLibraryHierarchyFoldersPageQueryHandler CreateHandler(
        SqliteLibraryTestDatabase db) =>
        new(
            db.Context,
            NullLogger<GetLibraryHierarchyFoldersPageQueryHandler>.Instance);

    private static GetLibraryHierarchyFoldersPageQuery Query(
        Guid containerId,
        int offset = 0,
        int pageSize = LibraryPagingDefaults.PAGE_SIZE) =>
        new(containerId, offset, pageSize);
}
