using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mnemora.Infrastructure.Persistence;

internal sealed class SqliteUnicodeCollationInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        RegisterDatabaseFeatures(connection);
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        RegisterDatabaseFeatures(connection);
        return Task.CompletedTask;
    }

    private static void RegisterDatabaseFeatures(DbConnection connection)
    {
        if (connection is not SqliteConnection sqliteConnection)
        {
            return;
        }

        sqliteConnection.CreateCollation(
            SqliteCollations.UnicodeNoCase,
            StringComparer.OrdinalIgnoreCase.Compare);

        sqliteConnection.CreateFunction<string?, string?, bool>(
            SqliteFunctions.UnicodeContains,
            UnicodeContains,
            isDeterministic: true);
    }

    private static bool UnicodeContains(string? source, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        return source?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
    }
}