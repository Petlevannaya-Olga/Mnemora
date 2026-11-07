using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mnemora.Infrastructure.Persistence;

internal sealed class SqliteUnicodeCollationInterceptor
    : DbConnectionInterceptor
{
    private const string UNICODE_CONTAINS_FUNCTION_NAME =
        "MNEMORA_UNICODE_CONTAINS";

    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        RegisterSqliteExtensions(connection);
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        RegisterSqliteExtensions(connection);
        return Task.CompletedTask;
    }

    private static void RegisterSqliteExtensions(
        DbConnection connection)
    {
        if (connection is not SqliteConnection sqliteConnection)
        {
            return;
        }

        sqliteConnection.CreateCollation(
            SqliteCollations.UNICODE_NO_CASE,
            StringComparer.OrdinalIgnoreCase.Compare);

        sqliteConnection.CreateFunction<string?, string?, bool>(
            UNICODE_CONTAINS_FUNCTION_NAME,
            static (source, search) =>
                source is not null &&
                search is not null &&
                source.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase));
    }
}
