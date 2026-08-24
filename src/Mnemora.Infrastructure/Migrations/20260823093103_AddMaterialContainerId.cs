using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mnemora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialContainerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Старые БД могли уже отметить AddLibraryContainers как применённую
            // до появления backfill Topic -> folder в той миграции.
            migrationBuilder.Sql(
                """
                INSERT OR IGNORE INTO library_containers
                (
                    id, section_id, parent_id, depth, name, color, icon,
                    created_at, updated_at, display_order
                )
                SELECT
                    topic.id, topic.section_id, root.id, 1, topic.name,
                    topic.color, topic.icon, topic.created_at,
                    topic.updated_at, topic.display_order
                FROM topics AS topic
                INNER JOIN library_containers AS root
                    ON root.section_id = topic.section_id
                   AND root.parent_id IS NULL
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM library_containers AS existing
                    WHERE existing.id = topic.id
                );
                """);

            // Сначала колонка nullable: у существующих строк ещё нет container_id.
            migrationBuilder.AddColumn<Guid>(
                name: "container_id",
                table: "materials",
                type: "TEXT",
                nullable: true);

            // На предыдущем шаге каждая Topic была превращена в папку первого
            // уровня с тем же Guid. Поэтому старый topic_id является корректным
            // идентификатором нового LibraryContainer.
            migrationBuilder.Sql(
                """
                UPDATE materials
                SET container_id = topic_id;
                """);

            // После backfill у каждой существующей строки есть контейнер.
            // Делаем колонку обязательной без постоянного Guid.Empty default.
            migrationBuilder.AlterColumn<Guid>(
                name: "container_id",
                table: "materials",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_materials_container_id",
                table: "materials",
                column: "container_id");

            migrationBuilder.CreateIndex(
                name: "ix_materials_container_id_created_at_id",
                table: "materials",
                columns: new[] { "container_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_materials_container_id_display_order_id",
                table: "materials",
                columns: new[] { "container_id", "display_order", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_materials_container_id_title_id",
                table: "materials",
                columns: new[] { "container_id", "title", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_materials_container_id_type",
                table: "materials",
                columns: new[] { "container_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_materials_container_id_updated_at_id",
                table: "materials",
                columns: new[] { "container_id", "updated_at", "id" });

            migrationBuilder.AddForeignKey(
                name: "FK_materials_library_containers_container_id",
                table: "materials",
                column: "container_id",
                principalTable: "library_containers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_materials_library_containers_container_id",
                table: "materials");

            migrationBuilder.DropIndex(
                name: "ix_materials_container_id",
                table: "materials");

            migrationBuilder.DropIndex(
                name: "ix_materials_container_id_created_at_id",
                table: "materials");

            migrationBuilder.DropIndex(
                name: "ix_materials_container_id_display_order_id",
                table: "materials");

            migrationBuilder.DropIndex(
                name: "ix_materials_container_id_title_id",
                table: "materials");

            migrationBuilder.DropIndex(
                name: "ix_materials_container_id_type",
                table: "materials");

            migrationBuilder.DropIndex(
                name: "ix_materials_container_id_updated_at_id",
                table: "materials");

            migrationBuilder.DropColumn(
                name: "container_id",
                table: "materials");
        }
    }
}
