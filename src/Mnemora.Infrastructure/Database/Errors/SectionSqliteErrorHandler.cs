using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mnemora.Domain.Sections;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Database.Errors;

internal sealed class SectionSqliteErrorHandler : ISqliteErrorHandler
{
    public bool TryMap(
        DbUpdateException exception,
        SqliteException sqliteException,
        out Error error)
    {
        error = null!;

        if (sqliteException.SqliteExtendedErrorCode != SqliteExtendedErrorCodes.UniqueConstraint)
        {
            return false;
        }

        var containsSection = exception.Entries
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