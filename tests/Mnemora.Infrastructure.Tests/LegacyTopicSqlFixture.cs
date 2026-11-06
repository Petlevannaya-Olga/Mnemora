using Microsoft.EntityFrameworkCore;
using Mnemora.Domain.Sections;
using Mnemora.Infrastructure.Persistence;

namespace Mnemora.Infrastructure.Tests;

/// <summary>
/// Seed the schema of a HISTORICAL migration without registering Topic in the current EF model.
/// Only call after migrating to a version that still contains the topics table.
/// </summary>
internal static class LegacyTopicSqlFixture
{
    public static Task<int> InsertAsync(
        MnemoraDbContext db,
        SectionId sectionId,
        Guid topicId,
        string name,
        string color,
        string icon,
        CancellationToken ct,
        int displayOrder = int.MaxValue)
    {
        DateTime now = DateTime.UtcNow;
        return db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO topics
            (id, section_id, name, color, icon, created_at, updated_at, display_order)
            VALUES
            ({topicId}, {sectionId.Value}, {name}, {color}, {icon}, {now}, {now}, {displayOrder});
            """, ct);
    }
}
