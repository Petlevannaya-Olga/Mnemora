using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Infrastructure.Persistence;
using Xunit;

namespace Mnemora.Infrastructure.Tests;

public sealed class RemoveLegacyTopicsMigrationTests
{
    private const string BeforeRemoval = "20261107010000_RemoveMaterialTopicId";

    [Fact]
    public async Task Upgrade_DropsTopicsButPreservesFoldersAndMaterials()
    {
        CancellationToken ct = CancellationToken.None;
        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(temporaryDirectory.Path);
        await using var scope = provider.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using MnemoraDbContext context = await factory.CreateDbContextAsync(ct);
        IMigrator migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeRemoval, ct);

        Section section = Section.Create(
            SectionName.Create("Test Section").Value, SectionColor.Teal, SectionIcon.Folder);
        LibraryContainer root = LibraryContainer.CreateRoot(section.Id).Value;
        LibraryContainer folder = LibraryContainer.CreateFolder(
            root, FolderName.Create("Test Folder").Value, FolderColor.Teal, FolderIcon.Bookmark).Value;
        Article article = Article.CreateInContainer(
            folder.Id, MaterialTitle.Create("Test Article").Value,
            MaterialDifficulty.Medium, MaterialIcon.DefaultArticle,
            MaterialExperienceRewards.Create(50, 20).Value).Value;
        Question question = Question.CreateForArticle(
            article, MaterialTitle.Create("Linked Question").Value,
            MaterialDifficulty.Medium, MaterialIcon.DefaultQuestion,
            MaterialExperienceRewards.Create(50, 20).Value).Value;

        context.Sections.Add(section);
        context.LibraryContainers.AddRange(root, folder);
        context.Materials.AddRange(article, question);
        await context.SaveChangesAsync(ct);
        await LegacyTopicSqlFixture.InsertAsync(
            context, section.Id, folder.Id.Value, "Test Folder", "Teal", "Bookmark", ct);

        await migrator.MigrateAsync(targetMigration: null, cancellationToken: ct);
        context.ChangeTracker.Clear();

        Assert.Equal(0, await TableCountAsync(context, ct));
        Assert.Equal(2, await context.Materials.CountAsync(ct));
        Assert.Equal(2, await context.LibraryContainers.CountAsync(ct));
        Assert.Equal(folder.Id, (await context.Materials.OfType<Article>()
            .SingleAsync(ct)).ContainerId);
        Assert.Equal(article.Id, (await context.Materials.OfType<Question>()
            .SingleAsync(ct)).ArticleId);
        Assert.Contains("20261107020000_RemoveLegacyTopics",
            await context.Database.GetAppliedMigrationsAsync(ct));

        // A downgrade restores the historical table from first-level folders.
        await migrator.MigrateAsync(BeforeRemoval, ct);
        Assert.Equal(1, await TableCountAsync(context, ct));
        Assert.Equal(1, await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM topics").SingleAsync(ct));
    }

    [Fact]
    public async Task Upgrade_RefusesOrphanLegacyTopic_AndKeepsOriginalTable()
    {
        CancellationToken ct = CancellationToken.None;
        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(temporaryDirectory.Path);
        await using var scope = provider.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using MnemoraDbContext context = await factory.CreateDbContextAsync(ct);
        IMigrator migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeRemoval, ct);

        Section section = Section.Create(
            SectionName.Create("Legacy Section").Value, SectionColor.Teal, SectionIcon.Folder);
        context.Sections.Add(section);
        await context.SaveChangesAsync(ct);
        await LegacyTopicSqlFixture.InsertAsync(
            context, section.Id, Guid.NewGuid(), "Unmigrated", "Teal", "Bookmark", ct);

        Exception? failure = await Record.ExceptionAsync(() =>
            migrator.MigrateAsync(targetMigration: null, cancellationToken: ct));
        Assert.NotNull(failure);
        Assert.Equal(1, await TableCountAsync(context, ct));
        Assert.Equal(1, await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM topics").SingleAsync(ct));
        Assert.DoesNotContain("20261107020000_RemoveLegacyTopics",
            await context.Database.GetAppliedMigrationsAsync(ct));
    }

    private static async Task<int> TableCountAsync(MnemoraDbContext context, CancellationToken ct) =>
        await context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='topics'")
            .SingleAsync(ct);
}
