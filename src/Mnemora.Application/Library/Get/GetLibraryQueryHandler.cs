using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Domain.Materials;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.Get;

public sealed class GetLibraryQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetLibraryQueryHandler> logger)
    : IQueryHandler<IReadOnlyList<LibrarySectionDto>, GetLibraryQuery>
{
    public async Task<Result<IReadOnlyList<LibrarySectionDto>, Errors>> Handle(
        GetLibraryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sections = await readDbContext.SectionsRead
                .OrderBy(section => section.CreatedAt)
                .ToListAsync(cancellationToken);

            if (sections.Count == 0)
            {
                return Result.Success<IReadOnlyList<LibrarySectionDto>, Errors>(
                    Array.Empty<LibrarySectionDto>());
            }

            var topics = await readDbContext.TopicsRead
                .OrderBy(topic => topic.CreatedAt)
                .ToListAsync(cancellationToken);

            var materials = await readDbContext.MaterialsRead
                .OrderBy(material => material.CreatedAt)
                .ToListAsync(cancellationToken);

            var materialsByTopic = materials
                .GroupBy(material => material.TopicId.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(MapMaterial).ToArray());

            var topicsBySection = topics
                .GroupBy(topic => topic.SectionId.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(topic => new LibraryTopicDto(
                            topic.Id.Value,
                            topic.Name.Value,
                            topic.Color.ToString(),
                            topic.Icon.ToString(),
                            topic.CreatedAt)
                        {
                            Materials = materialsByTopic.GetValueOrDefault(topic.Id.Value, [])
                        })
                        .ToArray());

            var result = sections
                .Select(section => new LibrarySectionDto(
                    section.Id.Value,
                    section.Name.Value,
                    section.Color.ToString(),
                    section.Icon.ToString(),
                    section.CreatedAt,
                    topicsBySection.GetValueOrDefault(section.Id.Value, [])))
                .ToArray();

            return Result.Success<IReadOnlyList<LibrarySectionDto>, Errors>(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Получение библиотеки было отменено");

            return CommonErrors.OperationCancelled(
                "library.get.cancelled").ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось получить библиотеку");

            return CommonErrors.Db(
                "library.get.failed",
                "Не удалось загрузить библиотеку").ToErrors();
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