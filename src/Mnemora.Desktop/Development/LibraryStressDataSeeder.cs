using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Mnemora.Infrastructure.Persistence;

namespace Mnemora.Desktop.Development;

/// <summary>
/// DEBUG/stress-only seeder for the container-based library UI.
/// Stress materials are stored only in SQLite; Markdown files are intentionally not created.
/// </summary>
public sealed class LibraryStressDataSeeder(
    IDbContextFactory<MnemoraDbContext> dbContextFactory,
    ILogger<LibraryStressDataSeeder> logger)
{
    public const int TargetCount = 50_000;

    private const string SeederVersion = "v5-library-containers";
    private const string SectionPrefix = "STRESS V5 — ";
    private const string CompatibilityTopicName = "STRESS V5 compatibility topic";
    private const int BatchSize = 1_000;
    private const int MixedFolderCount = 5_000;
    private const int MixedMaterialCount = 5_000;
    private const int IntermediateDepthMaterialCount = 5_000;
    private const int TypeCount = 25_000;
    private const int LinkedQuestionCount = 5_000;

    private const string FoldersSectionName = "STRESS V5 — 01 — 50 000 папок";
    private const string RootMaterialsSectionName = "STRESS V5 — 02 — 50 000 материалов в разделе";
    private const string MixedSectionName = "STRESS V5 — 03 — папки + материалы";
    private const string Depth1SectionName = "STRESS V5 — 04 — L1, 5 000 материалов";
    private const string Depth2SectionName = "STRESS V5 — 05 — L2, 5 000 материалов";
    private const string Depth3SectionName = "STRESS V5 — 06 — L3, 50 000 материалов";
    private const string TypesSectionName = "STRESS V5 — 07 — статьи + вопросы";
    private const string EmptySectionName = "STRESS V5 — 08 — пустой";
    private const string OneFolderSectionName = "STRESS V5 — 09 — одна папка";
    private const string FiveMaterialsSectionName = "STRESS V5 — 10 — пять материалов";
    private const string Folders29SectionName = "STRESS V5 — 11 — 29 папок";
    private const string Folders30SectionName = "STRESS V5 — 12 — 30 папок";
    private const string Folders31SectionName = "STRESS V5 — 13 — 31 папка";
    private const string Materials29SectionName = "STRESS V5 — 14 — 29 материалов";
    private const string Materials30SectionName = "STRESS V5 — 15 — 30 материалов";
    private const string Materials31SectionName = "STRESS V5 — 16 — 31 материал";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        await using MnemoraDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        logger.LogInformation("Stress seed {Version} started", SeederVersion);

        await EnsureSectionsAndRootsAsync(dbContext, cancellationToken);
        await SeedFolderPagingScenarioAsync(dbContext, cancellationToken);
        await SeedRootMaterialsScenarioAsync(dbContext, cancellationToken);
        await SeedMixedScenarioAsync(dbContext, cancellationToken);
        await SeedDepthScenarioAsync(dbContext, Depth1SectionName, 1, IntermediateDepthMaterialCount, cancellationToken);
        await SeedDepthScenarioAsync(dbContext, Depth2SectionName, 2, IntermediateDepthMaterialCount, cancellationToken);
        await SeedDepthScenarioAsync(dbContext, Depth3SectionName, 3, TargetCount, cancellationToken);
        await SeedTypesScenarioAsync(dbContext, cancellationToken);
        await SeedOneFolderScenarioAsync(dbContext, cancellationToken);
        await SeedFiveMaterialsScenarioAsync(dbContext, cancellationToken);
        await SeedFolderBoundaryScenariosAsync(dbContext, cancellationToken);
        await SeedMaterialBoundaryScenariosAsync(dbContext, cancellationToken);

        logger.LogInformation("Stress seed {Version} completed in {Elapsed}", SeederVersion, stopwatch.Elapsed);
    }

    private async Task EnsureSectionsAndRootsAsync(MnemoraDbContext dbContext, CancellationToken cancellationToken)
    {
        string pattern = SectionPrefix + "%";
        int existingCount = await dbContext.Sections
            .FromSqlInterpolated($"SELECT * FROM sections WHERE name LIKE {pattern}")
            .AsNoTracking()
            .CountAsync(cancellationToken);

        if (existingCount >= TargetCount)
        {
            logger.LogInformation("Stress V5 sections already exist: {Count}", existingCount);
            return;
        }

        SectionColor sectionColor = Enum.GetValues<SectionColor>()[0];
        SectionIcon sectionIcon = Enum.GetValues<SectionIcon>()[0];

        for (int start = existingCount; start < TargetCount; start += BatchSize)
        {
            int end = Math.Min(start + BatchSize, TargetCount);
            var sections = new List<Section>(end - start);
            var roots = new List<LibraryContainer>(end - start);

            for (int index = start; index < end; index++)
            {
                Section section = Section.Create(
                    SectionName.Create(GetSectionName(index)).Value,
                    sectionColor,
                    sectionIcon);
                LibraryContainer root = LibraryContainer.CreateRoot(section.Id).Value;
                sections.Add(section);
                roots.Add(root);
            }

            dbContext.Sections.AddRange(sections);
            dbContext.LibraryContainers.AddRange(roots);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            logger.LogInformation("Stress V5 sections + roots: {Done}/{Total}", end, TargetCount);
        }
    }

    private async Task SeedFolderPagingScenarioAsync(MnemoraDbContext dbContext, CancellationToken cancellationToken)
    {
        Section section = await GetSectionAsync(dbContext, FoldersSectionName, cancellationToken);
        LibraryContainer root = await GetRootAsync(dbContext, section, cancellationToken);
        await EnsureFoldersAsync(dbContext, root, TargetCount, "STRESS V5 Папка", "50k folders", cancellationToken);
    }

    private async Task SeedRootMaterialsScenarioAsync(MnemoraDbContext dbContext, CancellationToken cancellationToken)
    {
        Section section = await GetSectionAsync(dbContext, RootMaterialsSectionName, cancellationToken);
        LibraryContainer root = await GetRootAsync(dbContext, section, cancellationToken);
        Topic topic = await GetOrCreateCompatibilityTopicAsync(dbContext, section, cancellationToken);
        await EnsureArticlesAsync(dbContext, topic.Id, root, TargetCount, "STRESS V5 Материал root", "50k root materials", cancellationToken);
    }

    private async Task SeedMixedScenarioAsync(MnemoraDbContext dbContext, CancellationToken cancellationToken)
    {
        Section section = await GetSectionAsync(dbContext, MixedSectionName, cancellationToken);
        LibraryContainer root = await GetRootAsync(dbContext, section, cancellationToken);
        Topic topic = await GetOrCreateCompatibilityTopicAsync(dbContext, section, cancellationToken);
        await EnsureFoldersAsync(dbContext, root, MixedFolderCount, "STRESS V5 Mixed Папка", "mixed folders", cancellationToken);
        await EnsureArticlesAsync(dbContext, topic.Id, root, MixedMaterialCount, "STRESS V5 Mixed Материал", "mixed materials", cancellationToken);
    }

    private async Task SeedDepthScenarioAsync(
        MnemoraDbContext dbContext,
        string sectionName,
        int depth,
        int materialCount,
        CancellationToken cancellationToken)
    {
        Section section = await GetSectionAsync(dbContext, sectionName, cancellationToken);
        LibraryContainer current = await GetRootAsync(dbContext, section, cancellationToken);

        for (int level = 1; level <= depth; level++)
        {
            current = await GetOrCreateFolderAsync(dbContext, current, $"Уровень {level}", 0, cancellationToken);
        }

        Topic topic = await GetOrCreateCompatibilityTopicAsync(dbContext, section, cancellationToken);
        await EnsureArticlesAsync(
            dbContext,
            topic.Id,
            current,
            materialCount,
            $"STRESS V5 L{depth} Материал",
            $"depth {depth} materials",
            cancellationToken);
    }

    private async Task SeedTypesScenarioAsync(MnemoraDbContext dbContext, CancellationToken cancellationToken)
    {
        Section section = await GetSectionAsync(dbContext, TypesSectionName, cancellationToken);
        LibraryContainer root = await GetRootAsync(dbContext, section, cancellationToken);
        Topic topic = await GetOrCreateCompatibilityTopicAsync(dbContext, section, cancellationToken);

        await EnsureArticlesAsync(dbContext, topic.Id, root, TypeCount, "STRESS V5 Статья", "type articles", cancellationToken);
        await EnsureStandaloneQuestionsAsync(dbContext, topic.Id, root, TypeCount, "STRESS V5 Вопрос", "standalone questions", cancellationToken);
        await EnsureLinkedQuestionsAsync(dbContext, root, LinkedQuestionCount, cancellationToken);
    }

    private async Task SeedOneFolderScenarioAsync(MnemoraDbContext dbContext, CancellationToken cancellationToken)
    {
        Section section = await GetSectionAsync(dbContext, OneFolderSectionName, cancellationToken);
        LibraryContainer root = await GetRootAsync(dbContext, section, cancellationToken);
        await EnsureFoldersAsync(dbContext, root, 1, "Одна папка", "one folder", cancellationToken);
    }

    private async Task SeedFiveMaterialsScenarioAsync(MnemoraDbContext dbContext, CancellationToken cancellationToken)
    {
        Section section = await GetSectionAsync(dbContext, FiveMaterialsSectionName, cancellationToken);
        LibraryContainer root = await GetRootAsync(dbContext, section, cancellationToken);
        Topic topic = await GetOrCreateCompatibilityTopicAsync(dbContext, section, cancellationToken);
        await EnsureArticlesAsync(dbContext, topic.Id, root, 5, "STRESS V5 Пять материалов", "five materials", cancellationToken);
    }

    private async Task SeedFolderBoundaryScenariosAsync(MnemoraDbContext dbContext, CancellationToken cancellationToken)
    {
        await SeedFolderBoundaryScenarioAsync(dbContext, Folders29SectionName, 29, cancellationToken);
        await SeedFolderBoundaryScenarioAsync(dbContext, Folders30SectionName, 30, cancellationToken);
        await SeedFolderBoundaryScenarioAsync(dbContext, Folders31SectionName, 31, cancellationToken);
    }

    private async Task SeedFolderBoundaryScenarioAsync(
        MnemoraDbContext dbContext,
        string sectionName,
        int count,
        CancellationToken cancellationToken)
    {
        Section section = await GetSectionAsync(dbContext, sectionName, cancellationToken);
        LibraryContainer root = await GetRootAsync(dbContext, section, cancellationToken);
        await EnsureFoldersAsync(dbContext, root, count, $"STRESS V5 Boundary {count} Папка", $"{count} folders", cancellationToken);
    }

    private async Task SeedMaterialBoundaryScenariosAsync(MnemoraDbContext dbContext, CancellationToken cancellationToken)
    {
        await SeedMaterialBoundaryScenarioAsync(dbContext, Materials29SectionName, 29, cancellationToken);
        await SeedMaterialBoundaryScenarioAsync(dbContext, Materials30SectionName, 30, cancellationToken);
        await SeedMaterialBoundaryScenarioAsync(dbContext, Materials31SectionName, 31, cancellationToken);
    }

    private async Task SeedMaterialBoundaryScenarioAsync(
        MnemoraDbContext dbContext,
        string sectionName,
        int count,
        CancellationToken cancellationToken)
    {
        Section section = await GetSectionAsync(dbContext, sectionName, cancellationToken);
        LibraryContainer root = await GetRootAsync(dbContext, section, cancellationToken);
        Topic topic = await GetOrCreateCompatibilityTopicAsync(dbContext, section, cancellationToken);
        await EnsureArticlesAsync(dbContext, topic.Id, root, count, $"STRESS V5 Boundary {count} Материал", $"{count} materials", cancellationToken);
    }

    private async Task EnsureFoldersAsync(
        MnemoraDbContext dbContext,
        LibraryContainer parent,
        int targetCount,
        string namePrefix,
        string logLabel,
        CancellationToken cancellationToken)
    {
        int existingCount = await dbContext.LibraryContainers
            .AsNoTracking()
            .CountAsync(container => container.ParentId == parent.Id, cancellationToken);

        if (existingCount >= targetCount)
        {
            logger.LogInformation("Stress V5 {Label} already exist: {Count}", logLabel, existingCount);
            return;
        }

        FolderColor color = Enum.GetValues<FolderColor>()[0];
        FolderIcon icon = Enum.GetValues<FolderIcon>()[0];

        for (int start = existingCount; start < targetCount; start += BatchSize)
        {
            int end = Math.Min(start + BatchSize, targetCount);
            var batch = new List<LibraryContainer>(end - start);

            for (int index = start; index < end; index++)
            {
                LibraryContainer folder = LibraryContainer.CreateFolder(
                    parent,
                    FolderName.Create($"{namePrefix} {index:D5}").Value,
                    color,
                    icon).Value;
                folder.ChangeDisplayOrder(index);
                batch.Add(folder);
            }

            dbContext.LibraryContainers.AddRange(batch);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            logger.LogInformation("Stress V5 {Label}: {Done}/{Total}", logLabel, end, targetCount);
        }
    }

    private async Task EnsureArticlesAsync(
        MnemoraDbContext dbContext,
        TopicId legacyTopicId,
        LibraryContainer targetContainer,
        int targetCount,
        string titlePrefix,
        string logLabel,
        CancellationToken cancellationToken)
    {
        int existingCount = await dbContext.Materials
            .AsNoTracking()
            .OfType<Article>()
            .CountAsync(article => article.ContainerId == targetContainer.Id, cancellationToken);

        if (existingCount >= targetCount)
        {
            logger.LogInformation("Stress V5 {Label} already exist: {Count}", logLabel, existingCount);
            return;
        }

        for (int start = existingCount; start < targetCount; start += BatchSize)
        {
            int end = Math.Min(start + BatchSize, targetCount);
            var batch = new List<Article>(end - start);

            for (int index = start; index < end; index++)
            {
                Article article = Article.Create(
                    legacyTopicId,
                    MaterialTitle.Create($"{titlePrefix} {index:D5}").Value,
                    MaterialDifficulty.Medium,
                    MaterialIcon.DefaultArticle,
                    CreateRewards(),
                    Array.Empty<MaterialTag>()).Value;
                MoveToContainer(article, targetContainer.Id);
                article.ChangeDisplayOrder(index);
                batch.Add(article);
            }

            dbContext.Materials.AddRange(batch);
            dbContext.ChangeTracker.DetectChanges();
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            logger.LogInformation("Stress V5 {Label}: {Done}/{Total}", logLabel, end, targetCount);
        }
    }

    private async Task EnsureStandaloneQuestionsAsync(
        MnemoraDbContext dbContext,
        TopicId legacyTopicId,
        LibraryContainer targetContainer,
        int targetCount,
        string titlePrefix,
        string logLabel,
        CancellationToken cancellationToken)
    {
        int existingCount = await dbContext.Materials
            .AsNoTracking()
            .OfType<Question>()
            .CountAsync(question => question.ContainerId == targetContainer.Id && question.ArticleId == null, cancellationToken);

        if (existingCount >= targetCount)
        {
            logger.LogInformation("Stress V5 {Label} already exist: {Count}", logLabel, existingCount);
            return;
        }

        for (int start = existingCount; start < targetCount; start += BatchSize)
        {
            int end = Math.Min(start + BatchSize, targetCount);
            var batch = new List<Question>(end - start);

            for (int index = start; index < end; index++)
            {
                Question question = Question.CreateStandalone(
                    legacyTopicId,
                    MaterialTitle.Create($"{titlePrefix} {index:D5}").Value,
                    MaterialDifficulty.Medium,
                    MaterialIcon.DefaultQuestion,
                    CreateRewards(),
                    Array.Empty<MaterialTag>()).Value;
                MoveToContainer(question, targetContainer.Id);
                question.ChangeDisplayOrder(index);
                batch.Add(question);
            }

            dbContext.Materials.AddRange(batch);
            dbContext.ChangeTracker.DetectChanges();
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            logger.LogInformation("Stress V5 {Label}: {Done}/{Total}", logLabel, end, targetCount);
        }
    }

    private async Task EnsureLinkedQuestionsAsync(
        MnemoraDbContext dbContext,
        LibraryContainer targetContainer,
        int targetCount,
        CancellationToken cancellationToken)
    {
        int existingCount = await dbContext.Materials
            .AsNoTracking()
            .OfType<Question>()
            .CountAsync(question => question.ContainerId == targetContainer.Id && question.ArticleId != null, cancellationToken);

        if (existingCount >= targetCount)
        {
            logger.LogInformation("Stress V5 linked questions already exist: {Count}", existingCount);
            return;
        }

        for (int start = existingCount; start < targetCount; start += BatchSize)
        {
            int batchCount = Math.Min(BatchSize, targetCount - start);
            List<Article> articles = await dbContext.Materials
                .AsNoTracking()
                .OfType<Article>()
                .Where(article => article.ContainerId == targetContainer.Id)
                .OrderBy(article => article.DisplayOrder)
                .ThenBy(article => article.Id)
                .Skip(start)
                .Take(batchCount)
                .ToListAsync(cancellationToken);

            var questions = new List<Question>(articles.Count);
            for (int index = 0; index < articles.Count; index++)
            {
                int absoluteIndex = start + index;
                Question question = Question.CreateForArticle(
                    articles[index],
                    MaterialTitle.Create($"STRESS V5 Связанный вопрос {absoluteIndex:D5}").Value,
                    MaterialDifficulty.Medium,
                    MaterialIcon.DefaultQuestion,
                    CreateRewards()).Value;
                question.ChangeDisplayOrder(absoluteIndex);
                questions.Add(question);
            }

            if (questions.Count == 0)
            {
                throw new InvalidOperationException("Not enough stress articles to create linked questions.");
            }

            dbContext.Materials.AddRange(questions);
            dbContext.ChangeTracker.DetectChanges();
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            logger.LogInformation("Stress V5 linked questions: {Done}/{Total}", start + questions.Count, targetCount);
        }
    }

    private static async Task<Section> GetSectionAsync(
        MnemoraDbContext dbContext,
        string sectionName,
        CancellationToken cancellationToken)
    {
        Section? section = await dbContext.Sections
            .FromSqlInterpolated($"SELECT * FROM sections WHERE name = {sectionName}")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        return section ?? throw new InvalidOperationException($"Stress section '{sectionName}' was not created.");
    }

    private static async Task<LibraryContainer> GetRootAsync(
        MnemoraDbContext dbContext,
        Section section,
        CancellationToken cancellationToken)
    {
        Guid sectionId = section.Id.Value;
        LibraryContainer? root = await dbContext.LibraryContainers
            .FromSqlInterpolated($"SELECT * FROM library_containers WHERE section_id = {sectionId} AND parent_id IS NULL")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        return root ?? throw new InvalidOperationException($"Root container for stress section '{section.Name.Value}' was not created.");
    }

    private static async Task<LibraryContainer> GetOrCreateFolderAsync(
        MnemoraDbContext dbContext,
        LibraryContainer parent,
        string name,
        int displayOrder,
        CancellationToken cancellationToken)
    {
        Guid parentId = parent.Id.Value;
        LibraryContainer? existing = await dbContext.LibraryContainers
            .FromSqlInterpolated($"SELECT * FROM library_containers WHERE parent_id = {parentId} AND name = {name}")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        FolderColor color = Enum.GetValues<FolderColor>()[0];
        FolderIcon icon = Enum.GetValues<FolderIcon>()[0];
        LibraryContainer folder = LibraryContainer.CreateFolder(
            parent,
            FolderName.Create(name).Value,
            color,
            icon).Value;
        folder.ChangeDisplayOrder(displayOrder);
        dbContext.LibraryContainers.Add(folder);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return folder;
    }

    private static async Task<Topic> GetOrCreateCompatibilityTopicAsync(
        MnemoraDbContext dbContext,
        Section section,
        CancellationToken cancellationToken)
    {
        Guid sectionId = section.Id.Value;
        Topic? existing = await dbContext.Topics
            .FromSqlInterpolated($"SELECT * FROM topics WHERE section_id = {sectionId} AND name = {CompatibilityTopicName}")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        Topic topic = Topic.Create(
            section.Id,
            TopicName.Create(CompatibilityTopicName).Value,
            Enum.GetValues<TopicColor>()[0],
            Enum.GetValues<TopicIcon>()[0]);
        dbContext.Topics.Add(topic);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return topic;
    }

    private static MaterialExperienceRewards CreateRewards() =>
        MaterialExperienceRewards.Create(50, 20).Value;

    private static void MoveToContainer(Article article, LibraryContainerId containerId)
    {
        var result = article.MoveToContainer(containerId);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Message);
        }
    }

    private static void MoveToContainer(Question question, LibraryContainerId containerId)
    {
        var result = question.MoveToContainer(containerId);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Message);
        }
    }

    private static string GetSectionName(int index) => index switch
    {
        0 => FoldersSectionName,
        1 => RootMaterialsSectionName,
        2 => MixedSectionName,
        3 => Depth1SectionName,
        4 => Depth2SectionName,
        5 => Depth3SectionName,
        6 => TypesSectionName,
        7 => EmptySectionName,
        8 => OneFolderSectionName,
        9 => FiveMaterialsSectionName,
        10 => Folders29SectionName,
        11 => Folders30SectionName,
        12 => Folders31SectionName,
        13 => Materials29SectionName,
        14 => Materials30SectionName,
        15 => Materials31SectionName,
        _ => $"{SectionPrefix}Раздел {index:D5}",
    };
}
