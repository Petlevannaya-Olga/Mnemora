using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetTopicsPage;

public sealed class GetLibraryTopicsPageQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetLibraryTopicsPageQueryHandler> logger)
    : IQueryHandler<LibraryTopicsPageDto, GetLibraryTopicsPageQuery>
{
    public async Task<Result<LibraryTopicsPageDto, Errors>> Handle(
        GetLibraryTopicsPageQuery request,
        CancellationToken cancellationToken = default)
    {
        var sectionIdResult = SectionId.Create(request.SectionId);

        if (sectionIdResult.IsFailure)
        {
            return sectionIdResult.Error.ToErrors();
        }

        var sectionId = sectionIdResult.Value;

        try
        {
            var section = await readDbContext.SectionsRead.SingleOrDefaultAsync(
                section => section.Id == sectionId,
                cancellationToken);

            if (section is null)
            {
                return CommonErrors.NotFound(
                    "library.section.not.found",
                    $"Раздел с идентификатором '{request.SectionId}' не найден").ToErrors();
            }

            var topicsQuery = readDbContext.TopicsRead.Where(topic => topic.SectionId == sectionId);
            var search = request.Search?.Trim();

            if (!string.IsNullOrEmpty(search))
            {
                topicsQuery = topicsQuery.Where(topic =>
                    MnemoraDbFunctions.UnicodeContains(
                        EF.Property<string>(topic, nameof(Topic.Name)),
                        search));
            }

            int totalCount = await topicsQuery.CountAsync(cancellationToken);

            var topicActivities = topicsQuery.Select(topic => new
            {
                TopicId = topic.Id,
                ActivityAt = topic.UpdatedAt
            });

            var materialActivities =
                from material in readDbContext.MaterialsRead
                join topic in topicsQuery on material.TopicId equals topic.Id
                select new
                {
                    TopicId = topic.Id,
                    ActivityAt = material.UpdatedAt
                };

            var lastActivities = topicActivities
                .Concat(materialActivities)
                .GroupBy(activity => activity.TopicId)
                .Select(group => new
                {
                    TopicId = group.Key,
                    LastActivityAt = group.Max(activity => activity.ActivityAt)
                });

            var topicRows =
                from topic in topicsQuery
                join activity in lastActivities on topic.Id equals activity.TopicId
                select new
                {
                    Topic = topic,
                    activity.LastActivityAt
                };

            var orderedRows = request.Sort switch
            {
                LibraryTopicSort.Name => topicRows
                    .OrderBy(row => row.Topic.Name)
                    .ThenBy(row => row.Topic.Id),

                LibraryTopicSort.Newest => topicRows
                    .OrderByDescending(row => row.Topic.CreatedAt)
                    .ThenBy(row => row.Topic.Id),

                LibraryTopicSort.RecentActivity => topicRows
                    .OrderByDescending(row => row.LastActivityAt)
                    .ThenBy(row => row.Topic.Id),

                _ => topicRows
                    .OrderBy(row => row.Topic.Name)
                    .ThenBy(row => row.Topic.Id)
            };

            var loadedRows = await orderedRows
                .Skip(request.Offset)
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);

            bool hasMore = loadedRows.Count > request.PageSize;
            var pageRows = loadedRows.Take(request.PageSize).ToArray();
            var topicIds = pageRows.Select(row => row.Topic.Id).ToArray();

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

            var articlesCountByTopic = articleTopicIds
                .GroupBy(topicId => topicId)
                .ToDictionary(group => group.Key, group => group.Count());

            var questionsCountByTopic = questionTopicIds
                .GroupBy(topicId => topicId)
                .ToDictionary(group => group.Key, group => group.Count());

            var items = pageRows
                .Select(row =>
                {
                    int articlesCount = articlesCountByTopic.GetValueOrDefault(row.Topic.Id);
                    int questionsCount = questionsCountByTopic.GetValueOrDefault(row.Topic.Id);

                    return new LibraryTopicOverviewDto(
                        row.Topic.Id.Value,
                        row.Topic.Name.Value,
                        row.Topic.Color.ToString(),
                        row.Topic.Icon.ToString(),
                        row.Topic.CreatedAt,
                        row.Topic.UpdatedAt,
                        row.LastActivityAt,
                        articlesCount + questionsCount,
                        articlesCount,
                        questionsCount);
                })
                .ToArray();

            var sectionDto = new LibrarySectionHeaderDto(
                section.Id.Value,
                section.Name.Value,
                section.Color.ToString(),
                section.Icon.ToString(),
                section.CreatedAt,
                section.UpdatedAt);

            var result = new LibraryTopicsPageDto(
                sectionDto,
                items,
                request.Offset + items.Length,
                hasMore,
                totalCount);

            return Result.Success<LibraryTopicsPageDto, Errors>(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Получение страницы тем раздела {SectionId} было отменено",
                request.SectionId);

            return CommonErrors.OperationCancelled(
                "library.topics.page.cancelled").ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить страницу тем раздела {SectionId}",
                request.SectionId);

            return CommonErrors.Db(
                "library.topics.page.failed",
                "Не удалось загрузить темы раздела").ToErrors();
        }
    }
}
