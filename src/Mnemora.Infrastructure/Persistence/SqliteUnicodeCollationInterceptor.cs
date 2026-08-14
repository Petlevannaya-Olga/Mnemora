using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mnemora.Infrastructure.Persistence;

internal sealed class SqliteUnicodeCollationInterceptor
    : DbConnectionInterceptor
{
    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        RegisterCollation(connection);
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        RegisterCollation(connection);
        return Task.CompletedTask;
    }

    private static void RegisterCollation(DbConnection connection)
    {
        if (connection is not SqliteConnection sqliteConnection)
        {
            return;
        }

        sqliteConnection.CreateCollation(
            SqliteCollations.UnicodeNoCase,
            StringComparer.OrdinalIgnoreCase.Compare);
    }
}