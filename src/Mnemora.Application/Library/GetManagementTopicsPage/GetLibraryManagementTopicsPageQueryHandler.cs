using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetManagementTopicsPage;

public sealed class GetLibraryManagementTopicsPageQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetLibraryManagementTopicsPageQueryHandler> logger)
    : IQueryHandler<LibraryManagementTopicsPageDto, GetLibraryManagementTopicsPageQuery>
{
    public async Task<Result<LibraryManagementTopicsPageDto, Errors>> Handle(
        GetLibraryManagementTopicsPageQuery request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            int offset = Math.Max(0, request.Offset);
            int pageSize = Math.Clamp(
                request.PageSize,
                1,
                LibraryPagingDefaults.MaxQueryPageSize);

            var sectionIdResult =
                SectionId.Create(request.SectionId);

            if (sectionIdResult.IsFailure)
            {
                return sectionIdResult.Error.ToErrors();
            }

            IQueryable<Topic> topicsQuery =
                readDbContext.TopicsRead
                    .Where(topic =>
                        topic.SectionId == sectionIdResult.Value);

            string? search = request.Search?.Trim();

            if (!string.IsNullOrEmpty(search))
            {
                topicsQuery = topicsQuery.Where(topic =>
                    MnemoraDbFunctions.UnicodeContains(
                        EF.Property<string>(
                            topic,
                            nameof(Topic.Name)),
                        search));
            }

            int totalCount = await topicsQuery.CountAsync(
                cancellationToken);

            // Nullable MAX is intentional: a topic may legitimately have no
            // materials. COALESCE keeps LastActivityAt non-null in SQL and
            // avoids materializing NULL into DateTime.
            var rows =
                from topic in topicsQuery
                let lastMaterialActivityAt =
                    readDbContext.MaterialsRead
                        .Where(material =>
                            material.TopicId == topic.Id)
                        .Max(material =>
                            (DateTime?)material.UpdatedAt)
                let materialOrTopicActivityAt =
                    lastMaterialActivityAt ?? topic.UpdatedAt
                let lastActivityAt =
                    topic.UpdatedAt >= materialOrTopicActivityAt
                        ? topic.UpdatedAt
                        : materialOrTopicActivityAt
                select new
                {
                    Topic = topic,
                    LastActivityAt = lastActivityAt,
                };

            var orderedRows = request.Sort switch
            {
                LibraryManagementTopicPageSort.Custom => rows
                    .OrderBy(row => row.Topic.DisplayOrder)
                    .ThenBy(row => row.Topic.CreatedAt)
                    .ThenBy(row => row.Topic.Id),

                LibraryManagementTopicPageSort.RecentActivity => rows
                    .OrderByDescending(row => row.LastActivityAt)
                    .ThenBy(row => row.Topic.Id),

                LibraryManagementTopicPageSort.Name => rows
                    .OrderBy(row => row.Topic.Name)
                    .ThenBy(row => row.Topic.Id),

                LibraryManagementTopicPageSort.Newest => rows
                    .OrderByDescending(row => row.Topic.CreatedAt)
                    .ThenBy(row => row.Topic.Id),

                _ => rows
                    .OrderBy(row => row.Topic.DisplayOrder)
                    .ThenBy(row => row.Topic.CreatedAt)
                    .ThenBy(row => row.Topic.Id),
            };

            var loadedRows = await orderedRows
                .Skip(offset)
                .Take(pageSize + 1)
                .ToListAsync(cancellationToken);

            bool hasMore = loadedRows.Count > pageSize;
            var pageRows = loadedRows
                .Take(pageSize)
                .ToArray();

            TopicId[] topicIds = pageRows
                .Select(row => row.Topic.Id)
                .ToArray();

            Dictionary<TopicId, int> articleCounts =
                topicIds.Length == 0
                    ? []
                    : await readDbContext.MaterialsRead
                        .OfType<Article>()
                        .Where(article =>
                            topicIds.Contains(article.TopicId))
                        .GroupBy(article => article.TopicId)
                        .Select(group => new
                        {
                            TopicId = group.Key,
                            Count = group.Count(),
                        })
                        .ToDictionaryAsync(
                            row => row.TopicId,
                            row => row.Count,
                            cancellationToken);

            Dictionary<TopicId, int> questionCounts =
                topicIds.Length == 0
                    ? []
                    : await readDbContext.MaterialsRead
                        .OfType<Question>()
                        .Where(question =>
                            topicIds.Contains(question.TopicId) &&
                            question.ArticleId == null)
                        .GroupBy(question => question.TopicId)
                        .Select(group => new
                        {
                            TopicId = group.Key,
                            Count = group.Count(),
                        })
                        .ToDictionaryAsync(
                            row => row.TopicId,
                            row => row.Count,
                            cancellationToken);

            var items = pageRows
                .Select(row =>
                {
                    int articlesCount =
                        articleCounts.GetValueOrDefault(
                            row.Topic.Id);

                    int questionsCount =
                        questionCounts.GetValueOrDefault(
                            row.Topic.Id);

                    return new LibraryManagementTopicOverviewDto(
                        row.Topic.Id.Value,
                        row.Topic.SectionId.Value,
                        row.Topic.Name.Value,
                        row.Topic.Color.ToString(),
                        row.Topic.Icon.ToString(),
                        row.Topic.CreatedAt,
                        row.Topic.UpdatedAt,
                        row.LastActivityAt,
                        row.Topic.DisplayOrder,
                        articlesCount + questionsCount,
                        articlesCount,
                        questionsCount);
                })
                .ToArray();

            return Result.Success<LibraryManagementTopicsPageDto, Errors>(
                new LibraryManagementTopicsPageDto(
                    items,
                    offset + items.Length,
                    hasMore,
                    totalCount));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Получение страницы тем управления было отменено");

            return CommonErrors.OperationCancelled(
                "library.management.topics.page.cancelled")
                .ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить страницу тем управления");

            return CommonErrors.Db(
                "library.management.topics.page.failed",
                "Не удалось загрузить темы")
                .ToErrors();
        }
    }
}
