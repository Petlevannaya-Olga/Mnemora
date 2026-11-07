using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Mnemora.Infrastructure.Persistence;
using Xunit;

namespace Mnemora.Infrastructure.Tests;

public sealed class StableEnumStringPersistenceTests
{
    [Fact]
    public async Task ExistingSqliteEnumStrings_ArePreservedForSectionsAndFolders()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var provider = TestServiceProviderFactory.Create(temporaryDirectory.Path);
        await using var scope = provider.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();

        var section = Section.Create(
            SectionName.Create("Enum persistence").Value,
            SectionColor.TEAL,
            SectionIcon.DOT_NET);
        var root = LibraryContainer.CreateRoot(section.Id).Value;
        var folder = LibraryContainer.CreateFolder(
            root,
            FolderName.Create("Enum folder").Value,
            FolderColor.BLUE,
            FolderIcon.BOOKMARK).Value;

        db.Sections.Add(section);
        db.LibraryContainers.AddRange(root, folder);
        await db.SaveChangesAsync();

        await db.Database.OpenConnectionAsync();
        var connection = db.Database.GetDbConnection();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT color, icon FROM sections LIMIT 1";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Teal", reader.GetString(0));
            Assert.Equal("DotNet", reader.GetString(1));
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT color, icon FROM library_containers WHERE name = 'Enum folder'";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Blue", reader.GetString(0));
            Assert.Equal("Bookmark", reader.GetString(1));
        }

        db.ChangeTracker.Clear();
        var restored = await db.LibraryContainers.SingleAsync(container => container.Id == folder.Id);
        Assert.Equal(FolderColor.BLUE, restored.Color);
        Assert.Equal(FolderIcon.BOOKMARK, restored.Icon);
    }
}
