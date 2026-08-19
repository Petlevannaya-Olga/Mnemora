using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mnemora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryPagingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_materials_topic_id_display_order",
                table: "materials");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "materials",
                type: "TEXT",
                maxLength: 150,
                nullable: false,
                collation: "MNEMORA_UNICODE_NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 150);

            migrationBuilder.CreateIndex(
                name: "ix_materials_topic_id_article_id",
                table: "materials",
                columns: new[] { "topic_id", "article_id" });

            migrationBuilder.CreateIndex(
                name: "ix_materials_topic_id_created_at_id",
                table: "materials",
                columns: new[] { "topic_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_materials_topic_id_display_order_id",
                table: "materials",
                columns: new[] { "topic_id", "display_order", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_materials_topic_id_title_id",
                table: "materials",
                columns: new[] { "topic_id", "title", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_materials_topic_id_type",
                table: "materials",
                columns: new[] { "topic_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_materials_topic_id_updated_at_id",
                table: "materials",
                columns: new[] { "topic_id", "updated_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_materials_topic_id_article_id",
                table: "materials");

            migrationBuilder.DropIndex(
                name: "ix_materials_topic_id_created_at_id",
                table: "materials");

            migrationBuilder.DropIndex(
                name: "ix_materials_topic_id_display_order_id",
                table: "materials");

            migrationBuilder.DropIndex(
                name: "ix_materials_topic_id_title_id",
                table: "materials");

            migrationBuilder.DropIndex(
                name: "ix_materials_topic_id_type",
                table: "materials");

            migrationBuilder.DropIndex(
                name: "ix_materials_topic_id_updated_at_id",
                table: "materials");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "materials",
                type: "TEXT",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 150,
                oldCollation: "MNEMORA_UNICODE_NOCASE");

            migrationBuilder.CreateIndex(
                name: "ix_materials_topic_id_display_order",
                table: "materials",
                columns: new[] { "topic_id", "display_order" });
        }
    }
}
