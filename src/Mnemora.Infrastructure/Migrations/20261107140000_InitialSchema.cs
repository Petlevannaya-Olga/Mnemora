using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mnemora.Infrastructure.Migrations;

/// <summary>
/// The initial schema for a fresh Mnemora database.
/// </summary>
public partial class InitialSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "sections",
            columns: table => new
            {
                id = table.Column<Guid>(type: "TEXT", nullable: false),
                name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false,
                    collation: "MNEMORA_UNICODE_NOCASE"),
                color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                icon = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                display_order = table.Column<int>(type: "INTEGER", nullable: false,
                    defaultValue: int.MaxValue)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_sections", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "library_containers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "TEXT", nullable: false),
                section_id = table.Column<Guid>(type: "TEXT", nullable: false),
                parent_id = table.Column<Guid>(type: "TEXT", nullable: true),
                depth = table.Column<int>(type: "INTEGER", nullable: false),
                name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true,
                    collation: "MNEMORA_UNICODE_NOCASE"),
                color = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                display_order = table.Column<int>(type: "INTEGER", nullable: false,
                    defaultValue: int.MaxValue)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_library_containers", x => x.id);
                table.UniqueConstraint("ak_library_containers_id_section_id",
                    x => new { x.id, x.section_id });
                table.CheckConstraint("ck_library_containers_display_order", "display_order >= 0");
                table.CheckConstraint("ck_library_containers_shape",
                    "(parent_id IS NULL AND depth = 0 AND name IS NULL AND color IS NULL AND icon IS NULL) " +
                    "OR (parent_id IS NOT NULL AND depth BETWEEN 1 AND 3 " +
                    "AND name IS NOT NULL AND color IS NOT NULL AND icon IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_library_containers_sections_section_id",
                    column: x => x.section_id,
                    principalTable: "sections", principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_library_containers_library_containers_parent_id_section_id",
                    columns: x => new { x.parent_id, x.section_id },
                    principalTable: "library_containers",
                    principalColumns: new[] { "id", "section_id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "materials",
            columns: table => new
            {
                id = table.Column<Guid>(type: "TEXT", nullable: false),
                container_id = table.Column<Guid>(type: "TEXT", nullable: false),
                title = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false,
                    collation: "MNEMORA_UNICODE_NOCASE"),
                difficulty = table.Column<int>(type: "INTEGER", nullable: false),
                icon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                study_points = table.Column<int>(type: "INTEGER", nullable: false),
                review_points = table.Column<int>(type: "INTEGER", nullable: false),
                learning_revision = table.Column<int>(type: "INTEGER", nullable: false),
                created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                display_order = table.Column<int>(type: "INTEGER", nullable: false,
                    defaultValue: int.MaxValue),
                type = table.Column<int>(type: "INTEGER", nullable: false),
                article_id = table.Column<Guid>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_materials", x => x.id);
                table.ForeignKey(
                    name: "FK_materials_library_containers_container_id",
                    column: x => x.container_id,
                    principalTable: "library_containers", principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_materials_materials_article_id",
                    column: x => x.article_id,
                    principalTable: "materials", principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "material_tags",
            columns: table => new
            {
                material_id = table.Column<Guid>(type: "TEXT", nullable: false),
                value = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false,
                    collation: "MNEMORA_UNICODE_NOCASE")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_material_tags", x => new { x.material_id, x.value });
                table.ForeignKey(
                    name: "FK_material_tags_materials_material_id",
                    column: x => x.material_id,
                    principalTable: "materials", principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "ux_sections_name", table: "sections",
            column: "name", unique: true);
        migrationBuilder.CreateIndex(name: "ix_sections_display_order", table: "sections",
            column: "display_order");

        migrationBuilder.CreateIndex(name: "IX_library_containers_parent_id_section_id",
            table: "library_containers", columns: new[] { "parent_id", "section_id" });
        migrationBuilder.CreateIndex(name: "ix_library_containers_parent_id_display_order",
            table: "library_containers", columns: new[] { "parent_id", "display_order" });
        migrationBuilder.CreateIndex(name: "ux_library_containers_parent_id_name",
            table: "library_containers", columns: new[] { "parent_id", "name" },
            unique: true, filter: "parent_id IS NOT NULL");
        migrationBuilder.CreateIndex(name: "ix_library_containers_section_id_depth_display_order",
            table: "library_containers", columns: new[] { "section_id", "depth", "display_order" });
        migrationBuilder.CreateIndex(name: "ux_library_containers_section_id_root",
            table: "library_containers", column: "section_id", unique: true,
            filter: "parent_id IS NULL");

        migrationBuilder.CreateIndex(name: "ix_materials_container_id",
            table: "materials", column: "container_id");
        migrationBuilder.CreateIndex(name: "ix_materials_container_id_type",
            table: "materials", columns: new[] { "container_id", "type" });
        migrationBuilder.CreateIndex(name: "ix_materials_container_id_created_at_id",
            table: "materials", columns: new[] { "container_id", "created_at", "id" });
        migrationBuilder.CreateIndex(name: "ix_materials_container_id_display_order_id",
            table: "materials", columns: new[] { "container_id", "display_order", "id" });
        migrationBuilder.CreateIndex(name: "ix_materials_container_id_title_id",
            table: "materials", columns: new[] { "container_id", "title", "id" });
        migrationBuilder.CreateIndex(name: "ix_materials_container_id_updated_at_id",
            table: "materials", columns: new[] { "container_id", "updated_at", "id" });
        migrationBuilder.CreateIndex(name: "ix_materials_article_id",
            table: "materials", column: "article_id");
        migrationBuilder.CreateIndex(name: "ix_materials_container_id_article_id",
            table: "materials", columns: new[] { "container_id", "article_id" });
        migrationBuilder.CreateIndex(name: "ix_material_tags_value",
            table: "material_tags", column: "value");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "material_tags");
        migrationBuilder.DropTable(name: "materials");
        migrationBuilder.DropTable(name: "library_containers");
        migrationBuilder.DropTable(name: "sections");
    }
}
