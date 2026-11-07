using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mnemora.Application.Library.Order;
using Mnemora.Domain.LibraryContainers;
using Xunit;

namespace Mnemora.Application.IntegrationTests.Library;

public sealed class LibraryOrderFoldersQueryHandlerTests
{
    [Fact]
    public async Task OrderScopes_ContainOnlyFirstLevelFolders()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        var (section, folder) = await db.CreateSectionAndFolderAsync();
        LibraryContainer nestedFolder = LibraryContainer.CreateFolder(
            folder,
            FolderName.Create("Nested").Value,
            FolderColor.TEAL,
            FolderIcon.FOLDER).Value;
        db.Context.LibraryContainers.Add(nestedFolder);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var handler = new GetLibraryOrderScopesQueryHandler(
            db.Context,
            NullLogger<GetLibraryOrderScopesQueryHandler>.Instance);

        var result = await handler.Handle(new GetLibraryOrderScopesQuery());

        Assert.True(result.IsSuccess);
        var sectionScope = Assert.Single(result.Value.Sections);
        Assert.Equal(section.Id.Value, sectionScope.Id);
        var firstLevelFolder = Assert.Single(sectionScope.Folders);
        Assert.Equal(folder.Id.Value, firstLevelFolder.Id);
    }

    [Fact]
    public async Task FolderOrderingItems_ExcludeRootAndNestedFolders()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        var (section, folder) = await db.CreateSectionAndFolderAsync();
        db.Context.LibraryContainers.Add(LibraryContainer.CreateFolder(
            folder,
            FolderName.Create("Nested").Value,
            FolderColor.TEAL,
            FolderIcon.FOLDER).Value);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();

        var handler = new GetLibraryOrderItemsQueryHandler(
            db.Context,
            NullLogger<GetLibraryOrderItemsQueryHandler>.Instance);
        var result = await handler.Handle(
            new GetLibraryOrderItemsQuery(LibraryOrderTarget.FOLDERS, section.Id.Value));

        Assert.True(result.IsSuccess);
        Assert.Equal(folder.Id.Value, Assert.Single(result.Value).Id);
    }

    [Fact]
    public async Task SectionOrderCount_UsesFolderTerminology()
    {
        await using var db = await SqliteLibraryTestDatabase.CreateAsync();
        await db.CreateSectionAndFolderAsync();
        var handler = new GetLibraryOrderItemsQueryHandler(
            db.Context,
            NullLogger<GetLibraryOrderItemsQueryHandler>.Instance);
        var result = await handler.Handle(
            new GetLibraryOrderItemsQuery(LibraryOrderTarget.SECTIONS, null));

        Assert.True(result.IsSuccess);
        Assert.Equal("1 папка", Assert.Single(result.Value).Details);
    }
}
