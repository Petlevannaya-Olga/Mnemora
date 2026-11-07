using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
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
    #pragma warning disable CA1707
    public const int TARGET_COUNT = 50_000;
    #pragma warning restore CA1707

    private const string SEEDER_VERSION = "v5-library-containers";
    private const string SECTION_PREFIX = "STRESS V5 — ";
    private const int BATCH_SIZE = 1_000;
    private const int MIXED_FOLDER_COUNT = 5_000;
    private const int MIXED_MATERIAL_COUNT = 5_000;
    private const int INTERMEDIATE_DEPTH_MATERIAL_COUNT = 5_000;
    private const int TYPE_COUNT = 25_000;
    private const int LINKED_QUESTION_COUNT = 5_000;

    private const string FOLDERS_SECTION_NAME = "STRESS V5 — 01 — 50 000 папок";
    private const string ROOT_MATERIALS_SECTION_NAME = "STRESS V5 — 02 — 50 000 материалов в разделе";
    private const string MIXED_SECTION_NAME = "STRESS V5 — 03 — папки + материалы";
    private const string DEPTH1_SECTION_NAME = "STRESS V5 — 04 — L1, 5 000 материалов";
    private const string DEPTH2_SECTION_NAME = "STRESS V5 — 05 — L2, 5 000 материалов";
    private const string DEPTH3_SECTION_NAME = "STRESS V5 — 06 — L3, 50 000 материалов";
    private const string TYPES_SECTION_NAME = "STRESS V5 — 07 — статьи + вопросы";
    private const string EMPTY_SECTION_NAME = "STRESS V5 — 08 — пустой";
    private const string ONE_FOLDER_SECTION_NAME = "STRESS V5 — 09 — одна папка";
    private const string FIVE_MATERIALS_SECTION_NAME = "STRESS V5 — 10 — пять материалов";
    private const string FOLDERS29_SECTION_NAME = "STRESS V5 — 11 — 29 папок";
    private const string FOLDERS30_SECTION_NAME = "STRESS V5 — 12 — 30 папок";
    private const string FOLDERS31_SECTION_NAME = "STRESS V5 — 13 — 31 папка";
    private const string MATERIALS29_SECTION_NAME = "STRESS V5 — 14 — 29 материалов";
    private const string MATERIALS30_SECTION_NAME = "STRESS V5 — 15 — 30 материалов";
    private const string MATERIALS31_SECTION_NAME = "STRESS V5 — 16 — 31 материал";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        await using MnemoraDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        logger.LogInformation("Stress seed {Version} started", SEEDER_VERSION);

        await EnsureSectionsAndRootsAsync(dbContext, cancellationToken);
        await SeedFolderPagingScenarioAsync(dbContext, cancellationToken);
        await SeedRootMaterialsScenarioAsync(dbContext, cancellationToken);
        await SeedMixedScenarioAsync(dbContext, cancellationToken);
        await SeedDepthScenarioAsync(dbContext, DEPTH1_SECTION_NAME, 1, INTERMEDIATE_DEPTH_MATERIAL_COUNT, cancellationToken);
        await SeedDepthScenarioAsync(dbContext, DEPTH2_SECTION_NAME, 2, INTERMEDIATE_DEPTH_MATERIAL_COUNT, cancellationToken);
        await SeedDepthScenarioAsync(dbContext, DEPTH3_SECTION_NAME, 3, TARGET_COUNT, cancellationToken);
        await SeedTypesScenarioAsync(dbContext, cancellationToken);
        await SeedOneFolderScenarioAsync(dbContext, cancellationToken);
        await SeedFiveMaterialsScenarioAsync(dbContext, cancellationToken);
        await SeedFolderBoundaryScenariosAsync(dbContext, cancellationToken);
        await SeedMaterialBoundaryScenariosAsync(dbContext, cancellationToken);

        logger.LogInformation("Stress seed {Version} completed in {Elapsed}", SEEDER_VERSION, stopwatch.Elapsed);
    }

    private async Task EnsureSectionsAndRootsAsync(MnemoraDbContext dbContext, CancellationToken cancellationToken)
    {
        string pattern = SECTION_PREFIX + "%";
        int existingCount = await dbContext.Sections
            .FromSqlInterpolated($"SELECT * FROM sections WHERE name LIKE {pattern}")
            .AsNoTracking()
            .CountAsync(cancellationToken);

        if (existingCount >= TARGET_COUNT)
        {
            logger.LogInformation("Stress V5 sections already exist: {Count}", existingCount);
            return;
        }

        SectionColor sectionColor = Enum.GetValues<SectionColor>()[0];
        SectionIcon sectionIcon = Enum.GetValues<SectionIcon>()[0];

        for (int start = existingCount; start < TARGET_COUNT; start += BATCH_SIZE)
        {
            int end = Math.Min(start + BATCH_SIZE, TARGET_COUNT);
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
            logger.LogInformation("Stress V5 sections + roots: {Done}/{Total}", end, TARGET_COUNT);
        }
    }

    private async Task SeedFolderPagingScenarioAsync(MnemoraDbContext dbContext, CancellationToken cancellationToken)
    {
        Section section = await GetSectionAsync(dbContext, FOLDERS_SECTION_NAME, cancellationToken);
        LibraryContainer root = await GetRootAsync(dbContext, section, cancellationToken);
        await EnsureFoldersAsync(dbContext, root, TARGET_COUNT, "STRESS V5 Папка", "50k folders", cancellationToken);
    }

    private async Task SeedRootMaterialsScenarioAsync(MnemoraDbContext dbContext, CancellationToken cancellationToken)
    {
        Section section = await GetSectionAsync(dbContext, ROOT_MATERIALS_SECTION_NAME, cancellationToken);
        LibraryContainer root = await GetRootAsync(dbContext, section, cancellationToken);
        await EnsureArticlesAsync(dbContext, root, TARGET_COUNT, "STRESS V5 Материал root", "50k root materials", cancellationToken);
    }

    private async Task SeedMixedScenarioAsync(MnemoraDbContext dbContext, CancellationToken cancellationToken)
    {
        Section section = await GetSectionAsync(dbContext, MIXED_SECTION_NAME, cancellationToken);
        LibraryContainer root = await GetRootAsync(dbContext, section, cancellationToken);
        await EnsureFoldersAsync(dbContext, root, MIXED_FOLDER_COUNT, "STRESS V5 Mixed Папка", "mixed folders", cancellationToken);
        await EnsureArticlesAsync(dbContext, root, MIXED_MATERIAL_COUNT, "STRESS V5 Mixed Материал", "mixed materials", cancellationToken);
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

        await EnsureArticlesAsync(
            dbContext,
            current,
            materialCount,
            $"STRESS V5 L{depth} Материал",
            $"depth {depth} materials",
            cancellationToken);
    }

    private async Task SeedTypesScenarioAsync(MnemoraDbContext dbContext, CancellationToken cancellationToken)
    {
        Section section = await GetSectionAsync(dbContext, TYPES_SECTION_NAME, cancellationToken);
        LibraryContainer root = await GetRootAsync(dbContext, section, cancellationToken);

        await EnsureArticlesAsync(dbContext, root, TYPE_COUNT, "STRESS V5 Статья", "type articles", cancellationToken);
        await EnsureStandaloneQuestionsAsync(dbContext, root, TYPE_COUNT, "STRESS V5 Вопрос", "standalone questions", cancellationToken);
        await EnsureLinkedQuestionsAsync(dbContext, root, LINKED_QUESTION_COUNT, cancellationToken);
    }

    private async Task SeedOneFolderScenarioAsync(MnemoraDbContext dbContext, CancellationToken cancellationToken)
    {
        Section section = await GetSectionAsync(dbContext, ONE_FOLDER_SECTION_NAME, cancellationToken);
        LibraryContainer root = await GetRootAsync(dbContext, section, cancellationToken);
        await EnsureFoldersAsync(dbContext, root, 1, "Одна папка", "one folder", cancellationToken);
    }

    private async Task SeedFiveMaterialsScenarioAsync(MnemoraDbContext dbContext, CancellationToken cancellationToken)
    {
        Section section = await GetSectionAsync(dbContext, FIVE_MATERIALS_SECTION_NAME, cancellationToken);
        LibraryContainer root = await GetRootAsync(dbContext, section, cancellationToken);
        await EnsureArticlesAsync(dbContext, root, 5, "STRESS V5 Пять материалов", "five materials", cancellationToken);
    }

    private async Task SeedFolderBoundaryScenariosAsync(MnemoraDbContext dbContext, CancellationToken cancellationToken)
    {
        await SeedFolderBoundaryScenarioAsync(dbContext, FOLDERS29_SECTION_NAME, 29, cancellationToken);
        await SeedFolderBoundaryScenarioAsync(dbContext, FOLDERS30_SECTION_NAME, 30, cancellationToken);
        await SeedFolderBoundaryScenarioAsync(dbContext, FOLDERS31_SECTION_NAME, 31, cancellationToken);
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
        await SeedMaterialBoundaryScenarioAsync(dbContext, MATERIALS29_SECTION_NAME, 29, cancellationToken);
        await SeedMaterialBoundaryScenarioAsync(dbContext, MATERIALS30_SECTION_NAME, 30, cancellationToken);
        await SeedMaterialBoundaryScenarioAsync(dbContext, MATERIALS31_SECTION_NAME, 31, cancellationToken);
    }

    private async Task SeedMaterialBoundaryScenarioAsync(
        MnemoraDbContext dbContext,
        string sectionName,
        int count,
        CancellationToken cancellationToken)
    {
        Section section = await GetSectionAsync(dbContext, sectionName, cancellationToken);
        LibraryContainer root = await GetRootAsync(dbContext, section, cancellationToken);
        await EnsureArticlesAsync(dbContext, root, count, $"STRESS V5 Boundary {count} Материал", $"{count} materials", cancellationToken);
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

        for (int start = existingCount; start < targetCount; start += BATCH_SIZE)
        {
            int end = Math.Min(start + BATCH_SIZE, targetCount);
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

        for (int start = existingCount; start < targetCount; start += BATCH_SIZE)
        {
            int end = Math.Min(start + BATCH_SIZE, targetCount);
            var batch = new List<Article>(end - start);

            for (int index = start; index < end; index++)
            {
                Article article = Article.CreateInContainer(
                    targetContainer.Id,
                    MaterialTitle.Create($"{titlePrefix} {index:D5}").Value,
                    MaterialDifficulty.MEDIUM,
                    MaterialIcon.DefaultArticle,
                    CreateRewards(),
                    Array.Empty<MaterialTag>()).Value;
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

        for (int start = existingCount; start < targetCount; start += BATCH_SIZE)
        {
            int end = Math.Min(start + BATCH_SIZE, targetCount);
            var batch = new List<Question>(end - start);

            for (int index = start; index < end; index++)
            {
                Question question = Question.CreateStandaloneInContainer(
                    targetContainer.Id,
                    MaterialTitle.Create($"{titlePrefix} {index:D5}").Value,
                    MaterialDifficulty.MEDIUM,
                    MaterialIcon.DefaultQuestion,
                    CreateRewards(),
                    Array.Empty<MaterialTag>()).Value;
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

        for (int start = existingCount; start < targetCount; start += BATCH_SIZE)
        {
            int batchCount = Math.Min(BATCH_SIZE, targetCount - start);
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
                    MaterialDifficulty.MEDIUM,
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

    private static MaterialExperienceRewards CreateRewards() =>
        MaterialExperienceRewards.Create(50, 20).Value;

    private static string GetSectionName(int index) => index switch
    {
        0 => FOLDERS_SECTION_NAME,
        1 => ROOT_MATERIALS_SECTION_NAME,
        2 => MIXED_SECTION_NAME,
        3 => DEPTH1_SECTION_NAME,
        4 => DEPTH2_SECTION_NAME,
        5 => DEPTH3_SECTION_NAME,
        6 => TYPES_SECTION_NAME,
        7 => EMPTY_SECTION_NAME,
        8 => ONE_FOLDER_SECTION_NAME,
        9 => FIVE_MATERIALS_SECTION_NAME,
        10 => FOLDERS29_SECTION_NAME,
        11 => FOLDERS30_SECTION_NAME,
        12 => FOLDERS31_SECTION_NAME,
        13 => MATERIALS29_SECTION_NAME,
        14 => MATERIALS30_SECTION_NAME,
        15 => MATERIALS31_SECTION_NAME,
        _ => $"{SECTION_PREFIX}Раздел {index:D5}",
    };
}
