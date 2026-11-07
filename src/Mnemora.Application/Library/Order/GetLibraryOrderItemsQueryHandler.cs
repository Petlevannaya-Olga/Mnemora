using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Shared;
using Mnemora.Shared.Abstractions;

namespace Mnemora.Application.Library.Order;

public sealed class GetLibraryOrderItemsQueryHandler(
    IReadDbContext readDbContext,
    ILogger<GetLibraryOrderItemsQueryHandler> logger)
    : IQueryHandler<IReadOnlyList<LibraryOrderItemDto>, GetLibraryOrderItemsQuery>
{
    public async Task<Result<IReadOnlyList<LibraryOrderItemDto>, Errors>> Handle(
        GetLibraryOrderItemsQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return query.Target switch
            {
                LibraryOrderTarget.Sections => await GetSectionsAsync(cancellationToken),
                LibraryOrderTarget.Folders => await GetFoldersAsync(query.ParentId, cancellationToken),
                LibraryOrderTarget.Materials => await GetMaterialsAsync(query.ParentId, cancellationToken),
                _ => CommonErrors.Validation(
                        "library.order.target.invalid",
                        "Выбран неизвестный уровень библиотеки",
                        nameof(query.Target))
                    .ToErrors()
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled("library.order.items.cancelled").ToErrors();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось получить элементы настройки порядка {Target}", query.Target);
            return CommonErrors.Db(
                    "library.order.items.failed",
                    "Не удалось загрузить элементы для настройки порядка")
                .ToErrors();
        }
    }

    private async Task<Result<IReadOnlyList<LibraryOrderItemDto>, Errors>> GetSectionsAsync(
        CancellationToken cancellationToken)
    {
        var sections = await readDbContext.SectionsRead
            .OrderBy(section => section.DisplayOrder)
            .ThenBy(section => section.CreatedAt)
            .ThenBy(section => section.Id)
            .ToListAsync(cancellationToken);
        var folders = await readDbContext.LibraryContainersRead
            .Where(container => container.Depth == 1)
            .ToListAsync(cancellationToken);
        var counts = folders.GroupBy(folder => folder.SectionId.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        return Result.Success<IReadOnlyList<LibraryOrderItemDto>, Errors>(
            sections.Select(section => new LibraryOrderItemDto(
                    section.Id.Value,
                    section.Name.Value,
                    section.Icon.ToString(),
                    section.Color.ToString(),
                    FormatCount(counts.GetValueOrDefault(section.Id.Value), "папка", "папки", "папок"),
                    section.DisplayOrder))
                .ToArray());
    }

    private async Task<Result<IReadOnlyList<LibraryOrderItemDto>, Errors>> GetFoldersAsync(
        Guid? parentId,
        CancellationToken cancellationToken)
    {
        var sectionIdResult = CreateSectionId(parentId);

        if (sectionIdResult.IsFailure)
        {
            return sectionIdResult.Error.ToErrors();
        }

        var folders = await readDbContext.LibraryContainersRead
            .Where(folder => folder.SectionId == sectionIdResult.Value && folder.Depth == 1)
            .OrderBy(folder => folder.DisplayOrder)
            .ThenBy(folder => folder.CreatedAt)
            .ThenBy(folder => folder.Id)
            .ToListAsync(cancellationToken);
        var folderIds = folders.Select(folder => folder.Id).ToArray();
        List<Material> materials = folderIds.Length == 0
            ? []
            : await readDbContext.MaterialsRead
                .Where(material => folderIds.Contains(material.ContainerId))
                .ToListAsync(cancellationToken);
        var counts = materials.GroupBy(material => material.ContainerId.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        return Result.Success<IReadOnlyList<LibraryOrderItemDto>, Errors>(
            folders.Select(folder => new LibraryOrderItemDto(
                    folder.Id.Value,
                    folder.Name!.Value,
                    folder.Icon!.Value.ToString(),
                    folder.Color!.Value.ToString(),
                    FormatCount(counts.GetValueOrDefault(folder.Id.Value), "материал", "материала", "материалов"),
                    folder.DisplayOrder))
                .ToArray());
    }

    private async Task<Result<IReadOnlyList<LibraryOrderItemDto>, Errors>> GetMaterialsAsync(
        Guid? parentId,
        CancellationToken cancellationToken)
    {
        var containerIdResult = CreateContainerId(parentId);

        if (containerIdResult.IsFailure)
        {
            return containerIdResult.Error.ToErrors();
        }

        var materials = await readDbContext.MaterialsRead
            .Where(material => material.ContainerId == containerIdResult.Value)
            .OrderBy(material => material.DisplayOrder)
            .ThenBy(material => material.CreatedAt)
            .ThenBy(material => material.Id)
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<LibraryOrderItemDto>, Errors>(
            materials.Select(material => new LibraryOrderItemDto(
                    material.Id.Value,
                    material.Title.Value,
                    material.Icon.Key,
                    null,
                    material.Type == MaterialType.Article ? "Статья" : "Вопрос",
                    material.DisplayOrder))
                .ToArray());
    }

    private static Result<SectionId, Error> CreateSectionId(Guid? value)
    {
        return value.HasValue
            ? SectionId.Create(value.Value)
            : CommonErrors.IsRequired("sectionId");
    }

    private static Result<LibraryContainerId, Error> CreateContainerId(Guid? value)
    {
        return value.HasValue
            ? LibraryContainerId.Create(value.Value)
            : CommonErrors.IsRequired("containerId");
    }

    private static string FormatCount(int count, string one, string few, string many)
    {
        int lastTwoDigits = count % 100;

        if (lastTwoDigits is >= 11 and <= 14)
        {
            return $"{count} {many}";
        }

        return (count % 10) switch
        {
            1 => $"{count} {one}",
            2 or 3 or 4 => $"{count} {few}",
            _ => $"{count} {many}"
        };
    }
}
