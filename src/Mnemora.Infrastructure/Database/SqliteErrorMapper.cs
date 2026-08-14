using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mnemora.Domain.Sections;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Database;

internal static class SqliteErrorMapper
{
    private const int SQLITE_CONSTRAINT_PRIMARY_KEY = 1555;
    private const int SQLITE_CONSTRAINT_UNIQUE = 2067;

    public static bool TryMap(
        DbUpdateException exception,
        out Error error)
    {
        error = null!;

        if (exception.InnerException is not SqliteException sqliteException)
        {
            return false;
        }

        if (sqliteException.SqliteExtendedErrorCode
            is not SQLITE_CONSTRAINT_UNIQUE
            and not SQLITE_CONSTRAINT_PRIMARY_KEY)
        {
            return false;
        }

        if (sqliteException.Message.Contains(
                "sections.name",
                StringComparison.OrdinalIgnoreCase))
        {
            error = new Error(
                "section.name.already.exists",
                "Раздел с таким названием уже существует",
                ErrorType.CONFLICT,
                nameof(Section.Name));

            return true;
        }

        error = CommonErrors.Conflict(
            "db.unique.constraint.violation",
            "Запись с такими данными уже существует");

        return true;
    }
}