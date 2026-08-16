using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mnemora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryDisplayOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "display_order",
                table: "topics",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2147483647);

            migrationBuilder.AddColumn<int>(
                name: "display_order",
                table: "sections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2147483647);

            migrationBuilder.AddColumn<int>(
                name: "display_order",
                table: "materials",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2147483647);

            migrationBuilder.CreateIndex(
                name: "ix_topics_section_id_display_order",
                table: "topics",
                columns: new[] { "section_id", "display_order" });

            migrationBuilder.CreateIndex(
                name: "ix_sections_display_order",
                table: "sections",
                column: "display_order");

            migrationBuilder.CreateIndex(
                name: "ix_materials_topic_id_display_order",
                table: "materials",
                columns: new[] { "topic_id", "display_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_topics_section_id_display_order",
                table: "topics");

            migrationBuilder.DropIndex(
                name: "ix_sections_display_order",
                table: "sections");

            migrationBuilder.DropIndex(
                name: "ix_materials_topic_id_display_order",
                table: "materials");

            migrationBuilder.DropColumn(
                name: "display_order",
                table: "topics");

            migrationBuilder.DropColumn(
                name: "display_order",
                table: "sections");

            migrationBuilder.DropColumn(
                name: "display_order",
                table: "materials");
        }
    }
}
