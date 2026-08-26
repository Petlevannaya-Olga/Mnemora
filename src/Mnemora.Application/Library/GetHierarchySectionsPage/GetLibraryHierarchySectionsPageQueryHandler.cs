using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Sections;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.GetHierarchySectionsPage;

public sealed class GetLibraryHierarchySectionsPageQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetLibraryHierarchySectionsPageQueryHandler> logger)
    : IQueryHandler<
        LibraryHierarchySectionsPageDto,
        GetLibraryHierarchySectionsPageQuery>
{
    public async Task<Result<LibraryHierarchySectionsPageDto, Errors>> Handle(
        GetLibraryHierarchySectionsPageQuery request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            int offset = Math.Max(0, request.Offset);
            int pageSize = Math.Clamp(
                request.PageSize,
                1,
                LibraryPagingDefaults.MaxQueryPageSize);

            List<Section> loadedSections =
                await readDbContext.SectionsRead
                    .OrderBy(section => section.Name)
                    .ThenBy(section => section.Id)
                    .Skip(offset)
                    .Take(pageSize + 1)
                    .ToListAsync(cancellationToken);

            bool hasMore = loadedSections.Count > pageSize;
            Section[] pageSections =
                loadedSections.Take(pageSize).ToArray();

            SectionId[] sectionIds =
                pageSections
                    .Select(section => section.Id)
                    .ToArray();

            Dictionary<SectionId, LibraryContainerId> rootIdsBySection =
                sectionIds.Length == 0
                    ? []
                    : await readDbContext.LibraryContainersRead
                        .Where(container =>
                            container.ParentId == null &&
                            sectionIds.Contains(container.SectionId))
                        .ToDictionaryAsync(
                            container => container.SectionId,
                            container => container.Id,
                            cancellationToken);

            LibraryContainerId[] rootIds =
                rootIdsBySection.Values.ToArray();

            Dictionary<LibraryContainerId, int> childFoldersCountByRoot =
                rootIds.Length == 0
                    ? []
                    : await readDbContext.LibraryContainersRead
                        .Where(container =>
                            container.ParentId != null &&
                            rootIds.Contains(container.ParentId!))
                        .GroupBy(container => container.ParentId!)
                        .Select(group => new
                        {
                            RootId = group.Key,
                            Count = group.Count(),
                        })
                        .ToDictionaryAsync(
                            row => row.RootId,
                            row => row.Count,
                            cancellationToken);

            LibraryHierarchySectionDto[] items =
                pageSections
                    .Select(section =>
                    {
                        if (!rootIdsBySection.TryGetValue(
                                section.Id,
                                out LibraryContainerId? rootId))
                        {
                            throw new InvalidOperationException(
                                $"Для раздела '{section.Id.Value}' не найден root-контейнер библиотеки.");
                        }

                        return new LibraryHierarchySectionDto(
                            section.Id.Value,
                            rootId!.Value,
                            section.Name.Value,
                            section.Color.ToString(),
                            section.Icon.ToString(),
                            childFoldersCountByRoot.GetValueOrDefault(rootId!));
                    })
                    .ToArray();

            return Result.Success<LibraryHierarchySectionsPageDto, Errors>(
                new LibraryHierarchySectionsPageDto(
                    items,
                    offset + items.Length,
                    hasMore));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Получение страницы разделов дерева было отменено");

            return CommonErrors.OperationCancelled(
                    "library.hierarchy.sections.page.cancelled")
                .ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить страницу разделов дерева");

            return CommonErrors.Db(
                    "library.hierarchy.sections.page.failed",
                    "Не удалось загрузить структуру разделов")
                .ToErrors();
        }
    }
}
