using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.GetHierarchySectionsPage;
using Mnemora.Contracts;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class GetLibraryHierarchySectionsPageQueryHandlerTests
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
                SectionColor.Teal,
                SectionIcon.Folder);

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
        first.Value.HasMore.Should().BeTrue();
        second.Value.HasMore.Should().BeFalse();
        first.Value.Items.Should().OnlyContain(
            item => item.RootContainerId != Guid.Empty);
    }

    [Fact]
    public async Task Page_ReturnsOnlyDirectFolderCountForTreeExpansion()
    {
        await using var db =
            await SqliteLibraryTestDatabase.CreateAsync();

        Section section = Section.Create(
            SectionName.Create("Section").Value,
            SectionColor.Teal,
            SectionIcon.Folder);

        LibraryContainer root =
            db.AddSectionWithRoot(section);

        LibraryContainer first = LibraryContainer.CreateFolder(
            root,
            FolderName.Create("First").Value,
            FolderColor.Teal,
            FolderIcon.Folder).Value;

        LibraryContainer second = LibraryContainer.CreateFolder(
            root,
            FolderName.Create("Second").Value,
            FolderColor.Teal,
            FolderIcon.Folder).Value;

        LibraryContainer nested = LibraryContainer.CreateFolder(
            first,
            FolderName.Create("Nested").Value,
            FolderColor.Teal,
            FolderIcon.Folder).Value;

        db.Context.LibraryContainers.AddRange(first, second, nested);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        db.CommandCounter.Reset();

        var sut = CreateHandler(db);
        var result = await sut.Handle(Query());

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].ChildFoldersCount.Should().Be(2);
        db.Context.ChangeTracker.Entries().Should().BeEmpty();
        db.CommandCounter.Count.Should().BeLessThanOrEqualTo(3);
    }

    private static GetLibraryHierarchySectionsPageQueryHandler CreateHandler(
        SqliteLibraryTestDatabase db) =>
        new(
            db.Context,
            NullLogger<GetLibraryHierarchySectionsPageQueryHandler>.Instance);

    private static GetLibraryHierarchySectionsPageQuery Query(
        int offset = 0,
        int pageSize = LibraryPagingDefaults.PageSize) =>
        new(offset, pageSize);
}
