using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mnemora.Domain.Topics;
using Mnemora.Shared;

namespace Mnemora.Infrastructure.Database.Errors;

internal sealed class TopicSqliteErrorHandler : ISqliteErrorHandler
{
    public bool TryMap(
        DbUpdateException exception,
        SqliteException sqliteException,
        out Error error)
    {
        var containsTopic = exception.Entries
            .Any(entry => entry.Entity is Topic);

        if (!containsTopic)
        {
            error = null!;
            return false;
        }

        switch (sqliteException.SqliteExtendedErrorCode)
        {
            case SqliteExtendedErrorCodes.UniqueConstraint:
                error = CommonErrors.Conflict(
                    "topic.name.already.exists",
                    "Тема с таким названием уже существует в выбранном разделе");

                return true;

            case SqliteExtendedErrorCodes.ForeignKeyConstraint:
                error = CommonErrors.NotFound(
                    "topic.section.not.found",
                    "Раздел, в котором создаётся тема, больше не существует");

                return true;

            default:
                error = null!;
                return false;
        }
    }
}