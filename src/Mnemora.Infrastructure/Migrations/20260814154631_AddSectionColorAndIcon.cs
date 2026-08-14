using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mnemora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSectionColorAndIcon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "sections",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "icon",
                table: "sections",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "color",
                table: "sections");

            migrationBuilder.DropColumn(
                name: "icon",
                table: "sections");
        }
    }
}
