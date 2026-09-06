using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Mnemora.Infrastructure.Persistence;
using Xunit;

namespace Mnemora.Infrastructure.Tests;

public sealed class LibraryContainerGuidNormalizationMigrationTests
{
    private const string BeforeNormalizationMigration =
        "20260823093103_AddMaterialContainerId";

    [Fact]
    public async Task Migration_NormalizesLegacyLowercaseRootId_AndPreservesHierarchy()
    {
        CancellationToken ct = CancellationToken.None;
        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(
            temporaryDirectory.Path);
        await using var scope = provider.CreateAsyncScope();

        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using MnemoraDbContext dbContext =
            await factory.CreateDbContextAsync(ct);

        IMigrator migrator = dbContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeNormalizationMigration, ct);

        Section section = Section.Create(
            SectionName.Create("Legacy containers").Value,
            Enum.GetValues<SectionColor>()[0],
            Enum.GetValues<SectionIcon>()[0]);
        LibraryContainer root = LibraryContainer.CreateRoot(section.Id).Value;
        LibraryContainer folder = LibraryContainer.CreateFolder(
            root,
            FolderName.Create("Child").Value,
            FolderColor.Teal,
            FolderIcon.Folder).Value;

        dbContext.Sections.Add(section);
        dbContext.LibraryContainers.AddRange(root, folder);
        await dbContext.SaveChangesAsync(ct);

        Guid rootId = root.Id.Value;
        Guid folderId = folder.Id.Value;

        await using (var transaction = await dbContext.Database.BeginTransactionAsync(ct))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "PRAGMA defer_foreign_keys = ON;",
                ct);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE library_containers SET parent_id = lower(parent_id) WHERE id = {folderId};",
                ct);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE library_containers SET id = lower(id) WHERE id = {rootId};",
                ct);
            await transaction.CommitAsync(ct);
        }

        dbContext.ChangeTracker.Clear();
        LibraryContainerId expectedRootId = LibraryContainerId.Create(rootId).Value;
        LibraryContainer? beforeMigration = await dbContext.LibraryContainers
            .AsNoTracking()
            .SingleOrDefaultAsync(container => container.Id == expectedRootId, ct);

        Assert.Null(beforeMigration);

        await migrator.MigrateAsync(targetMigration: null, cancellationToken: ct);
        dbContext.ChangeTracker.Clear();

        LibraryContainer normalizedRoot = await dbContext.LibraryContainers
            .AsNoTracking()
            .SingleAsync(container => container.Id == expectedRootId, ct);
        LibraryContainer normalizedFolder = await dbContext.LibraryContainers
            .AsNoTracking()
            .SingleAsync(
                container => container.Id == LibraryContainerId.Create(folderId).Value,
                ct);

        Assert.True(normalizedRoot.IsRoot);
        Assert.Equal(expectedRootId, normalizedFolder.ParentId);
        Assert.Contains(
            "20260824160000_NormalizeLibraryContainerGuidText",
            await dbContext.Database.GetAppliedMigrationsAsync(ct));
    }
}
