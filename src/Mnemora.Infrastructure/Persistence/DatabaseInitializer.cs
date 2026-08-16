using CSharpFunctionalExtensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mnemora.Application.Database;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<DatabaseInitializer> logger)
    : IDatabaseInitializer
{
    public async Task<UnitResult<Error>> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();

            var dbContextFactory =
                scope.ServiceProvider
                    .GetRequiredService<IDbContextFactory<MnemoraDbContext>>();

            await using var dbContext =
                await dbContextFactory.CreateDbContextAsync(
                    cancellationToken);

            await dbContext.Database.MigrateAsync(cancellationToken);

            return UnitResult.Success<Error>();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return CommonErrors.OperationCancelled(
                "database.initialization.cancelled");
        }
        catch (PersistenceConfigurationException exception)
        {
            logger.LogError(
                exception,
                "Ошибка конфигурации базы данных.");

            return exception.Error;
        }
        catch (Exception exception)
            when (exception is SqliteException
                      or IOException
                      or UnauthorizedAccessException)
        {
            logger.LogError(
                exception,
                "Не удалось инициализировать базу данных.");

            return CommonErrors.Db(
                "database.initialization.failed",
                "Не удалось подготовить базу данных Mnemora.");
        }
    }
}