using System.Linq.Expressions;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Topics;
using Mnemora.Domain.Topics;
using Mnemora.Infrastructure.Persistence;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Topics;

internal sealed class TopicsRepository(
    MnemoraDbContext dbContext,
    ILogger<TopicsRepository> logger)
    : ITopicsRepository
{
    public void Add(Topic topic)
    {
        dbContext.Topics.Add(topic);
    }

    public async Task<Result<bool, Error>> ExistsAsync(
        Expression<Func<Topic, bool>> predicate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        try
        {
            var exists = await dbContext.Topics.AnyAsync(
                predicate,
                cancellationToken);

            return exists;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled(
                "topic.exists.cancelled");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось проверить существование темы");

            return CommonErrors.Db(
                "topic.exists.failed",
                "Не удалось проверить существование темы");
        }
    }
}