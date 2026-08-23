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

namespace Mnemora.Application.Library.GetFoldersPage;

public sealed class GetLibraryFoldersPageQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetLibraryFoldersPageQueryHandler> logger)
    : IQueryHandler<LibraryFoldersPageDto, GetLibraryFoldersPageQuery>
{
    public async Task<Result<LibraryFoldersPageDto, Errors>> Handle(
        GetLibraryFoldersPageQuery request,
        CancellationToken cancellationToken = default)
    {
        var containerIdResult =
            LibraryContainerId.Create(request.ContainerId);

        if (containerIdResult.IsFailure)
        {
            return containerIdResult.Error.ToErrors();
        }

        LibraryContainerId containerId =
            containerIdResult.Value;

        try
        {
            bool containerExists =
                await readDbContext.LibraryContainersRead
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

            int offset = Math.Max(0, request.Offset);
            int pageSize = Math.Clamp(
                request.PageSize,
                1,
                LibraryPagingDefaults.MaxQueryPageSize);

            IQueryable<LibraryContainer> foldersQuery =
                readDbContext.LibraryContainersRead
                    .Where(folder =>
                        folder.ParentId == containerId);

            string? search = request.Search?.Trim();

            if (!string.IsNullOrEmpty(search))
            {
                foldersQuery = foldersQuery.Where(folder =>
                    MnemoraDbFunctions.UnicodeContains(
                        EF.Property<string>(
                            folder,
                            nameof(LibraryContainer.Name)),
                        search));
            }

            int totalCount =
                await foldersQuery.CountAsync(cancellationToken);

            IOrderedQueryable<LibraryContainer> orderedQuery =
                request.Sort switch
                {
                    LibraryFolderSort.Name => foldersQuery
                        .OrderBy(folder => folder.Name)
                        .ThenBy(folder => folder.Id),

                    _ => foldersQuery
                        .OrderBy(folder => folder.DisplayOrder)
                        .ThenBy(folder => folder.CreatedAt)
                        .ThenBy(folder => folder.Id),
                };

            List<LibraryContainer> loaded =
                await orderedQuery
                    .Skip(offset)
                    .Take(pageSize + 1)
                    .ToListAsync(cancellationToken);

            bool hasMore = loaded.Count > pageSize;
            LibraryContainer[] pageFolders =
                loaded.Take(pageSize).ToArray();

            LibraryContainerId[] pageFolderIds =
                pageFolders
                    .Select(folder => folder.Id)
                    .ToArray();

            Dictionary<LibraryContainerId, int>
                childFoldersCountByParent =
                    pageFolderIds.Length == 0
                        ? []
                        : await readDbContext.LibraryContainersRead
                            .Where(child =>
                                child.ParentId != null &&
                                pageFolderIds.Contains(child.ParentId!))
                            .GroupBy(child => child.ParentId!)
                            .Select(group => new
                            {
                                ParentId = group.Key,
                                Count = group.Count(),
                            })
                            .ToDictionaryAsync(
                                row => row.ParentId,
                                row => row.Count,
                                cancellationToken);

            Dictionary<LibraryContainerId, int>
                materialsCountByContainer =
                    pageFolderIds.Length == 0
                        ? []
                        : await GetTopLevelMaterials(
                                readDbContext.MaterialsRead)
                            .Where(material =>
                                pageFolderIds.Contains(
                                    material.ContainerId))
                            .GroupBy(material => material.ContainerId)
                            .Select(group => new
                            {
                                ContainerId = group.Key,
                                Count = group.Count(),
                            })
                            .ToDictionaryAsync(
                                row => row.ContainerId,
                                row => row.Count,
                                cancellationToken);

            LibraryFolderDto[] items =
                pageFolders
                    .Select(folder =>
                        new LibraryFolderDto(
                            folder.Id.Value,
                            folder.SectionId.Value,
                            folder.ParentId!.Value,
                            folder.Depth,
                            folder.Name!.Value,
                            folder.Color!.Value.ToString(),
                            folder.Icon!.Value.ToString(),
                            folder.DisplayOrder,
                            childFoldersCountByParent
                                .GetValueOrDefault(folder.Id),
                            materialsCountByContainer
                                .GetValueOrDefault(folder.Id),
                            CanCreateChildFolder(folder),
                            folder.CreatedAt,
                            folder.UpdatedAt))
                    .ToArray();

            return Result.Success<LibraryFoldersPageDto, Errors>(
                new LibraryFoldersPageDto(
                    items,
                    offset + items.Length,
                    hasMore,
                    totalCount));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Получение страницы папок контейнера {ContainerId} было отменено",
                request.ContainerId);

            return CommonErrors.OperationCancelled(
                    "library.folders.page.cancelled")
                .ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить страницу папок контейнера {ContainerId}",
                request.ContainerId);

            return CommonErrors.Db(
                    "library.folders.page.failed",
                    "Не удалось загрузить папки")
                .ToErrors();
        }
    }

    private static IQueryable<Material> GetTopLevelMaterials(
        IQueryable<Material> materials) =>
        materials.Where(material =>
            material is Article ||
            (material is Question &&
             ((Question)material).ArticleId == null));

    private static bool CanCreateChildFolder(
        LibraryContainer container) =>
        container.Depth <
        LibraryContainer.MaxFolderDepth;
}
