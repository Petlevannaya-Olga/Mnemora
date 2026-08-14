using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Database.Errors;

internal sealed class SqliteErrorTranslator(
    IEnumerable<ISqliteErrorHandler> errorHandlers)
{
    private const int SQLITE_CONSTRAINT_PRIMARY_KEY = 1555;
    private const int SQLITE_CONSTRAINT_UNIQUE = 2067;

    public bool TryTranslate(
        DbUpdateException exception,
        out Error error)
    {
        error = null!;

        if (exception.InnerException is not SqliteException sqliteException)
        {
            return false;
        }

        foreach (var errorHandler in errorHandlers)
        {
            if (errorHandler.TryMap(exception, sqliteException, out error))
            {
                return true;
            }
        }

        if (sqliteException.SqliteExtendedErrorCode
            is SQLITE_CONSTRAINT_PRIMARY_KEY
            or SQLITE_CONSTRAINT_UNIQUE)
        {
            error = CommonErrors.Conflict(
                "db.unique.constraint.violation",
                "Запись с такими данными уже существует");

            return true;
        }

        return false;
    }
}