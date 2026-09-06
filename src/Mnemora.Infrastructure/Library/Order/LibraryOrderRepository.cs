using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Library.Order;
using Mnemora.Domain.LibraryContainers;
using Mnemora.Domain.Materials;
using Mnemora.Domain.Sections;
using Mnemora.Domain.Topics;
using Mnemora.Infrastructure.Persistence;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Library.Order;

internal sealed class LibraryOrderRepository(
    MnemoraDbContext dbContext,
    ILogger<LibraryOrderRepository> logger)
    : ILibraryOrderRepository
{
    public Task<Result<IReadOnlyList<Section>, Error>> GetSectionsAsync(
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            async () => await dbContext.Sections
                .OrderBy(section => section.DisplayOrder)
                .ThenBy(section => section.CreatedAt)
                .ThenBy(section => section.Id)
                .ToListAsync(cancellationToken),
            "разделы",
            cancellationToken);
    }

    public Task<Result<IReadOnlyList<Topic>, Error>> GetTopicsAsync(
        SectionId sectionId,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            async () => await dbContext.Topics
                .Where(topic => topic.SectionId == sectionId)
                .OrderBy(topic => topic.DisplayOrder)
                .ThenBy(topic => topic.CreatedAt)
                .ThenBy(topic => topic.Id)
                .ToListAsync(cancellationToken),
            "темы",
            cancellationToken);
    }

    public Task<Result<IReadOnlyList<LibraryContainer>, Error>> GetFirstLevelFoldersAsync(
        SectionId sectionId,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            async () => await dbContext.LibraryContainers
                .Where(container =>
                    container.SectionId == sectionId &&
                    container.Depth == 1)
                .OrderBy(container => container.DisplayOrder)
                .ThenBy(container => container.CreatedAt)
                .ThenBy(container => container.Id)
                .ToListAsync(cancellationToken),
            "папки первого уровня",
            cancellationToken);
    }

    public Task<Result<IReadOnlyList<Material>, Error>> GetMaterialsAsync(
        TopicId topicId,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            async () => await dbContext.Materials
                .Where(material => material.TopicId == topicId)
                .OrderBy(material => material.DisplayOrder)
                .ThenBy(material => material.CreatedAt)
                .ThenBy(material => material.Id)
                .ToListAsync(cancellationToken),
            "материалы",
            cancellationToken);
    }

    private async Task<Result<IReadOnlyList<TEntity>, Error>> ExecuteAsync<TEntity>(
        Func<Task<List<TEntity>>> query,
        string entityName,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<TEntity> entities = await query();
            return Result.Success<IReadOnlyList<TEntity>, Error>(entities);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled(
                "library.order.get.cancelled");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось получить {EntityName} для изменения порядка",
                entityName);

            return CommonErrors.Db(
                "library.order.get.failed",
                "Не удалось получить элементы для изменения порядка");
        }
    }
}
