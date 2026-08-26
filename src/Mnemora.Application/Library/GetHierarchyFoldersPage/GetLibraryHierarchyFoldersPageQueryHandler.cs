using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetHierarchyFoldersPage;

public sealed class GetLibraryHierarchyFoldersPageQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetLibraryHierarchyFoldersPageQueryHandler> logger)
    : IQueryHandler<
        LibraryHierarchyFoldersPageDto,
        GetLibraryHierarchyFoldersPageQuery>
{
    public async Task<Result<LibraryHierarchyFoldersPageDto, Errors>> Handle(
        GetLibraryHierarchyFoldersPageQuery request,
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
            int offset = Math.Max(0, request.Offset);
            int pageSize = Math.Clamp(
                request.PageSize,
                1,
                LibraryPagingDefaults.MaxQueryPageSize);

            List<LibraryContainer> loadedFolders =
                await readDbContext.LibraryContainersRead
                    .Where(folder => folder.ParentId == containerId)
                    .OrderBy(folder => folder.DisplayOrder)
                    .ThenBy(folder => folder.CreatedAt)
                    .ThenBy(folder => folder.Id)
                    .Skip(offset)
                    .Take(pageSize + 1)
                    .ToListAsync(cancellationToken);

            bool hasMore = loadedFolders.Count > pageSize;
            LibraryContainer[] pageFolders =
                loadedFolders.Take(pageSize).ToArray();

            LibraryContainerId[] pageFolderIds =
                pageFolders
                    .Select(folder => folder.Id)
                    .ToArray();

            Dictionary<LibraryContainerId, int> childFoldersCountByParent =
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

            LibraryHierarchyFolderDto[] items =
                pageFolders
                    .Select(folder =>
                        new LibraryHierarchyFolderDto(
                            folder.Id.Value,
                            folder.SectionId.Value,
                            folder.ParentId!.Value,
                            folder.Name!.Value,
                            folder.Color!.Value.ToString(),
                            folder.Icon!.Value.ToString(),
                            childFoldersCountByParent
                                .GetValueOrDefault(folder.Id)))
                    .ToArray();

            return Result.Success<LibraryHierarchyFoldersPageDto, Errors>(
                new LibraryHierarchyFoldersPageDto(
                    items,
                    offset + items.Length,
                    hasMore));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Получение страницы папок дерева для контейнера {ContainerId} было отменено",
                request.ContainerId);

            return CommonErrors.OperationCancelled(
                    "library.hierarchy.folders.page.cancelled")
                .ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить страницу папок дерева для контейнера {ContainerId}",
                request.ContainerId);

            return CommonErrors.Db(
                    "library.hierarchy.folders.page.failed",
                    "Не удалось загрузить структуру папок")
                .ToErrors();
        }
    }
}
