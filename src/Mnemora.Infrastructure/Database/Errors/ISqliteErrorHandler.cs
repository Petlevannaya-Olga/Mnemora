using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Database.Errors;

internal interface ISqliteErrorHandler
{
    bool TryMap(
        DbUpdateException exception,
        SqliteException sqliteException,
        out Error error);
}