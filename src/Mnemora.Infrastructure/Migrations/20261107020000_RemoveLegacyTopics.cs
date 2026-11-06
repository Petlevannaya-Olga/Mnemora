using Microsoft.EntityFrameworkCore.Migrations;

namespace Mnemora.Infrastructure.Migrations;

/// <summary>
/// Remove the legacy topics table after the container migration and material.topic_id removal.
/// Fail before DROP TABLE if any Topic has no migrated folder with the same ID and section.
/// </summary>
public partial class RemoveLegacyTopics : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TEMP TABLE __mnemora_topic_folder_guard
            (
                is_valid INTEGER NOT NULL CHECK (is_valid = 1)
            );

            INSERT INTO __mnemora_topic_folder_guard (is_valid)
            SELECT CASE WHEN EXISTS
            (
                SELECT 1 FROM topics AS t
                LEFT JOIN library_containers AS c
                  ON c.id = t.id AND c.section_id = t.section_id AND c.depth = 1
                WHERE c.id IS NULL
            ) THEN 0 ELSE 1 END;

            DROP TABLE __mnemora_topic_folder_guard;
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
    }
}
