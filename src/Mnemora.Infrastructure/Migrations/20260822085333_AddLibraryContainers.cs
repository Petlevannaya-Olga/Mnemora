using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mnemora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryContainers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "library_containers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    section_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    parent_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    depth = table.Column<int>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true, collation: "MNEMORA_UNICODE_NOCASE"),
                    color = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    icon = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    display_order = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 2147483647)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_library_containers", x => x.id);
                    table.UniqueConstraint("ak_library_containers_id_section_id", x => new { x.id, x.section_id });
                    table.CheckConstraint("ck_library_containers_display_order", "display_order >= 0");
                    table.CheckConstraint("ck_library_containers_shape", "(parent_id IS NULL AND depth = 0 AND name IS NULL AND color IS NULL AND icon IS NULL) OR (parent_id IS NOT NULL AND depth BETWEEN 1 AND 3 AND name IS NOT NULL AND color IS NOT NULL AND icon IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_library_containers_library_containers_parent_id_section_id",
                        columns: x => new { x.parent_id, x.section_id },
                        principalTable: "library_containers",
                        principalColumns: new[] { "id", "section_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_library_containers_sections_section_id",
                        column: x => x.section_id,
                        principalTable: "sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Существующие разделы получают технический root-контейнер.
            // Root имеет собственный идентификатор: он не является самим Section.
            migrationBuilder.Sql(
                """
                INSERT INTO library_containers
                (
                    id,
                    section_id,
                    parent_id,
                    depth,
                    name,
                    color,
                    icon,
                    created_at,
                    updated_at,
                    display_order
                )
                SELECT
                    lower(
                        hex(randomblob(4)) || '-' ||
                        hex(randomblob(2)) || '-' ||
                        hex(randomblob(2)) || '-' ||
                        hex(randomblob(2)) || '-' ||
                        hex(randomblob(6))
                    ),
                    id,
                    NULL,
                    0,
                    NULL,
                    NULL,
                    NULL,
                    created_at,
                    updated_at,
                    2147483647
                FROM sections;
                """);

            // Каждая существующая Topic становится папкой первого уровня.
            // Намеренно сохраняем Topic.Id как LibraryContainer.Id. Благодаря этому
            // на следующем этапе material.topic_id можно безопасно превратить
            // в material.container_id без переназначения самих идентификаторов.
            migrationBuilder.Sql(
                """
                INSERT INTO library_containers
                (
                    id,
                    section_id,
                    parent_id,
                    depth,
                    name,
                    color,
                    icon,
                    created_at,
                    updated_at,
                    display_order
                )
                SELECT
                    topic.id,
                    topic.section_id,
                    root.id,
                    1,
                    topic.name,
                    topic.color,
                    topic.icon,
                    topic.created_at,
                    topic.updated_at,
                    topic.display_order
                FROM topics AS topic
                INNER JOIN library_containers AS root
                    ON root.section_id = topic.section_id
                   AND root.parent_id IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_library_containers_parent_id_display_order",
                table: "library_containers",
                columns: new[] { "parent_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "IX_library_containers_parent_id_section_id",
                table: "library_containers",
                columns: new[] { "parent_id", "section_id" });

            migrationBuilder.CreateIndex(
                name: "ix_library_containers_section_id_depth_display_order",
                table: "library_containers",
                columns: new[] { "section_id", "depth", "display_order" });

            migrationBuilder.CreateIndex(
                name: "ux_library_containers_parent_id_name",
                table: "library_containers",
                columns: new[] { "parent_id", "name" },
                unique: true,
                filter: "parent_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_library_containers_section_id_root",
                table: "library_containers",
                column: "section_id",
                unique: true,
                filter: "parent_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "library_containers");
        }
    }
}
