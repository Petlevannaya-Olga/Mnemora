using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Contracts;
using Mnemora.Contracts.Library;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
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
                LibraryOrderTarget.Topics => await GetTopicsAsync(query.ParentId, cancellationToken),
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
        var topics = await readDbContext.TopicsRead.ToListAsync(cancellationToken);
        var counts = topics.GroupBy(topic => topic.SectionId.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        return Result.Success<IReadOnlyList<LibraryOrderItemDto>, Errors>(
            sections.Select(section => new LibraryOrderItemDto(
                    section.Id.Value,
                    section.Name.Value,
                    section.Icon.ToString(),
                    section.Color.ToString(),
                    FormatCount(counts.GetValueOrDefault(section.Id.Value), "тема", "темы", "тем"),
                    section.DisplayOrder))
                .ToArray());
    }

    private async Task<Result<IReadOnlyList<LibraryOrderItemDto>, Errors>> GetTopicsAsync(
        Guid? parentId,
        CancellationToken cancellationToken)
    {
        var sectionIdResult = CreateSectionId(parentId);

        if (sectionIdResult.IsFailure)
        {
            return sectionIdResult.Error.ToErrors();
        }

        var topics = await readDbContext.TopicsRead
            .Where(topic => topic.SectionId == sectionIdResult.Value)
            .OrderBy(topic => topic.DisplayOrder)
            .ThenBy(topic => topic.CreatedAt)
            .ThenBy(topic => topic.Id)
            .ToListAsync(cancellationToken);
        var topicIds = topics.Select(topic => topic.Id).ToArray();
        List<Material> materials = topicIds.Length == 0
            ? []
            : await readDbContext.MaterialsRead
                .Where(material => topicIds.Contains(material.TopicId))
                .ToListAsync(cancellationToken);
        var counts = materials.GroupBy(material => material.TopicId.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        return Result.Success<IReadOnlyList<LibraryOrderItemDto>, Errors>(
            topics.Select(topic => new LibraryOrderItemDto(
                    topic.Id.Value,
                    topic.Name.Value,
                    topic.Icon.ToString(),
                    topic.Color.ToString(),
                    FormatCount(counts.GetValueOrDefault(topic.Id.Value), "материал", "материала", "материалов"),
                    topic.DisplayOrder))
                .ToArray());
    }

    private async Task<Result<IReadOnlyList<LibraryOrderItemDto>, Errors>> GetMaterialsAsync(
        Guid? parentId,
        CancellationToken cancellationToken)
    {
        var topicIdResult = CreateTopicId(parentId);

        if (topicIdResult.IsFailure)
        {
            return topicIdResult.Error.ToErrors();
        }

        var materials = await readDbContext.MaterialsRead
            .Where(material => material.TopicId == topicIdResult.Value)
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

    private static Result<TopicId, Error> CreateTopicId(Guid? value)
    {
        return value.HasValue
            ? TopicId.Create(value.Value)
            : CommonErrors.IsRequired("topicId");
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
