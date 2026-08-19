using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mnemora.Infrastructure.Persistence;

internal sealed class SqliteUnicodeCollationInterceptor
    : DbConnectionInterceptor
{
    private const string UnicodeContainsFunctionName =
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
            SqliteCollations.UnicodeNoCase,
            StringComparer.OrdinalIgnoreCase.Compare);

        sqliteConnection.CreateFunction<string?, string?, bool>(
            UnicodeContainsFunctionName,
            static (source, search) =>
                source is not null &&
                search is not null &&
                source.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase));
    }
}
