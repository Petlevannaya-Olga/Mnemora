using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
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
        var containerIdResult = LibraryContainerId.Create(request.ContainerId);

        if (containerIdResult.IsFailure)
        {
            return containerIdResult.Error.ToErrors();
        }

        LibraryContainerId containerId = containerIdResult.Value;

        try
        {
            var containerRow = await (
                    from containerEntity in readDbContext.LibraryContainersRead
                    join sectionEntity in readDbContext.SectionsRead
                        on containerEntity.SectionId equals sectionEntity.Id
                    where containerEntity.Id == containerId
                    select new
                    {
                        Container = containerEntity,
                        Section = sectionEntity,
                    })
                .SingleOrDefaultAsync(cancellationToken);

            if (containerRow is null)
            {
                return CommonErrors.NotFound(
                    "library.container.not.found",
                    $"Контейнер библиотеки с идентификатором '{request.ContainerId}' не найден")
                    .ToErrors();
            }

            IQueryable<Material> materialsQuery = readDbContext.MaterialsRead
                .Where(material => material.ContainerId == containerId)
                .Where(material =>
                    material is Article ||
                    (material is Question && ((Question)material).ArticleId == null));

            materialsQuery = request.Filter switch
            {
                LibraryMaterialFilter.Articles => materialsQuery.OfType<Article>(),
                LibraryMaterialFilter.Questions => materialsQuery.OfType<Question>(),
                _ => materialsQuery,
            };

            string? search = request.Search?.Trim();

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
                LibraryMaterialSort.Custom => materialsQuery
                    .OrderBy(material => material.DisplayOrder)
                    .ThenBy(material => material.Title)
                    .ThenBy(material => material.Id),

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
                    .ThenBy(material => material.Id),
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

            LibraryContainer container = containerRow.Container;
            var section = containerRow.Section;

            string displayName = container.IsRoot
                ? section.Name.Value
                : container.Name!.Value;

            string displayColor = container.IsRoot
                ? section.Color.ToString()
                : container.Color!.Value.ToString();

            string displayIcon = container.IsRoot
                ? section.Icon.ToString()
                : container.Icon!.Value.ToString();

            // Compatibility header for the current Desktop view model.
            // Its values now describe the current container, not necessarily a legacy Topic.
            var compatibilityTopicHeader = new LibraryTopicHeaderDto(
                container.Id.Value,
                section.Id.Value,
                section.Name.Value,
                displayName,
                displayColor,
                displayIcon,
                container.CreatedAt,
                container.UpdatedAt);

            var containerHeader = new LibraryContainerHeaderDto(
                container.Id.Value,
                section.Id.Value,
                section.Name.Value,
                container.ParentId?.Value,
                container.Depth,
                displayName,
                displayColor,
                displayIcon,
                container.CreatedAt,
                container.UpdatedAt);

            var result = new LibraryMaterialsPageDto(
                compatibilityTopicHeader,
                items,
                request.Offset + items.Length,
                hasMore,
                totalCount)
            {
                Container = containerHeader,
            };

            return Result.Success<LibraryMaterialsPageDto, Errors>(result);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Получение страницы материалов контейнера {ContainerId} было отменено",
                request.ContainerId);

            return CommonErrors.OperationCancelled(
                "library.materials.page.cancelled").ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить страницу материалов контейнера {ContainerId}",
                request.ContainerId);

            return CommonErrors.Db(
                "library.materials.page.failed",
                "Не удалось загрузить материалы").ToErrors();
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
            material.UpdatedAt)
        {
            ContainerId = material.ContainerId.Value,
        };
    }
}
