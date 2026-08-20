using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Mnemora.Infrastructure.Persistence;

namespace Mnemora.Desktop.Development;

/// <summary>
/// DEBUG/stress-only seeder for checking library paging and bounded caches.
///
/// Dataset shape:
/// - 50,000 sections total;
/// - 50,000 topics inside one "hot" section;
/// - 50,000 top-level articles inside one "hot" topic.
///
/// This deliberately maximizes each paging level instead of distributing
/// topics/materials evenly.
/// </summary>
public sealed class LibraryStressDataSeeder(
    IDbContextFactory<MnemoraDbContext> dbContextFactory,
    ILogger<LibraryStressDataSeeder> logger)
{
    public const int TargetCount = 50_000;

    private const string SeederVersion = "v4-owned-rewards-per-material";
    private const int BatchSize = 1_000;

    private const string SectionPrefix = "STRESS Раздел ";
    private const string HotSectionName =
        "STRESS Раздел 00000 — 50 000 тем";

    private const string TopicPrefix = "STRESS Тема ";
    private const string HotTopicName =
        "STRESS Тема 00000 — 50 000 материалов";

    private const string MaterialPrefix = "STRESS Материал ";

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(
                cancellationToken);

        logger.LogInformation(
            "Stress seed {Version} started: {Count} sections, topics and materials",
            SeederVersion,
            TargetCount);

        await EnsureSectionsAsync(
            dbContext,
            cancellationToken);

        Section hotSection =
            await GetHotSectionAsync(
                dbContext,
                cancellationToken);

        await EnsureTopicsAsync(
            dbContext,
            hotSection.Id,
            cancellationToken);

        Topic hotTopic =
            await GetHotTopicAsync(
                dbContext,
                hotSection.Id,
                cancellationToken);

        await EnsureMaterialsAsync(
            dbContext,
            hotTopic.Id,
            cancellationToken);

        logger.LogInformation(
            "Stress seed {Version} completed in {Elapsed}.",
            SeederVersion,
            stopwatch.Elapsed);
    }

    private async Task EnsureSectionsAsync(
        MnemoraDbContext dbContext,
        CancellationToken cancellationToken)
    {
        string sectionPattern =
            SectionPrefix + "%";

        int existingCount =
            await dbContext.Sections
                .FromSqlInterpolated(
                    $"""
                     SELECT *
                     FROM sections
                     WHERE name LIKE {sectionPattern}
                     """)
                .AsNoTracking()
                .CountAsync(cancellationToken);

        if (existingCount >= TargetCount)
        {
            logger.LogInformation(
                "Stress sections already exist: {Count}.",
                existingCount);

            return;
        }

        SectionColor color =
            Enum.GetValues<SectionColor>()[0];

        SectionIcon icon =
            Enum.GetValues<SectionIcon>()[0];

        for (int start = existingCount;
             start < TargetCount;
             start += BatchSize)
        {
            int end =
                Math.Min(
                    start + BatchSize,
                    TargetCount);

            var batch =
                new List<Section>(
                    end - start);

            for (int index = start;
                 index < end;
                 index++)
            {
                string name =
                    index == 0
                        ? HotSectionName
                        : $"{SectionPrefix}{index:D5}";

                batch.Add(
                    Section.Create(
                        SectionName.Create(name).Value,
                        color,
                        icon));
            }

            dbContext.Sections.AddRange(batch);

            await dbContext.SaveChangesAsync(
                cancellationToken);

            dbContext.ChangeTracker.Clear();

            logger.LogInformation(
                "Stress sections: {Done}/{Total}",
                end,
                TargetCount);
        }
    }

    private static async Task<Section> GetHotSectionAsync(
        MnemoraDbContext dbContext,
        CancellationToken cancellationToken)
    {
        Section? section =
            await dbContext.Sections
                .FromSqlInterpolated(
                    $"""
                     SELECT *
                     FROM sections
                     WHERE name = {HotSectionName}
                     """)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

        return section
               ?? throw new InvalidOperationException(
                   $"Stress section '{HotSectionName}' was not created.");
    }

    private async Task EnsureTopicsAsync(
        MnemoraDbContext dbContext,
        SectionId sectionId,
        CancellationToken cancellationToken)
    {
        int existingCount =
            await dbContext.Topics
                .AsNoTracking()
                .CountAsync(
                    topic =>
                        topic.SectionId == sectionId,
                    cancellationToken);

        if (existingCount >= TargetCount)
        {
            logger.LogInformation(
                "Stress topics already exist: {Count}.",
                existingCount);

            return;
        }

        TopicColor color =
            Enum.GetValues<TopicColor>()[0];

        TopicIcon icon =
            Enum.GetValues<TopicIcon>()[0];

        for (int start = existingCount;
             start < TargetCount;
             start += BatchSize)
        {
            int end =
                Math.Min(
                    start + BatchSize,
                    TargetCount);

            var batch =
                new List<Topic>(
                    end - start);

            for (int index = start;
                 index < end;
                 index++)
            {
                string name =
                    index == 0
                        ? HotTopicName
                        : $"{TopicPrefix}{index:D5}";

                batch.Add(
                    Topic.Create(
                        sectionId,
                        TopicName.Create(name).Value,
                        color,
                        icon));
            }

            dbContext.Topics.AddRange(batch);

            await dbContext.SaveChangesAsync(
                cancellationToken);

            dbContext.ChangeTracker.Clear();

            logger.LogInformation(
                "Stress topics: {Done}/{Total}",
                end,
                TargetCount);
        }
    }

    private static async Task<Topic> GetHotTopicAsync(
        MnemoraDbContext dbContext,
        SectionId sectionId,
        CancellationToken cancellationToken)
    {
        Guid rawSectionId =
            sectionId.Value;

        Topic? topic =
            await dbContext.Topics
                .FromSqlInterpolated(
                    $"""
                     SELECT *
                     FROM topics
                     WHERE section_id = {rawSectionId}
                       AND name = {HotTopicName}
                     """)
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

        return topic
               ?? throw new InvalidOperationException(
                   $"Stress topic '{HotTopicName}' was not created.");
    }

    private async Task EnsureMaterialsAsync(
        MnemoraDbContext dbContext,
        TopicId topicId,
        CancellationToken cancellationToken)
    {
        int existingCount =
            await dbContext.Materials
                .AsNoTracking()
                .OfType<Article>()
                .CountAsync(
                    article =>
                        article.TopicId == topicId,
                    cancellationToken);

        if (existingCount >= TargetCount)
        {
            logger.LogInformation(
                "Stress materials already exist: {Count}.",
                existingCount);

            return;
        }

        MaterialTag[] tags = [];

        for (int start = existingCount;
             start < TargetCount;
             start += BatchSize)
        {
            int end =
                Math.Min(
                    start + BatchSize,
                    TargetCount);

            var batch =
                new List<Article>(
                    end - start);

            for (int index = start;
                 index < end;
                 index++)
            {
                string title =
                    $"{MaterialPrefix}{index:D5}";

                // ExperienceRewards is an EF owned value object.
                // Each Material must get its own CLR instance.
                MaterialExperienceRewards rewards =
                    MaterialExperienceRewards.Create(
                        50,
                        20).Value;

                Article article =
                    Article.Create(
                        topicId,
                        MaterialTitle.Create(title).Value,
                        MaterialDifficulty.Medium,
                        MaterialIcon.DefaultArticle,
                        rewards,
                        tags).Value;

                batch.Add(article);
            }

            dbContext.Materials.AddRange(batch);

            // Keep EF change detection enabled for the required owned graph.
            dbContext.ChangeTracker.DetectChanges();

            await dbContext.SaveChangesAsync(
                cancellationToken);

            dbContext.ChangeTracker.Clear();

            logger.LogInformation(
                "Stress materials: {Done}/{Total}",
                end,
                TargetCount);
        }
    }
}
