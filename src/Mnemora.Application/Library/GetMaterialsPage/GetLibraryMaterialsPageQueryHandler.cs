using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Topics;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetMaterialsPage;

public sealed class GetLibraryMaterialsPageQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetLibraryMaterialsPageQueryHandler> logger)
    : IQueryHandler<LibraryMaterialsPageDto, GetLibraryMaterialsPageQuery>
{
    public async Task<Result<LibraryMaterialsPageDto, Errors>> Handle(
        GetLibraryMaterialsPageQuery request,
        CancellationToken cancellationToken = default)
    {
        var topicIdResult = TopicId.Create(request.TopicId);

        if (topicIdResult.IsFailure)
        {
            return topicIdResult.Error.ToErrors();
        }

        var topicId = topicIdResult.Value;

        try
        {
            var topicRow = await (
                    from topic in readDbContext.TopicsRead
                    join section in readDbContext.SectionsRead on topic.SectionId equals section.Id
                    where topic.Id == topicId
                    select new
                    {
                        Topic = topic,
                        Section = section
                    })
                .SingleOrDefaultAsync(cancellationToken);

            if (topicRow is null)
            {
                return CommonErrors.NotFound(
                    "library.topic.not.found",
                    $"Тема с идентификатором '{request.TopicId}' не найдена").ToErrors();
            }

            IQueryable<Material> materialsQuery = readDbContext.MaterialsRead
                .Where(material => material.TopicId == topicId);

            materialsQuery = request.Filter switch
            {
                LibraryMaterialFilter.Articles => materialsQuery.OfType<Article>(),
                LibraryMaterialFilter.Questions => materialsQuery.OfType<Question>(),
                _ => materialsQuery
            };

            var search = request.Search?.Trim();

            if (!string.IsNullOrEmpty(search))
            {
                materialsQuery = materialsQuery.Where(material =>
                    MnemoraDbFunctions.UnicodeContains(
                        EF.Property<string>(material, nameof(Material.Title)),
                        search));
            }

            int totalCount = await materialsQuery.CountAsync(cancellationToken);

            var orderedMaterials = request.Sort switch
            {
                LibraryMaterialSort.RecentlyUpdated => materialsQuery
                    .OrderByDescending(material => material.UpdatedAt)
                    .ThenBy(material => material.Id),

                LibraryMaterialSort.Name => materialsQuery
                    .OrderBy(material => material.Title)
                    .ThenBy(material => material.Id),

                LibraryMaterialSort.Newest => materialsQuery
                    .OrderByDescending(material => material.CreatedAt)
                    .ThenBy(material => material.Id),

                LibraryMaterialSort.Easiest => materialsQuery
                    .OrderBy(material => material.Difficulty)
                    .ThenBy(material => material.Title)
                    .ThenBy(material => material.Id),

                LibraryMaterialSort.Hardest => materialsQuery
                    .OrderByDescending(material => material.Difficulty)
                    .ThenBy(material => material.Title)
                    .ThenBy(material => material.Id),

                _ => materialsQuery
                    .OrderBy(material => material.Title)
                    .ThenBy(material => material.Id)
            };

            var loadedMaterials = await orderedMaterials
                .Include(material => material.Tags)
                .Skip(request.Offset)
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);

            bool hasMore = loadedMaterials.Count > request.PageSize;

            var items = loadedMaterials
                .Take(request.PageSize)
                .Select(MapMaterial)
                .ToArray();

            var topicDto = new LibraryTopicHeaderDto(
                topicRow.Topic.Id.Value,
                topicRow.Section.Id.Value,
                topicRow.Section.Name.Value,
                topicRow.Topic.Name.Value,
                topicRow.Topic.Color.ToString(),
                topicRow.Topic.Icon.ToString(),
                topicRow.Topic.CreatedAt,
                topicRow.Topic.UpdatedAt);

            var result = new LibraryMaterialsPageDto(
                topicDto,
                items,
                request.Offset + items.Length,
                hasMore,
                totalCount);

            return Result.Success<LibraryMaterialsPageDto, Errors>(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Получение страницы материалов темы {TopicId} было отменено",
                request.TopicId);

            return CommonErrors.OperationCancelled(
                "library.materials.page.cancelled").ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить страницу материалов темы {TopicId}",
                request.TopicId);

            return CommonErrors.Db(
                "library.materials.page.failed",
                "Не удалось загрузить материалы темы").ToErrors();
        }
    }

    private static LibraryMaterialDto MapMaterial(Material material)
    {
        Guid? articleId = material is Question question
            ? question.ArticleId?.Value
            : null;

        var tags = material.Tags
            .OrderBy(tag => tag.Value, StringComparer.OrdinalIgnoreCase)
            .Select(tag => tag.Value)
            .ToArray();

        return new LibraryMaterialDto(
            material.Id.Value,
            material.TopicId.Value,
            material.Title.Value,
            material.Type.ToString(),
            material.Difficulty.ToString(),
            material.Icon.Key,
            material.ExperienceRewards.StudyPoints,
            material.ExperienceRewards.ReviewPoints,
            material.LearningRevision,
            tags,
            articleId,
            material.CreatedAt,
            material.UpdatedAt);
    }
}
