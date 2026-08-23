using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetManagementMaterialsPage;

public sealed class GetLibraryManagementMaterialsPageQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetLibraryManagementMaterialsPageQueryHandler> logger)
    : IQueryHandler<LibraryManagementMaterialsPageDto, GetLibraryManagementMaterialsPageQuery>
{
    public async Task<Result<LibraryManagementMaterialsPageDto, Errors>> Handle(
        GetLibraryManagementMaterialsPageQuery request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            int offset = Math.Max(0, request.Offset);
            int pageSize = Math.Clamp(
                request.PageSize,
                1,
                LibraryPagingDefaults.MaxQueryPageSize);

            var containerIdResult = LibraryContainerId.Create(request.ContainerId);

            if (containerIdResult.IsFailure)
            {
                return containerIdResult.Error.ToErrors();
            }

            LibraryContainerId containerId = containerIdResult.Value;

            bool containerExists = await readDbContext.LibraryContainersRead
                .AnyAsync(
                    container => container.Id == containerId,
                    cancellationToken);

            if (!containerExists)
            {
                return CommonErrors.NotFound(
                    "library.container.not.found",
                    $"Контейнер библиотеки с идентификатором '{request.ContainerId}' не найден")
                    .ToErrors();
            }

            // На верхнем уровне списка показываются статьи и самостоятельные вопросы.
            // Связанные вопросы принадлежат статье и не занимают отдельные позиции.
            IQueryable<Material> sourceQuery = readDbContext.MaterialsRead
                .Where(material => material.ContainerId == containerId)
                .Where(material =>
                    material is Article ||
                    (material is Question && ((Question)material).ArticleId == null));

            int sourceTotalCount = await sourceQuery.CountAsync(cancellationToken);

            IQueryable<Material> filteredQuery = sourceQuery;
            string? search = request.Search?.Trim();

            if (!string.IsNullOrEmpty(search))
            {
                filteredQuery = filteredQuery.Where(material =>
                    MnemoraDbFunctions.UnicodeContains(
                        EF.Property<string>(material, nameof(Material.Title)),
                        search));
            }

            filteredQuery = request.Filter switch
            {
                LibraryManagementMaterialPageFilter.Articles =>
                    filteredQuery.Where(material => material is Article),

                LibraryManagementMaterialPageFilter.Questions =>
                    filteredQuery.Where(material => material is Question),

                _ => filteredQuery,
            };

            bool hasSearch = !string.IsNullOrEmpty(search);
            bool hasTypeFilter =
                request.Filter != LibraryManagementMaterialPageFilter.All;

            int totalCount = !hasSearch && !hasTypeFilter
                ? sourceTotalCount
                : await filteredQuery.CountAsync(cancellationToken);

            var orderedQuery = request.Sort switch
            {
                LibraryManagementMaterialPageSort.Custom => filteredQuery
                    .OrderBy(material => material.DisplayOrder)
                    .ThenBy(material => material.CreatedAt)
                    .ThenBy(material => material.Id),

                LibraryManagementMaterialPageSort.RecentActivity => filteredQuery
                    .OrderByDescending(material => material.UpdatedAt)
                    .ThenBy(material => material.Id),

                LibraryManagementMaterialPageSort.Name => filteredQuery
                    .OrderBy(material => material.Title)
                    .ThenBy(material => material.Id),

                LibraryManagementMaterialPageSort.Newest => filteredQuery
                    .OrderByDescending(material => material.CreatedAt)
                    .ThenBy(material => material.Id),

                _ => filteredQuery
                    .OrderBy(material => material.DisplayOrder)
                    .ThenBy(material => material.CreatedAt)
                    .ThenBy(material => material.Id),
            };

            List<Material> loaded = await orderedQuery
                .Skip(offset)
                .Take(pageSize + 1)
                .ToListAsync(cancellationToken);

            bool hasMore = loaded.Count > pageSize;
            Material[] pageMaterials = loaded.Take(pageSize).ToArray();

            MaterialId[] articleIds = pageMaterials
                .OfType<Article>()
                .Select(article => article.Id)
                .ToArray();

            Dictionary<MaterialId, int> questionCounts = articleIds.Length == 0
                ? []
                : await readDbContext.MaterialsRead
                    .OfType<Question>()
                    .Where(question =>
                        question.ArticleId != null &&
                        articleIds.Contains(question.ArticleId))
                    .GroupBy(question => question.ArticleId!)
                    .Select(group => new
                    {
                        ArticleId = group.Key,
                        Count = group.Count(),
                    })
                    .ToDictionaryAsync(
                        row => row.ArticleId,
                        row => row.Count,
                        cancellationToken);

            var items = pageMaterials
                .Select(material =>
                    new LibraryManagementMaterialOverviewDto(
                        material.Id.Value,
                        material.TopicId.Value,
                        material.Title.Value,
                        material.Type.ToString(),
                        material.Difficulty.ToString(),
                        material.Icon.Key,
                        material.CreatedAt,
                        material.UpdatedAt,
                        material.DisplayOrder,
                        material is Article
                            ? questionCounts.GetValueOrDefault(material.Id)
                            : 0)
                    {
                        ContainerId = material.ContainerId.Value,
                    })
                .ToArray();

            return Result.Success<LibraryManagementMaterialsPageDto, Errors>(
                new LibraryManagementMaterialsPageDto(
                    items,
                    offset + items.Length,
                    hasMore,
                    totalCount,
                    sourceTotalCount));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Получение страницы материалов контейнера {ContainerId} было отменено",
                request.ContainerId);

            return CommonErrors.OperationCancelled(
                "library.management.materials.page.cancelled").ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить страницу материалов контейнера {ContainerId}",
                request.ContainerId);

            return CommonErrors.Db(
                "library.management.materials.page.failed",
                "Не удалось загрузить материалы").ToErrors();
        }
    }
}
