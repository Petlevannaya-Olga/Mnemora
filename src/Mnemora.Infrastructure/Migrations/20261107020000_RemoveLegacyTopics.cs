using Microsoft.EntityFrameworkCore.Migrations;

namespace Mnemora.Infrastructure.Migrations;

/// <summary>
/// Remove the legacy topics table after the container migration and material.topic_id removal.
/// Preserve legacy rows that have no current folder, rather than blocking startup or
/// silently discarding metadata for a deleted/unmigrated topic.
/// </summary>
public partial class RemoveLegacyTopics : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Legacy Topic was a mirror of a top-level folder at the time of the
        // original backfill. Since then, folders may have been moved or deleted,
        // and older databases may store GUID text in a different case.
        // This table is intentionally not in the EF model. It is a recovery archive
        // for old metadata only; the app continues using library_containers.
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS legacy_topic_archive
            (
                id TEXT NOT NULL PRIMARY KEY,
                section_id TEXT NOT NULL,
                name TEXT NOT NULL,
                color TEXT NOT NULL,
                icon TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                display_order INTEGER NOT NULL,
                archived_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            INSERT OR IGNORE INTO legacy_topic_archive
                (id, section_id, name, color, icon, created_at, updated_at, display_order)
            SELECT t.id, t.section_id, t.name, t.color, t.icon,
                   t.created_at, t.updated_at, t.display_order
            FROM topics AS t
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM library_containers AS c
                WHERE upper(c.id) = upper(t.id)
                  AND upper(c.section_id) = upper(t.section_id)
                  AND c.depth BETWEEN 1 AND 3
            );
            """);

        // No current table references topics (the material FK was detached earlier).
        // DROP TABLE is supported transactionally by SQLite; do not use PRAGMA foreign_keys.
        migrationBuilder.DropTable(name: "topics");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "topics",
            columns: table => new
            {
                id = table.Column<Guid>(type: "TEXT", nullable: false),
                section_id = table.Column<Guid>(type: "TEXT", nullable: false),
                name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false,
                    collation: "MNEMORA_UNICODE_NOCASE"),
                color = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                display_order = table.Column<int>(type: "INTEGER", nullable: false,
                    defaultValue: 2147483647)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_topics", x => x.id);
                table.ForeignKey("FK_topics_sections_section_id", x => x.section_id,
                    "sections", "id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ux_topics_section_id_name", table: "topics",
            columns: new[] { "section_id", "name" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_topics_section_id_display_order", table: "topics",
            columns: new[] { "section_id", "display_order" });

        // The previous schema did not support nested folders or root materials.
        // Recover its topic rows from first-level folders only. Material.topic_id is
        // restored by the earlier migration if further downgrade is requested.
        migrationBuilder.Sql(
            """
            INSERT INTO topics (id, section_id, name, color, icon,
                                created_at, updated_at, display_order)
            SELECT id, section_id, name, color, icon,
                   created_at, updated_at, display_order
            FROM library_containers
            WHERE depth = 1;
            """);

        // A downgrade should also recover the legacy rows preserved on upgrade.
        // Ignore a row only if its id/name is already occupied by a current folder.
        // The archive is retained on downgrade so that re-upgrade remains safe.
        migrationBuilder.Sql(
            """
            INSERT OR IGNORE INTO topics
                (id, section_id, name, color, icon, created_at, updated_at, display_order)
            SELECT id, section_id, name, color, icon,
                   created_at, updated_at, display_order
            FROM legacy_topic_archive;
            """);
    }
}
