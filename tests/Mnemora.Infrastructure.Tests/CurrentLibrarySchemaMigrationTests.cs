using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Infrastructure.Persistence;
using Xunit;

namespace Mnemora.Infrastructure.Tests;

public sealed class CurrentLibrarySchemaMigrationTests
{
    [Fact]
    public async Task FreshDatabase_AllMigrations_RemoveOperationalTopicSchema()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(temporaryDirectory.Path);
        await using var scope = provider.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using MnemoraDbContext context = await factory.CreateDbContextAsync();

        // Unlike EnsureCreated, MigrateAsync exercises the real first-run startup path.
        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(0, await ScalarAsync(context,
            "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='topics'"));
        Assert.Equal(0, await ScalarAsync(context,
            "SELECT COUNT(*) AS Value FROM pragma_table_info('materials') WHERE name='topic_id'"));
        Assert.Equal(1, await ScalarAsync(context,
            "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name='legacy_topic_archive'"));
        Assert.Equal(0, await ScalarAsync(context,
            "SELECT COUNT(*) AS Value FROM pragma_foreign_key_check"));
        Assert.DoesNotContain(context.Model.GetEntityTypes(), entity => entity.GetTableName() == "topics");
    }

    [Fact]
    public async Task FreshMigratedDatabase_PersistsFolderAndMaterialWithoutLegacyTopic()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(temporaryDirectory.Path);
        await using var scope = provider.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using MnemoraDbContext context = await factory.CreateDbContextAsync();
        await context.Database.MigrateAsync();

        Section section = Section.Create(
            SectionName.Create("Migration check").Value, SectionColor.Teal, SectionIcon.Folder);
        LibraryContainer root = LibraryContainer.CreateRoot(section.Id).Value;
        LibraryContainer folder = LibraryContainer.CreateFolder(
            root, FolderName.Create("First folder").Value, FolderColor.Teal, FolderIcon.Folder).Value;
        Article article = Article.CreateInContainer(
            folder.Id, MaterialTitle.Create("Article").Value,
            MaterialDifficulty.Medium, MaterialIcon.DefaultArticle,
            MaterialExperienceRewards.Create(50, 20).Value).Value;
        Question question = Question.CreateForArticle(
            article, MaterialTitle.Create("Question").Value,
            MaterialDifficulty.Medium, MaterialIcon.DefaultQuestion,
            MaterialExperienceRewards.Create(50, 20).Value).Value;

        context.Sections.Add(section);
        context.LibraryContainers.AddRange(root, folder);
        context.Materials.AddRange(article, question);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Article savedArticle = await context.Materials.OfType<Article>().SingleAsync();
        Question savedQuestion = await context.Materials.OfType<Question>().SingleAsync();
        Assert.Equal(folder.Id, savedArticle.ContainerId);
        Assert.Equal(folder.Id, savedQuestion.ContainerId);
        Assert.Equal(article.Id, savedQuestion.ArticleId);
        Assert.Equal(0, await ScalarAsync(context,
            "SELECT COUNT(*) AS Value FROM pragma_foreign_key_check"));
    }

    private static Task<int> ScalarAsync(MnemoraDbContext context, string sql) =>
        context.Database.SqlQueryRaw<int>(sql).SingleAsync();
}
