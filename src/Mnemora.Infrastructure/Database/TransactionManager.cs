using CSharpFunctionalExtensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Infrastructure.Database.Errors;
using Mnemora.Infrastructure.Persistence;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Database;

internal sealed class TransactionManager(
    MnemoraDbContext dbContext,
    SqliteErrorTranslator errorTranslator,
    ILogger<TransactionManager> logger,
    ILogger<TransactionScope> transactionScopeLogger)
    : ITransactionManager
{
    public async Task<Result<ITransactionScope, Error>> BeginTransactionAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var transactionScope = new TransactionScope(transaction, transactionScopeLogger);

            return Result.Success<ITransactionScope, Error>(transactionScope);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Создание транзакции было отменено");

            return CommonErrors.OperationCancelled(
                "transaction.begin.cancelled");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Не удалось создать транзакцию");

            return CommonErrors.Db(
                "transaction.begin.failed",
                "Не удалось начать транзакцию");
        }
    }

    public async Task<UnitResult<Error>> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            return UnitResult.Success<Error>();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Сохранение изменений было отменено");

            return UnitResult.Failure(
                CommonErrors.OperationCancelled(
                    "save.changes.cancelled"));
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(
                exception,
                "Возник конфликт конкурентного изменения данных");

            return UnitResult.Failure(
                CommonErrors.Conflict(
                    "db.concurrency.conflict",
                    "Данные были изменены другой операцией"));
        }
        catch (DbUpdateException exception)
            when (errorTranslator.TryTranslate(
                exception,
                out var mappedError))
        {
            var sqliteException =
                (SqliteException)exception.InnerException!;

            logger.LogWarning(
                exception,
                """
                Нарушено ограничение SQLite.
                Код: {SqliteErrorCode}.
                Расширенный код: {ExtendedErrorCode}.
                Ошибка: {ErrorCode}
                """,
                sqliteException.SqliteErrorCode,
                sqliteException.SqliteExtendedErrorCode,
                mappedError.Code);

            return UnitResult.Failure(mappedError);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(
                exception,
                "Ошибка SQLite при сохранении изменений");

            return UnitResult.Failure(
                CommonErrors.Db(
                    "db.update.failed",
                    "Не удалось сохранить изменения в базе данных"));
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Не удалось сохранить изменения");

            return UnitResult.Failure(
                CommonErrors.Db(
                    "db.save.changes.failed",
                    "Не удалось сохранить изменения в базе данных"));
        }
    }
}