using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetSectionsPage;

public sealed class GetLibrarySectionsPageQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetLibrarySectionsPageQueryHandler> logger)
    : IQueryHandler<LibrarySectionsPageDto, GetLibrarySectionsPageQuery>
{
    public async Task<Result<LibrarySectionsPageDto, Errors>> Handle(
        GetLibrarySectionsPageQuery request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            int offset = Math.Max(0, request.Offset);
            int pageSize = Math.Clamp(
                request.PageSize,
                1,
                LibraryPagingDefaults.MaxQueryPageSize);

            IQueryable<Section> sectionsQuery =
                readDbContext.SectionsRead;

            string? search = request.Search?.Trim();

            if (!string.IsNullOrEmpty(search))
            {
                sectionsQuery = sectionsQuery.Where(section =>
                    MnemoraDbFunctions.UnicodeContains(
                        EF.Property<string>(section, nameof(Section.Name)),
                        search));
            }

            int totalCount =
                await sectionsQuery.CountAsync(cancellationToken);

            var sectionActivities = readDbContext.SectionsRead
                .Select(section => new
                {
                    SectionId = section.Id,
                    ActivityAt = section.UpdatedAt,
                });

            var containerActivities =
                readDbContext.LibraryContainersRead
                    .Select(container => new
                    {
                        container.SectionId,
                        ActivityAt = container.UpdatedAt,
                    });

            var materialActivities =
                from material in readDbContext.MaterialsRead
                join container in readDbContext.LibraryContainersRead
                    on material.ContainerId equals container.Id
                select new
                {
                    container.SectionId,
                    ActivityAt = material.UpdatedAt,
                };

            var lastActivities = sectionActivities
                .Concat(containerActivities)
                .Concat(materialActivities)
                .GroupBy(activity => activity.SectionId)
                .Select(group => new
                {
                    SectionId = group.Key,
                    LastActivityAt = group.Max(activity => activity.ActivityAt),
                });

            var sectionRows =
                from section in sectionsQuery
                join activity in lastActivities
                    on section.Id equals activity.SectionId
                select new
                {
                    Section = section,
                    activity.LastActivityAt,
                };

            var orderedRows = request.Sort switch
            {
                LibrarySectionSort.Name => sectionRows
                    .OrderBy(row => row.Section.Name)
                    .ThenBy(row => row.Section.Id),

                LibrarySectionSort.Newest => sectionRows
                    .OrderByDescending(row => row.Section.CreatedAt)
                    .ThenBy(row => row.Section.Id),

                LibrarySectionSort.RecentActivity => sectionRows
                    .OrderByDescending(row => row.LastActivityAt)
                    .ThenBy(row => row.Section.Id),

                _ => sectionRows
                    .OrderBy(row => row.Section.Name)
                    .ThenBy(row => row.Section.Id),
            };

            var loadedRows = await orderedRows
                .Skip(offset)
                .Take(pageSize + 1)
                .ToListAsync(cancellationToken);

            bool hasMore = loadedRows.Count > pageSize;
            var pageRows = loadedRows.Take(pageSize).ToArray();
            SectionId[] sectionIds =
                pageRows.Select(row => row.Section.Id).ToArray();

            Dictionary<SectionId, LibraryContainerId>
                rootIdsBySection =
                    sectionIds.Length == 0
                        ? []
                        : await readDbContext.LibraryContainersRead
                            .Where(container =>
                                container.ParentId == null &&
                                sectionIds.Contains(container.SectionId))
                            .ToDictionaryAsync(
                                container => container.SectionId,
                                container => container.Id,
                                cancellationToken);

            Dictionary<SectionId, int> foldersCountBySection =
                sectionIds.Length == 0
                    ? []
                    : await readDbContext.LibraryContainersRead
                        .Where(container =>
                            container.ParentId != null &&
                            sectionIds.Contains(container.SectionId))
                        .GroupBy(container => container.SectionId)
                        .Select(group => new
                        {
                            SectionId = group.Key,
                            Count = group.Count(),
                        })
                        .ToDictionaryAsync(
                            row => row.SectionId,
                            row => row.Count,
                            cancellationToken);

            // Временный legacy-счётчик нужен старому UI до перехода на folders.
            Dictionary<SectionId, int> topicsCountBySection =
                sectionIds.Length == 0
                    ? []
                    : await readDbContext.TopicsRead
                        .Where(topic => sectionIds.Contains(topic.SectionId))
                        .GroupBy(topic => topic.SectionId)
                        .Select(group => new
                        {
                            SectionId = group.Key,
                            Count = group.Count(),
                        })
                        .ToDictionaryAsync(
                            row => row.SectionId,
                            row => row.Count,
                            cancellationToken);

            Dictionary<SectionId, int> articlesCountBySection =
                sectionIds.Length == 0
                    ? []
                    : await (
                        from article in readDbContext.MaterialsRead.OfType<Article>()
                        join container in readDbContext.LibraryContainersRead
                            on article.ContainerId equals container.Id
                        where sectionIds.Contains(container.SectionId)
                        group article by container.SectionId
                        into grouped
                        select new
                        {
                            SectionId = grouped.Key,
                            Count = grouped.Count(),
                        })
                        .ToDictionaryAsync(
                            row => row.SectionId,
                            row => row.Count,
                            cancellationToken);

            Dictionary<SectionId, int> questionsCountBySection =
                sectionIds.Length == 0
                    ? []
                    : await (
                        from question in readDbContext.MaterialsRead.OfType<Question>()
                        join container in readDbContext.LibraryContainersRead
                            on question.ContainerId equals container.Id
                        where sectionIds.Contains(container.SectionId) &&
                              question.ArticleId == null
                        group question by container.SectionId
                        into grouped
                        select new
                        {
                            SectionId = grouped.Key,
                            Count = grouped.Count(),
                        })
                        .ToDictionaryAsync(
                            row => row.SectionId,
                            row => row.Count,
                            cancellationToken);

            LibrarySectionOverviewDto[] items = pageRows
                .Select(row =>
                {
                    if (!rootIdsBySection.TryGetValue(
                            row.Section.Id,
                            out LibraryContainerId? rootId))
                    {
                        throw new InvalidOperationException(
                            $"Для раздела '{row.Section.Id.Value}' не найден root-контейнер библиотеки.");
                    }

                    int foldersCount =
                        foldersCountBySection.GetValueOrDefault(row.Section.Id);
                    int topicsCount =
                        topicsCountBySection.GetValueOrDefault(row.Section.Id);
                    int articlesCount =
                        articlesCountBySection.GetValueOrDefault(row.Section.Id);
                    int questionsCount =
                        questionsCountBySection.GetValueOrDefault(row.Section.Id);

                    return new LibrarySectionOverviewDto(
                        row.Section.Id.Value,
                        rootId!.Value,
                        row.Section.Name.Value,
                        row.Section.Color.ToString(),
                        row.Section.Icon.ToString(),
                        row.Section.CreatedAt,
                        row.Section.UpdatedAt,
                        row.LastActivityAt,
                        foldersCount,
                        topicsCount,
                        articlesCount + questionsCount,
                        articlesCount,
                        questionsCount);
                })
                .ToArray();

            return Result.Success<LibrarySectionsPageDto, Errors>(
                new LibrarySectionsPageDto(
                    items,
                    offset + items.Length,
                    hasMore,
                    totalCount));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Получение страницы разделов было отменено");

            return CommonErrors.OperationCancelled(
                    "library.sections.page.cancelled")
                .ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить страницу разделов");

            return CommonErrors.Db(
                    "library.sections.page.failed",
                    "Не удалось загрузить разделы")
                .ToErrors();
        }
    }
}
