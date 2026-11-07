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

            // Include root containers and every nested folder. The old query
            // selected only Depth == 1, losing nested and root materials.
            var containers = await readDbContext.LibraryContainersRead
                .OrderBy(container => container.Depth)
                .ThenBy(container => container.DisplayOrder)
                .ThenBy(container => container.CreatedAt)
                .ToListAsync(cancellationToken);

            var sectionIdByContainerId = containers.ToDictionary(
                container => container.Id.Value,
                container => container.SectionId.Value);

            var materials = await readDbContext.MaterialsRead
                .OrderBy(material => material.CreatedAt)
                .ToListAsync(cancellationToken);

            var lastContainerActivityBySection = containers
                .GroupBy(container => container.SectionId.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.Max(container => container.UpdatedAt));

            var lastMaterialActivityBySection = materials
                .Where(material => sectionIdByContainerId.ContainsKey(material.ContainerId.Value))
                .GroupBy(material => sectionIdByContainerId[material.ContainerId.Value])
                .ToDictionary(
                    group => group.Key,
                    group => group.Max(material => material.UpdatedAt));

            var materialsByContainer = materials
                .GroupBy(material => material.ContainerId.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(MapMaterial).ToArray());

            // The snapshot is flat by design: ParentId and Depth preserve
            // nesting without duplicating the same material in ancestor folders.
            var foldersBySection = containers
                .Where(container => container.Depth > 0)
                .GroupBy(container => container.SectionId.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(folder => new LibraryFolderSnapshotDto(
                            folder.Id.Value,
                            folder.ParentId!.Value,
                            folder.Depth,
                            folder.Name!.Value,
                            folder.Color!.Value.ToStableName(),
                            folder.Icon!.Value.ToStableName(),
                            folder.DisplayOrder,
                            folder.CreatedAt,
                            folder.UpdatedAt)
                        {
                            Materials = materialsByContainer.GetValueOrDefault(folder.Id.Value, [])
                        })
                        .ToArray());

            var rootMaterialsBySection = containers
                .Where(container => container.Depth == 0)
                .ToDictionary(
                    container => container.SectionId.Value,
                    container => (IReadOnlyList<LibraryMaterialDto>)
                        materialsByContainer.GetValueOrDefault(container.Id.Value, []));

            var result = sections
                .Select(section => new LibrarySectionDto(
                    section.Id.Value,
                    section.Name.Value,
                    section.Color.ToStableName(),
                    section.Icon.ToStableName(),
                    section.CreatedAt,
                    section.UpdatedAt,
                    GetLastActivityAt(
                        section,
                        lastContainerActivityBySection,
                        lastMaterialActivityBySection),
                    foldersBySection.GetValueOrDefault(section.Id.Value, []),
                    rootMaterialsBySection.GetValueOrDefault(section.Id.Value, [])))
                .ToArray();

            return Result.Success<IReadOnlyList<LibrarySectionDto>, Errors>(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            InfoLog.LibraryRetrievalCancelled(logger);

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
        IReadOnlyDictionary<Guid, DateTime> lastContainerActivityBySection,
        IReadOnlyDictionary<Guid, DateTime> lastMaterialActivityBySection)
    {
        var lastActivityAt = section.UpdatedAt;

        if (lastContainerActivityBySection.TryGetValue(section.Id.Value, out var lastContainerActivityAt) &&
            lastContainerActivityAt > lastActivityAt)
        {
            lastActivityAt = lastContainerActivityAt;
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
            material.Type.ToStableName(),
            material.Difficulty.ToStableName(),
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