using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Database;

internal sealed class TransactionScope(
    IDbContextTransaction transaction,
    ILogger<TransactionScope> logger)
    : ITransactionScope
{
    private bool _isCompleted;
    private bool _isDisposed;

    public async Task<UnitResult<Error>> CommitAsync(
        CancellationToken cancellationToken)
    {
        if (_isDisposed)
        {
            return TransactionIsDisposed();
        }

        if (_isCompleted)
        {
            return TransactionIsCompleted();
        }

        try
        {
            await transaction.CommitAsync(
                cancellationToken);

            _isCompleted = true;

            return UnitResult.Success<Error>();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Фиксация транзакции была отменена");

            return CommonErrors.OperationCancelled(
                "transaction.commit.cancelled");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось зафиксировать транзакцию");

            return CommonErrors.Db(
                "transaction.commit.failed",
                "Не удалось зафиксировать транзакцию");
        }
    }

    public async Task<UnitResult<Error>> RollbackAsync(
        CancellationToken cancellationToken)
    {
        if (_isDisposed)
        {
            return TransactionIsDisposed();
        }

        if (_isCompleted)
        {
            return TransactionIsCompleted();
        }

        try
        {
            await transaction.RollbackAsync(
                cancellationToken);

            _isCompleted = true;

            return UnitResult.Success<Error>();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Откат транзакции был отменён");

            return CommonErrors.OperationCancelled(
                "transaction.rollback.cancelled");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось откатить транзакцию");

            return CommonErrors.Db(
                "transaction.rollback.failed",
                "Не удалось откатить транзакцию");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (!_isCompleted)
        {
            try
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);

                _isCompleted = true;

                logger.LogDebug(
                    "Незавершённая транзакция автоматически отменена");
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Не удалось автоматически откатить транзакцию");
            }
        }

        try
        {
            await transaction.DisposeAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось освободить транзакцию");
        }
    }

    private static UnitResult<Error>
        TransactionIsCompleted()
    {
        return CommonErrors.Failure(
            "transaction.already.completed",
            "Транзакция уже завершена");
    }

    private static UnitResult<Error>
        TransactionIsDisposed()
    {
        return CommonErrors.Failure(
            "transaction.already.disposed",
            "Транзакция уже освобождена");
    }
}