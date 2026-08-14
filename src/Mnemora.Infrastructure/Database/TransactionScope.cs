using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Database;

public sealed class TransactionScope(
    IDbContextTransaction transaction,
    ILogger<TransactionScope> logger)
    : ITransactionScope
{
    private bool _isCompleted;

    public async Task<UnitResult<Error>> CommitAsync(CancellationToken cancellationToken)
    {
        if (_isCompleted)
        {
            return UnitResult.Failure(
                CommonErrors.Failure(
                    "transaction.already.completed",
                    "Транзакция уже завершена"));
        }

        try
        {
            await transaction.CommitAsync(cancellationToken);
            _isCompleted = true;

            return UnitResult.Success<Error>();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Фиксация транзакции была отменена");

            return UnitResult.Failure(
                CommonErrors.OperationCancelled(
                    "transaction.commit.cancelled"));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось зафиксировать транзакцию");

            return UnitResult.Failure(
                CommonErrors.Db(
                    "transaction.commit.failed",
                    "Не удалось зафиксировать транзакцию"));
        }
    }

    public async Task<UnitResult<Error>> RollbackAsync(CancellationToken cancellationToken)
    {
        if (_isCompleted)
        {
            return UnitResult.Failure(
                CommonErrors.Failure(
                    "transaction.already.completed",
                    "Транзакция уже завершена"));
        }

        try
        {
            await transaction.RollbackAsync(cancellationToken);
            _isCompleted = true;

            return UnitResult.Success<Error>();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Откат транзакции был отменён");

            return UnitResult.Failure(
                CommonErrors.OperationCancelled(
                    "transaction.rollback.cancelled"));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось откатить транзакцию");

            return UnitResult.Failure(
                CommonErrors.Db(
                    "transaction.rollback.failed",
                    "Не удалось откатить транзакцию"));
        }
    }

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
    }
}