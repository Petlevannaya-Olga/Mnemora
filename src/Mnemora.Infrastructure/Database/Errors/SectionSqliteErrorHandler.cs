using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mnemora.Domain.Sections;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Database.Errors;

internal sealed class SectionSqliteErrorHandler : ISqliteErrorHandler
{
    private const int SQLITE_CONSTRAINT_UNIQUE = 2067;

    public bool TryMap(
        DbUpdateException exception,
        SqliteException sqliteException,
        out Error error)
    {
        error = null!;

        if (sqliteException.SqliteExtendedErrorCode != SQLITE_CONSTRAINT_UNIQUE)
        {
            return false;
        }

        var containsSection = exception
            .Entries
            .Any(entry => entry.Entity is Section);

        if (!containsSection)
        {
            return false;
        }

        error = new Error(
            "section.name.already.exists",
            "Раздел с таким названием уже существует",
            ErrorType.CONFLICT,
            nameof(Section.Name));

        return true;
    }
}