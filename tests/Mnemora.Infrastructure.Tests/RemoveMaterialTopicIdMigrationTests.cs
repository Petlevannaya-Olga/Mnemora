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

public sealed class RemoveMaterialTopicIdMigrationTests
{
    [Fact]
    public async Task Upgrade_PreservesRootArticleAndLinkedQuestion_WhenLegacyTopicDoesNotExist()
    {
        CancellationToken ct = CancellationToken.None;
        using var temporaryDirectory = new TemporaryDirectory();
        await using ServiceProvider provider = TestServiceProviderFactory.Create(temporaryDirectory.Path);
        await using var scope = provider.CreateAsyncScope();

        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MnemoraDbContext>>();
        await using MnemoraDbContext context = await factory.CreateDbContextAsync(ct);
        IMigrator migrator = context.Database.GetService<IMigrator>();

        // Existing installations may have material rows in the section root or nested
        // folders, for which topic_id points to a non-existing legacy Topic.
        await migrator.MigrateAsync("20260905010000_DetachMaterialsFromTopics", ct);

        Section section = Section.Create(
            SectionName.Create("Migration section").Value,
            SectionColor.Teal,
            SectionIcon.Folder);
        LibraryContainer root = LibraryContainer.CreateRoot(section.Id).Value;
        context.Sections.Add(section);
        context.LibraryContainers.Add(root);
        await context.SaveChangesAsync(ct);

        Guid articleId = Guid.NewGuid();
        Guid questionId = Guid.NewGuid();
        Guid orphanLegacyTopicId = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO materials
            (
                id, topic_id, container_id, title, difficulty, icon,
                study_points, review_points, learning_revision,
                created_at, updated_at, type, article_id, display_order
            )
            VALUES
            (
                {articleId}, {orphanLegacyTopicId}, {root.Id.Value}, {"Root article"},
                {(int)MaterialDifficulty.Medium}, {MaterialIcon.DefaultArticle.Key},
                {50}, {20}, {1}, {now}, {now},
                {(int)MaterialType.Article}, NULL, {Material.DefaultDisplayOrder}
            );
            """, ct);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO materials
            (
                id, topic_id, container_id, title, difficulty, icon,
                study_points, review_points, learning_revision,
                created_at, updated_at, type, article_id, display_order
            )
            VALUES
            (
                {questionId}, {orphanLegacyTopicId}, {root.Id.Value}, {"Linked question"},
                {(int)MaterialDifficulty.Medium}, {MaterialIcon.DefaultQuestion.Key},
                {50}, {20}, {1}, {now}, {now},
                {(int)MaterialType.Question}, {articleId}, {Material.DefaultDisplayOrder}
            );
            """, ct);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO material_tags (material_id, value) VALUES ({articleId}, {"legacy-tag"});",
            ct);

        await migrator.MigrateAsync(targetMigration: null, cancellationToken: ct);
        context.ChangeTracker.Clear();

        Article article = await context.Materials.OfType<Article>()
            .Include(material => material.Tags)
            .SingleAsync(material => material.Id == MaterialId.Create(articleId).Value, ct);
        Question question = await context.Materials.OfType<Question>()
            .SingleAsync(material => material.Id == MaterialId.Create(questionId).Value, ct);

        Assert.Equal(root.Id, article.ContainerId);
        Assert.Equal(root.Id, question.ContainerId);
        Assert.Equal(article.Id, question.ArticleId);
        Assert.Equal("Root article", article.Title.Value);
        Assert.Equal("Linked question", question.Title.Value);
        Assert.Equal("legacy-tag", Assert.Single(article.Tags).Value);

        await context.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA table_info('materials');";
            await using var reader = await command.ExecuteReaderAsync(ct);
            var columnNames = new List<string>();
            while (await reader.ReadAsync(ct))
            {
                columnNames.Add(reader.GetString(1));
            }

            Assert.Contains("container_id", columnNames);
            Assert.DoesNotContain("topic_id", columnNames);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
