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
    private const string MaterialContainerMigrationId =
        "20260823093103_AddMaterialContainerId";
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

            await RecoverInterruptedMaterialContainerMigrationAsync(dbContext, cancellationToken);
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

    private async Task RecoverInterruptedMaterialContainerMigrationAsync(
        MnemoraDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = (SqliteConnection)dbContext.Database.GetDbConnection();
        bool shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
            await dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            bool historyExists = await TableExistsAsync(
                connection, "__EFMigrationsHistory", cancellationToken);
            bool materialsExists = await TableExistsAsync(
                connection, "materials", cancellationToken);

            if (!historyExists || !materialsExists)
                return;

            bool migrationApplied = await MigrationAppliedAsync(
                connection, MaterialContainerMigrationId, cancellationToken);
            bool containerColumnExists = await ColumnExistsAsync(
                connection, "materials", "container_id", cancellationToken);

            if (migrationApplied || !containerColumnExists)
                return;

            logger.LogWarning(
                "Обнаружена частично применённая миграция {MigrationId}. Восстанавливаем схему перед повторным запуском.",
                MaterialContainerMigrationId);

            string[] commands =
            [
                "DROP TABLE IF EXISTS ef_temp_materials;",
                "DROP INDEX IF EXISTS ix_materials_container_id;",
                "DROP INDEX IF EXISTS ix_materials_container_id_created_at_id;",
                "DROP INDEX IF EXISTS ix_materials_container_id_display_order_id;",
                "DROP INDEX IF EXISTS ix_materials_container_id_title_id;",
                "DROP INDEX IF EXISTS ix_materials_container_id_type;",
                "DROP INDEX IF EXISTS ix_materials_container_id_updated_at_id;",
                "ALTER TABLE materials DROP COLUMN container_id;",
            ];

            foreach (string sql in commands)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            logger.LogInformation(
                "Схема после частично применённой миграции {MigrationId} восстановлена.",
                MaterialContainerMigrationId);
        }
        finally
        {
            if (shouldClose)
                await dbContext.Database.CloseConnectionAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
        command.Parameters.AddWithValue("$name", tableName);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<bool> MigrationAppliedAsync(
        SqliteConnection connection,
        string migrationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", migrationId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<bool> ColumnExistsAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}