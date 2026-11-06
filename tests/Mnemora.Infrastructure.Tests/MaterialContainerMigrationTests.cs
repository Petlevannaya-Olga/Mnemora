using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Application.Database;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Infrastructure.Persistence;
using Xunit;

namespace Mnemora.Infrastructure.Tests;

public sealed class MaterialContainerMigrationTests
{
    private const string BeforeLibraryContainersMigration =
        "20260819165930_AddLibraryPagingIndexes";

    [Fact]
    public async Task Migration_BackfillsContainerIdFromExistingTopicId()
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

        // Создаём БД в состоянии до LibraryContainer.
        await migrator.MigrateAsync(
            BeforeLibraryContainersMigration,
            ct);

        Section section = CreateSection("C#");
        Guid topicId = Guid.NewGuid();
        dbContext.Sections.Add(section);
        await dbContext.SaveChangesAsync(ct);
        await LegacyTopicSqlFixture.InsertAsync(
            dbContext, section.Id, topicId, "CLR", "Purple", "DotNet", ct);

        // Текущая EF-модель уже знает о container_id, а старая схема ещё нет,
        // поэтому старый Material намеренно вставляем SQL-ом в историческую схему.
        Guid materialId = Guid.NewGuid();
        MaterialId expectedMaterialId = MaterialId.Create(materialId).Value;
        DateTime now = DateTime.UtcNow;

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO materials
            (
                id,
                topic_id,
                title,
                difficulty,
                icon,
                study_points,
                review_points,
                learning_revision,
                created_at,
                updated_at,
                type,
                article_id,
                display_order
            )
            VALUES
            (
                {materialId},
                {topicId},
                {"Garbage Collector"},
                {(int)MaterialDifficulty.Medium},
                {MaterialIcon.DefaultArticle.Key},
                {50},
                {20},
                {1},
                {now},
                {now},
                {(int)MaterialType.Article},
                NULL,
                {Material.DefaultDisplayOrder}
            );
            """,
            ct);

        // AddLibraryContainers создаст folder с Id == old Topic.Id,
        // AddMaterialContainerId затем перенесёт topic_id в container_id.
        await migrator.MigrateAsync(
            targetMigration: null,
            cancellationToken: ct);

        dbContext.ChangeTracker.Clear();

        Material material = await dbContext.Materials
            .AsNoTracking()
            .SingleAsync(
                current => current.Id == expectedMaterialId,
                ct);

        Assert.Equal(topicId, material.ContainerId.Value);

        LibraryContainer folder = await dbContext.LibraryContainers
            .AsNoTracking()
            .SingleAsync(
                container => container.Id == material.ContainerId,
                ct);

        Assert.True(folder.IsFolder);
        Assert.Equal(section.Id, folder.SectionId);
        Assert.Equal(1, folder.Depth);
        Assert.NotNull(folder.ParentId);
        Assert.Equal("CLR", folder.Name!.Value);
    }


    [Fact]
    public async Task Migration_RepairsAppliedLibraryContainersWithoutTopicFolders()
    {
        CancellationToken ct = CancellationToken.None;
        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider =
            TestServiceProviderFactory.Create(temporaryDirectory.Path);
        await using var scope = provider.CreateAsyncScope();

        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using MnemoraDbContext dbContext =
            await factory.CreateDbContextAsync(ct);

        IMigrator migrator = dbContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeLibraryContainersMigration, ct);

        Section section = CreateSection("Legacy");
        Guid topicId = Guid.NewGuid();
        dbContext.Sections.Add(section);
        await dbContext.SaveChangesAsync(ct);
        await LegacyTopicSqlFixture.InsertAsync(
            dbContext, section.Id, topicId, "Old topic", "Purple", "DotNet", ct);

        Guid materialId = Guid.NewGuid();
        await InsertLegacyMaterialAsync(dbContext, materialId, topicId, "Legacy material", ct);

        await migrator.MigrateAsync("20260822085333_AddLibraryContainers", ct);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM library_containers WHERE id = {topicId};", ct);

        await migrator.MigrateAsync(targetMigration: null, cancellationToken: ct);

        LibraryContainer folder = await dbContext.LibraryContainers
            .AsNoTracking()
            .SingleAsync(container => container.Id == LibraryContainerId.Create(topicId).Value, ct);
        Material material = await dbContext.Materials
            .AsNoTracking()
            .SingleAsync(current => current.Id == MaterialId.Create(materialId).Value, ct);

        Assert.Equal(1, folder.Depth);
        Assert.Equal(topicId, material.ContainerId.Value);
    }

    [Fact]
    public async Task DatabaseInitializer_RecoversInterruptedMaterialContainerMigration()
    {
        CancellationToken ct = CancellationToken.None;
        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider =
            TestServiceProviderFactory.Create(temporaryDirectory.Path);
        await using var scope = provider.CreateAsyncScope();

        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using MnemoraDbContext dbContext =
            await factory.CreateDbContextAsync(ct);

        IMigrator migrator = dbContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeLibraryContainersMigration, ct);

        Section section = CreateSection("Interrupted");
        Guid topicId = Guid.NewGuid();
        dbContext.Sections.Add(section);
        await dbContext.SaveChangesAsync(ct);
        await LegacyTopicSqlFixture.InsertAsync(
            dbContext, section.Id, topicId, "Topic", "Purple", "DotNet", ct);

        Guid materialId = Guid.NewGuid();
        await InsertLegacyMaterialAsync(
            dbContext, materialId, topicId, "Interrupted material", ct);

        await migrator.MigrateAsync("20260822085333_AddLibraryContainers", ct);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM library_containers WHERE id = {topicId};", ct);
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE materials ADD COLUMN container_id TEXT NULL;", ct);
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE materials SET container_id = topic_id;", ct);

        IDatabaseInitializer initializer =
            scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
        var result = await initializer.InitializeAsync(ct);

        Assert.True(result.IsSuccess);

        await using MnemoraDbContext verificationContext =
            await factory.CreateDbContextAsync(ct);
        Material material = await verificationContext.Materials
            .AsNoTracking()
            .SingleAsync(current => current.Id == MaterialId.Create(materialId).Value, ct);

        Assert.Equal(topicId, material.ContainerId.Value);
        Assert.Contains(
            "20260823093103_AddMaterialContainerId",
            await verificationContext.Database.GetAppliedMigrationsAsync(ct));
    }

    private static Task InsertLegacyMaterialAsync(
        MnemoraDbContext dbContext,
        Guid materialId,
        Guid topicId,
        string title,
        CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;

        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO materials
            (
                id, topic_id, title, difficulty, icon, study_points,
                review_points, learning_revision, created_at, updated_at,
                type, article_id, display_order
            )
            VALUES
            (
                {materialId}, {topicId}, {title},
                {(int)MaterialDifficulty.Medium}, {MaterialIcon.DefaultArticle.Key},
                {50}, {20}, {1}, {now}, {now},
                {(int)MaterialType.Article}, NULL, {Material.DefaultDisplayOrder}
            );
            """,
            ct);
    }

    private static Section CreateSection(string name) =>
        Section.Create(
            SectionName.Create(name).Value,
            Enum.GetValues<SectionColor>()[0],
            Enum.GetValues<SectionIcon>()[0]);
}
