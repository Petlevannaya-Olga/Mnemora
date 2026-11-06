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

            var topics = await readDbContext.LibraryContainersRead
                .Where(container => container.Depth == 1)
                .OrderBy(container => container.CreatedAt)
                .ToListAsync(cancellationToken);

            var sectionIdByContainerId = await readDbContext.LibraryContainersRead
                .Select(container => new { container.Id, container.SectionId })
                .ToDictionaryAsync(
                    container => container.Id.Value,
                    container => container.SectionId.Value,
                    cancellationToken);

            var materials = await readDbContext.MaterialsRead
                .OrderBy(material => material.CreatedAt)
                .ToListAsync(cancellationToken);

            var lastTopicActivityBySection = topics
                .GroupBy(topic => topic.SectionId.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.Max(topic => topic.UpdatedAt));

            var lastMaterialActivityBySection = materials
                .Where(material => sectionIdByContainerId.ContainsKey(material.ContainerId.Value))
                .GroupBy(material => sectionIdByContainerId[material.ContainerId.Value])
                .ToDictionary(
                    group => group.Key,
                    group => group.Max(material => material.UpdatedAt));

            var materialsByTopic = materials
                .GroupBy(material => material.ContainerId.Value)
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
                            topic.Name!.Value,
                            topic.Color!.Value.ToString(),
                            topic.Icon!.Value.ToString(),
                            topic.CreatedAt,
                            topic.UpdatedAt)
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
                    section.UpdatedAt,
                    GetLastActivityAt(
                        section,
                        lastTopicActivityBySection,
                        lastMaterialActivityBySection),
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

    private static DateTime GetLastActivityAt(
        Section section,
        IReadOnlyDictionary<Guid, DateTime> lastTopicActivityBySection,
        IReadOnlyDictionary<Guid, DateTime> lastMaterialActivityBySection)
    {
        var lastActivityAt = section.UpdatedAt;

        if (lastTopicActivityBySection.TryGetValue(section.Id.Value, out var lastTopicActivityAt) &&
            lastTopicActivityAt > lastActivityAt)
        {
            lastActivityAt = lastTopicActivityAt;
        }

        if (lastMaterialActivityBySection.TryGetValue(section.Id.Value, out var lastMaterialActivityAt) &&
            lastMaterialActivityAt > lastActivityAt)
        {
            lastActivityAt = lastMaterialActivityAt;
        }

        return lastActivityAt;
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
            material.ContainerId.Value,
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
            material.UpdatedAt)
        {
            ContainerId = material.ContainerId.Value,
        };
    }
}