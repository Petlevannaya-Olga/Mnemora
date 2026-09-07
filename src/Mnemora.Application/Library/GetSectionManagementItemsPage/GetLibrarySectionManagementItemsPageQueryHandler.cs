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

namespace Mnemora.Application.Library.GetSectionManagementItemsPage;

public sealed class GetLibrarySectionManagementItemsPageQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetLibrarySectionManagementItemsPageQueryHandler> logger)
    : IQueryHandler<LibrarySectionManagementItemsPageDto, GetLibrarySectionManagementItemsPageQuery>
{
    public async Task<Result<LibrarySectionManagementItemsPageDto, Errors>> Handle(
        GetLibrarySectionManagementItemsPageQuery request,
        CancellationToken cancellationToken = default)
    {
        var sectionIdResult = SectionId.Create(request.SectionId);

        if (sectionIdResult.IsFailure)
        {
            return sectionIdResult.Error.ToErrors();
        }

        SectionId sectionId = sectionIdResult.Value;

        try
        {
            bool sectionExists = await readDbContext.SectionsRead
                .AnyAsync(section => section.Id == sectionId, cancellationToken);

            if (!sectionExists)
            {
                return CommonErrors.NotFound(
                        "library.section.not.found",
                        $"Раздел с идентификатором '{request.SectionId}' не найден")
                    .ToErrors();
            }

            int offset = Math.Max(0, request.Offset);
            int pageSize = Math.Clamp(request.PageSize, 1, LibraryPagingDefaults.MaxQueryPageSize);
            string? search = request.Search?.Trim();

            bool includeFolders = request.Filter is
                LibrarySectionManagementItemFilter.All or
                LibrarySectionManagementItemFilter.Folders;
            bool includeMaterials = request.Filter is not LibrarySectionManagementItemFilter.Folders;

            IQueryable<LibraryContainer> sourceFolders = readDbContext.LibraryContainersRead
                .Where(container => container.SectionId == sectionId && container.ParentId != null);

            IQueryable<Material> sourceMaterials = GetTopLevelMaterials(readDbContext.MaterialsRead)
                .Where(material => readDbContext.LibraryContainersRead.Any(container =>
                    container.Id == material.ContainerId &&
                    container.SectionId == sectionId));

            sourceMaterials = request.Filter switch
            {
                LibrarySectionManagementItemFilter.Articles => sourceMaterials.OfType<Article>(),
                LibrarySectionManagementItemFilter.Questions => sourceMaterials.OfType<Question>(),
                _ => sourceMaterials,
            };

            int sourceFoldersCount = includeFolders
                ? await sourceFolders.CountAsync(cancellationToken)
                : 0;
            int sourceMaterialsCount = includeMaterials
                ? await sourceMaterials.CountAsync(cancellationToken)
                : 0;
            int sourceTotalCount = sourceFoldersCount + sourceMaterialsCount;

            IQueryable<LibraryContainer> folders = sourceFolders;
            IQueryable<Material> materials = sourceMaterials;

            if (!string.IsNullOrWhiteSpace(search))
            {
                if (includeFolders)
                {
                    folders = folders.Where(folder =>
                        MnemoraDbFunctions.UnicodeContains(
                            EF.Property<string>(folder, nameof(LibraryContainer.Name)),
                            search));
                }

                if (includeMaterials)
                {
                    materials = materials.Where(material =>
                        MnemoraDbFunctions.UnicodeContains(
                            EF.Property<string>(material, nameof(Material.Title)),
                            search));
                }
            }

            int foldersCount = !includeFolders
                ? 0
                : string.IsNullOrWhiteSpace(search)
                    ? sourceFoldersCount
                    : await folders.CountAsync(cancellationToken);

            int materialsCount = !includeMaterials
                ? 0
                : string.IsNullOrWhiteSpace(search)
                    ? sourceMaterialsCount
                    : await materials.CountAsync(cancellationToken);

            int totalCount = foldersCount + materialsCount;
            int remaining = pageSize;
            var folderPage = new List<LibraryContainer>(pageSize);
            var materialPage = new List<Material>(pageSize);

            if (offset < foldersCount && remaining > 0)
            {
                int folderTake = Math.Min(remaining, foldersCount - offset);
                folderPage.AddRange(await OrderFolders(folders, request.Sort)
                    .Skip(offset)
                    .Take(folderTake)
                    .ToListAsync(cancellationToken));
                remaining -= folderPage.Count;
            }

            if (remaining > 0)
            {
                int materialOffset = Math.Max(0, offset - foldersCount);
                materialPage.AddRange(await OrderMaterials(materials, request.Sort)
                    .Skip(materialOffset)
                    .Take(remaining)
                    .ToListAsync(cancellationToken));
            }

            MaterialId[] articleIds = materialPage
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
                    .Select(group => new { ArticleId = group.Key, Count = group.Count() })
                    .ToDictionaryAsync(row => row.ArticleId, row => row.Count, cancellationToken);

            var relevantContainerIds = new HashSet<LibraryContainerId>();

            foreach (LibraryContainer folder in folderPage)
            {
                if (folder.ParentId is not null)
                {
                    relevantContainerIds.Add(folder.ParentId);
                }
            }

            foreach (Material material in materialPage)
            {
                relevantContainerIds.Add(material.ContainerId);
            }

            Dictionary<LibraryContainerId, LibraryContainer> containers = await LoadContainerPathsAsync(
                sectionId,
                relevantContainerIds,
                cancellationToken);

            var items = new List<LibrarySectionManagementItemDto>(folderPage.Count + materialPage.Count);

            foreach (LibraryContainer folder in folderPage)
            {
                items.Add(new LibrarySectionManagementItemDto(
                    folder.Id.Value,
                    LibrarySectionManagementItemKind.Folder,
                    folder.Name!.Value,
                    BuildLocation(folder.ParentId, containers, rootText: "Раздел"),
                    folder.Icon!.Value.ToString(),
                    null,
                    null,
                    0,
                    folder.CreatedAt,
                    folder.UpdatedAt,
                    folder.DisplayOrder,
                    folder.Id.Value,
                    folder.ParentId?.Value));
            }

            foreach (Material material in materialPage)
            {
                items.Add(new LibrarySectionManagementItemDto(
                    material.Id.Value,
                    LibrarySectionManagementItemKind.Material,
                    material.Title.Value,
                    BuildLocation(material.ContainerId, containers, rootText: "Без папки"),
                    material.Icon.Key,
                    material.Type.ToString(),
                    material.Difficulty.ToString(),
                    material is Article ? questionCounts.GetValueOrDefault(material.Id) : 0,
                    material.CreatedAt,
                    material.UpdatedAt,
                    material.DisplayOrder,
                    material.ContainerId.Value,
                    null));
            }

            int nextOffset = offset + items.Count;

            return Result.Success<LibrarySectionManagementItemsPageDto, Errors>(
                new LibrarySectionManagementItemsPageDto(
                    items,
                    nextOffset,
                    nextOffset < totalCount,
                    totalCount,
                    sourceTotalCount));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled(
                    "library.section.management.page.cancelled")
                .ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить плоский список управления разделом {SectionId}",
                request.SectionId);

            return CommonErrors.Db(
                    "library.section.management.page.failed",
                    "Не удалось загрузить содержимое раздела")
                .ToErrors();
        }
    }

    private static IOrderedQueryable<LibraryContainer> OrderFolders(
        IQueryable<LibraryContainer> query,
        LibrarySectionManagementItemSort sort) => sort switch
        {
            LibrarySectionManagementItemSort.RecentActivity => query
                .OrderByDescending(folder => folder.UpdatedAt)
                .ThenBy(folder => folder.Id),
            LibrarySectionManagementItemSort.Name => query
                .OrderBy(folder => folder.Name)
                .ThenBy(folder => folder.Id),
            LibrarySectionManagementItemSort.Newest => query
                .OrderByDescending(folder => folder.CreatedAt)
                .ThenBy(folder => folder.Id),
            _ => query
                .OrderBy(folder => folder.DisplayOrder)
                .ThenBy(folder => folder.CreatedAt)
                .ThenBy(folder => folder.Id),
        };

    private static IOrderedQueryable<Material> OrderMaterials(
        IQueryable<Material> query,
        LibrarySectionManagementItemSort sort) => sort switch
        {
            LibrarySectionManagementItemSort.RecentActivity => query
                .OrderByDescending(material => material.UpdatedAt)
                .ThenBy(material => material.Id),
            LibrarySectionManagementItemSort.Name => query
                .OrderBy(material => material.Title)
                .ThenBy(material => material.Id),
            LibrarySectionManagementItemSort.Newest => query
                .OrderByDescending(material => material.CreatedAt)
                .ThenBy(material => material.Id),
            _ => query
                .OrderBy(material => material.DisplayOrder)
                .ThenBy(material => material.CreatedAt)
                .ThenBy(material => material.Id),
        };

    private async Task<Dictionary<LibraryContainerId, LibraryContainer>> LoadContainerPathsAsync(
        SectionId sectionId,
        IEnumerable<LibraryContainerId> startIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<LibraryContainerId, LibraryContainer>();
        var pending = new HashSet<LibraryContainerId>(startIds);

        for (int level = 0; level <= LibraryContainer.MaxFolderDepth && pending.Count > 0; level++)
        {
            LibraryContainerId[] ids = pending
                .Where(id => !result.ContainsKey(id))
                .ToArray();

            pending.Clear();

            if (ids.Length == 0)
            {
                break;
            }

            List<LibraryContainer> loaded = await readDbContext.LibraryContainersRead
                .Where(container =>
                    container.SectionId == sectionId &&
                    ids.Contains(container.Id))
                .ToListAsync(cancellationToken);

            foreach (LibraryContainer container in loaded)
            {
                result[container.Id] = container;

                if (container.ParentId is not null && !result.ContainsKey(container.ParentId))
                {
                    pending.Add(container.ParentId);
                }
            }
        }

        return result;
    }

    private static string BuildLocation(
        LibraryContainerId? containerId,
        IReadOnlyDictionary<LibraryContainerId, LibraryContainer> containers,
        string rootText)
    {
        if (containerId is null || !containers.TryGetValue(containerId, out LibraryContainer? container))
        {
            return rootText;
        }

        if (container.IsRoot)
        {
            return rootText;
        }

        var names = new Stack<string>();
        LibraryContainer? current = container;

        while (current is not null && !current.IsRoot)
        {
            if (current.Name is not null)
            {
                names.Push(current.Name.Value);
            }

            if (current.ParentId is null)
            {
                break;
            }

            LibraryContainerId parentId = current.ParentId!;

            if (!containers.TryGetValue(parentId, out LibraryContainer? parent))
            {
                break;
            }

            current = parent;
        }

        return names.Count == 0 ? rootText : string.Join(" / ", names);
    }

    private static IQueryable<Material> GetTopLevelMaterials(IQueryable<Material> materials) =>
        materials.Where(material =>
            material is Article ||
            (material is Question && ((Question)material).ArticleId == null));
}
