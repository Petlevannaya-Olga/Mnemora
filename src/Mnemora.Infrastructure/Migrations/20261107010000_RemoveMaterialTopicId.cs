using Microsoft.EntityFrameworkCore.Migrations;

namespace Mnemora.Infrastructure.Migrations;

/// <summary>
/// The authoritative location of an article or question is now materials.container_id.
/// Remove the obsolete topic_id bridge only after preserving all existing materials.
/// This migration intentionally keeps the topics table: dropping it is a separate step.
/// </summary>
public partial class RemoveMaterialTopicId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Refuse to discard the legacy column if the new container relation is damaged.
        // A CHECK constraint makes this SQL fail transactionally for orphaned rows.
        migrationBuilder.Sql(
            """
            CREATE TEMP TABLE __mnemora_material_container_guard
            (
                is_valid INTEGER NOT NULL CHECK (is_valid = 1)
            );

            INSERT INTO __mnemora_material_container_guard (is_valid)
            SELECT CASE WHEN EXISTS
            (
                SELECT 1 FROM materials AS m
                LEFT JOIN library_containers AS c ON c.id = m.container_id
                WHERE m.container_id IS NULL OR c.id IS NULL
            ) THEN 0 ELSE 1 END;

            DROP TABLE __mnemora_material_container_guard;
            """);

        migrationBuilder.DropIndex(
            name: "ix_materials_topic_id_article_id", table: "materials");
        migrationBuilder.DropIndex(
            name: "ix_materials_topic_id_created_at_id", table: "materials");
        migrationBuilder.DropIndex(
            name: "ix_materials_topic_id_display_order_id", table: "materials");
        migrationBuilder.DropIndex(
            name: "ix_materials_topic_id_title_id", table: "materials");
        migrationBuilder.DropIndex(
            name: "ix_materials_topic_id_type", table: "materials");
        migrationBuilder.DropIndex(
            name: "ix_materials_topic_id_updated_at_id", table: "materials");
        migrationBuilder.DropIndex(
            name: "ix_materials_topic_id", table: "materials");

        // SQLite native DROP COLUMN is supported by the runtime used by the application.
        // Using migrationBuilder.DropColumn() makes the EF Core SQLite provider
        // rebuild the table and generate PRAGMA foreign_keys = 0 outside a transaction.
        // The old FK was already removed by DetachMaterialsFromTopics, and every
        // remaining index referencing topic_id has been dropped above.
        // Run the native operation within this migration transaction instead.
        migrationBuilder.Sql("ALTER TABLE \"materials\" DROP COLUMN \"topic_id\";");

        migrationBuilder.CreateIndex(
            name: "ix_materials_container_id_article_id",
            table: "materials",
            columns: new[] { "container_id", "article_id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Downgrading recreates topic_id from container_id. There is no longer
        // an authoritative legacy Topic mapping for nested folders and roots.
        migrationBuilder.DropIndex(
            name: "ix_materials_container_id_article_id", table: "materials");

        // Adding a NOT NULL column without a constant default is not supported
        // by SQLite when the table already contains rows. A temporary-value
        // DEFAULT allows a native ADD COLUMN without table rebuilding; the next
        // statement backfills all existing rows from their container IDs.
        // The default remains in SQLite after rollback, but all old rows are
        // restored and subsequent old application writes provide topic_id.
        migrationBuilder.Sql(
            """
            ALTER TABLE "materials"
            ADD COLUMN "topic_id" TEXT NOT NULL
            DEFAULT '00000000-0000-0000-0000-000000000000';

            UPDATE "materials" SET "topic_id" = "container_id";
            """);

        migrationBuilder.CreateIndex(
            name: "ix_materials_topic_id", table: "materials", column: "topic_id");
        migrationBuilder.CreateIndex(
            name: "ix_materials_topic_id_type", table: "materials",
            columns: new[] { "topic_id", "type" });
        migrationBuilder.CreateIndex(
            name: "ix_materials_topic_id_created_at_id", table: "materials",
            columns: new[] { "topic_id", "created_at", "id" });
        migrationBuilder.CreateIndex(
            name: "ix_materials_topic_id_display_order_id", table: "materials",
            columns: new[] { "topic_id", "display_order", "id" });
        migrationBuilder.CreateIndex(
            name: "ix_materials_topic_id_title_id", table: "materials",
            columns: new[] { "topic_id", "title", "id" });
        migrationBuilder.CreateIndex(
            name: "ix_materials_topic_id_updated_at_id", table: "materials",
            columns: new[] { "topic_id", "updated_at", "id" });
        migrationBuilder.CreateIndex(
            name: "ix_materials_topic_id_article_id", table: "materials",
            columns: new[] { "topic_id", "article_id" });
    }
}
