using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetManagementSectionsPage;

public sealed class GetLibraryManagementSectionsPageQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetLibraryManagementSectionsPageQueryHandler> logger)
    : IQueryHandler<LibraryManagementSectionsPageDto, GetLibraryManagementSectionsPageQuery>
{
    public async Task<Result<LibraryManagementSectionsPageDto, Errors>> Handle(
        GetLibraryManagementSectionsPageQuery request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IQueryable<Section> sectionsQuery = readDbContext.SectionsRead;
            string? search = request.Search?.Trim();

            if (!string.IsNullOrEmpty(search))
            {
                sectionsQuery = sectionsQuery.Where(section =>
                    MnemoraDbFunctions.UnicodeContains(
                        EF.Property<string>(section, nameof(Section.Name)),
                        search));
            }

            int totalCount = await sectionsQuery.CountAsync(cancellationToken);

            var sectionActivities = readDbContext.SectionsRead
                .Select(section => new
                {
                    SectionId = section.Id,
                    ActivityAt = section.UpdatedAt,
                });

            var topicActivities = readDbContext.TopicsRead
                .Select(topic => new
                {
                    SectionId = topic.SectionId,
                    ActivityAt = topic.UpdatedAt,
                });

            var materialActivities =
                from material in readDbContext.MaterialsRead
                join topic in readDbContext.TopicsRead on material.TopicId equals topic.Id
                select new
                {
                    SectionId = topic.SectionId,
                    ActivityAt = material.UpdatedAt,
                };

            var lastActivities = sectionActivities
                .Concat(topicActivities)
                .Concat(materialActivities)
                .GroupBy(activity => activity.SectionId)
                .Select(group => new
                {
                    SectionId = group.Key,
                    LastActivityAt = group.Max(activity => activity.ActivityAt),
                });

            var sectionRows =
                from section in sectionsQuery
                join activity in lastActivities on section.Id equals activity.SectionId
                select new
                {
                    Section = section,
                    activity.LastActivityAt,
                };

            var orderedRows = request.Sort switch
            {
                LibraryManagementSectionSort.Custom => sectionRows
                    .OrderBy(row => row.Section.DisplayOrder)
                    .ThenBy(row => row.Section.CreatedAt)
                    .ThenBy(row => row.Section.Id),

                LibraryManagementSectionSort.Name => sectionRows
                    .OrderBy(row => row.Section.Name)
                    .ThenBy(row => row.Section.Id),

                LibraryManagementSectionSort.Newest => sectionRows
                    .OrderByDescending(row => row.Section.CreatedAt)
                    .ThenBy(row => row.Section.Id),

                LibraryManagementSectionSort.RecentActivity => sectionRows
                    .OrderByDescending(row => row.LastActivityAt)
                    .ThenBy(row => row.Section.Id),

                _ => sectionRows
                    .OrderBy(row => row.Section.DisplayOrder)
                    .ThenBy(row => row.Section.CreatedAt)
                    .ThenBy(row => row.Section.Id),
            };

            var loadedRows = await orderedRows
                .Skip(request.Offset)
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);

            bool hasMore = loadedRows.Count > request.PageSize;
            var pageRows = loadedRows.Take(request.PageSize).ToArray();
            var sectionIds = pageRows.Select(row => row.Section.Id).ToArray();

            var topics = sectionIds.Length == 0
                ? []
                : await readDbContext.TopicsRead
                    .Where(topic => sectionIds.Contains(topic.SectionId))
                    .ToListAsync(cancellationToken);

            var topicIds = topics.Select(topic => topic.Id).ToArray();

            var articleTopicIds = topicIds.Length == 0
                ? []
                : await readDbContext.MaterialsRead
                    .OfType<Article>()
                    .Where(article => topicIds.Contains(article.TopicId))
                    .Select(article => article.TopicId)
                    .ToListAsync(cancellationToken);

            var questionTopicIds = topicIds.Length == 0
                ? []
                : await readDbContext.MaterialsRead
                    .OfType<Question>()
                    .Where(question => topicIds.Contains(question.TopicId))
                    .Select(question => question.TopicId)
                    .ToListAsync(cancellationToken);

            var sectionIdByTopicId = topics.ToDictionary(
                topic => topic.Id,
                topic => topic.SectionId);

            var topicsCountBySection = topics
                .GroupBy(topic => topic.SectionId)
                .ToDictionary(group => group.Key, group => group.Count());

            var articlesCountBySection = articleTopicIds
                .GroupBy(topicId => sectionIdByTopicId[topicId])
                .ToDictionary(group => group.Key, group => group.Count());

            var questionsCountBySection = questionTopicIds
                .GroupBy(topicId => sectionIdByTopicId[topicId])
                .ToDictionary(group => group.Key, group => group.Count());

            var items = pageRows
                .Select(row =>
                {
                    int topicsCount = topicsCountBySection.GetValueOrDefault(row.Section.Id);
                    int articlesCount = articlesCountBySection.GetValueOrDefault(row.Section.Id);
                    int questionsCount = questionsCountBySection.GetValueOrDefault(row.Section.Id);

                    return new LibrarySectionOverviewDto(
                        row.Section.Id.Value,
                        row.Section.Name.Value,
                        row.Section.Color.ToString(),
                        row.Section.Icon.ToString(),
                        row.Section.CreatedAt,
                        row.Section.UpdatedAt,
                        row.LastActivityAt,
                        topicsCount,
                        articlesCount + questionsCount,
                        articlesCount,
                        questionsCount);
                })
                .ToArray();

            var result = new LibraryManagementSectionsPageDto(
                items,
                request.Offset + items.Length,
                hasMore,
                totalCount);

            return Result.Success<LibraryManagementSectionsPageDto, Errors>(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Получение страницы разделов управления было отменено");
            return CommonErrors.OperationCancelled(
                "library.management.sections.page.cancelled").ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось получить страницу разделов управления");
            return CommonErrors.Db(
                "library.management.sections.page.failed",
                "Не удалось загрузить разделы").ToErrors();
        }
    }
}
