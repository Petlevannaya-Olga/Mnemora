using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetManagementFoldersPage;

public sealed class GetLibraryManagementFoldersPageQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetLibraryManagementFoldersPageQueryHandler> logger)
    : IQueryHandler<LibraryManagementFoldersPageDto, GetLibraryManagementFoldersPageQuery>
{
    public async Task<Result<LibraryManagementFoldersPageDto, Errors>> Handle(
        GetLibraryManagementFoldersPageQuery request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            int offset = Math.Max(0, request.Offset);
            int pageSize = Math.Clamp(
                request.PageSize,
                1,
                LibraryPagingDefaults.MAX_QUERY_PAGE_SIZE);

            var sectionIdResult =
                SectionId.Create(request.SectionId);

            if (sectionIdResult.IsFailure)
            {
                return sectionIdResult.Error.ToErrors();
            }

            IQueryable<LibraryContainer> foldersQuery =
                readDbContext.LibraryContainersRead
                    .Where(container =>
                        container.SectionId == sectionIdResult.Value &&
                        container.Depth == 1);

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

            int totalCount = await foldersQuery.CountAsync(
                cancellationToken);

            // Nullable MAX is intentional: a folder may legitimately have no
            // materials. COALESCE keeps LastActivityAt non-null in SQL and
            // avoids materializing NULL into DateTime.
            var rows =
                from folder in foldersQuery
                let lastMaterialActivityAt =
                    readDbContext.MaterialsRead
                        .Where(material =>
                            material.ContainerId == folder.Id)
                        .Max(material =>
                            (DateTime?)material.UpdatedAt)
                let materialOrFolderActivityAt =
                    lastMaterialActivityAt ?? folder.UpdatedAt
                let lastActivityAt =
                    folder.UpdatedAt >= materialOrFolderActivityAt
                        ? folder.UpdatedAt
                        : materialOrFolderActivityAt
                select new
                {
                    Folder = folder,
                    LastActivityAt = lastActivityAt,
                };

            var orderedRows = request.Sort switch
            {
                LibraryManagementFolderPageSort.CUSTOM => rows
                    .OrderBy(row => row.Folder.DisplayOrder)
                    .ThenBy(row => row.Folder.CreatedAt)
                    .ThenBy(row => row.Folder.Id),

                LibraryManagementFolderPageSort.RECENT_ACTIVITY => rows
                    .OrderByDescending(row => row.LastActivityAt)
                    .ThenBy(row => row.Folder.Id),

                LibraryManagementFolderPageSort.NAME => rows
                    .OrderBy(row => row.Folder.Name)
                    .ThenBy(row => row.Folder.Id),

                LibraryManagementFolderPageSort.NEWEST => rows
                    .OrderByDescending(row => row.Folder.CreatedAt)
                    .ThenBy(row => row.Folder.Id),

                _ => rows
                    .OrderBy(row => row.Folder.DisplayOrder)
                    .ThenBy(row => row.Folder.CreatedAt)
                    .ThenBy(row => row.Folder.Id),
            };

            var loadedRows = await orderedRows
                .Skip(offset)
                .Take(pageSize + 1)
                .ToListAsync(cancellationToken);

            bool hasMore = loadedRows.Count > pageSize;
            var pageRows = loadedRows
                .Take(pageSize)
                .ToArray();

            LibraryContainerId[] folderIds = pageRows
                .Select(row => row.Folder.Id)
                .ToArray();

            Dictionary<LibraryContainerId, int> articleCounts =
                folderIds.Length == 0
                    ? []
                    : await readDbContext.MaterialsRead
                        .OfType<Article>()
                        .Where(article =>
                            folderIds.Contains(article.ContainerId))
                        .GroupBy(article => article.ContainerId)
                        .Select(group => new
                        {
                            ContainerId = group.Key,
                            Count = group.Count(),
                        })
                        .ToDictionaryAsync(
                            row => row.ContainerId,
                            row => row.Count,
                            cancellationToken);

            Dictionary<LibraryContainerId, int> questionCounts =
                folderIds.Length == 0
                    ? []
                    : await readDbContext.MaterialsRead
                        .OfType<Question>()
                        .Where(question =>
                            folderIds.Contains(question.ContainerId) &&
                            question.ArticleId == null)
                        .GroupBy(question => question.ContainerId)
                        .Select(group => new
                        {
                            ContainerId = group.Key,
                            Count = group.Count(),
                        })
                        .ToDictionaryAsync(
                            row => row.ContainerId,
                            row => row.Count,
                            cancellationToken);

            var items = pageRows
                .Select(row =>
                {
                    int articlesCount =
                        articleCounts.GetValueOrDefault(
                            row.Folder.Id);

                    int questionsCount =
                        questionCounts.GetValueOrDefault(
                            row.Folder.Id);

                    return new LibraryManagementFolderOverviewDto(
                        row.Folder.Id.Value,
                        row.Folder.SectionId.Value,
                        row.Folder.Name!.Value,
                        row.Folder.Color!.Value.ToStableName(),
                        row.Folder.Icon!.Value.ToStableName(),
                        row.Folder.CreatedAt,
                        row.Folder.UpdatedAt,
                        row.LastActivityAt,
                        row.Folder.DisplayOrder,
                        articlesCount + questionsCount,
                        articlesCount,
                        questionsCount);
                })
                .ToArray();

            return Result.Success<LibraryManagementFoldersPageDto, Errors>(
                new LibraryManagementFoldersPageDto(
                    items,
                    offset + items.Length,
                    hasMore,
                    totalCount));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Получение страницы папок управления было отменено");

            return CommonErrors.OperationCancelled(
                "library.management.folders.page.cancelled")
                .ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить страницу папок управления");

            return CommonErrors.Db(
                "library.management.folders.page.failed",
                "Не удалось загрузить папки")
                .ToErrors();
        }
    }
}
