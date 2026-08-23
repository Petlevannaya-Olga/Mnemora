using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
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
        Topic topic = Topic.Create(
            section.Id,
            TopicName.Create("CLR").Value,
            TopicColor.Purple,
            TopicIcon.DotNet);

        dbContext.Sections.Add(section);
        dbContext.Topics.Add(topic);
        await dbContext.SaveChangesAsync(ct);

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
                {topic.Id.Value},
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

        Assert.Equal(topic.Id, material.TopicId);
        Assert.Equal(topic.Id.Value, material.ContainerId.Value);

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

    private static Section CreateSection(string name) =>
        Section.Create(
            SectionName.Create(name).Value,
            Enum.GetValues<SectionColor>()[0],
            Enum.GetValues<SectionIcon>()[0]);
}
