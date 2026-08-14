using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mnemora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NoCaseFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "topics",
                type: "TEXT",
                maxLength: 150,
                nullable: false,
                collation: "MNEMORA_UNICODE_NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "sections",
                type: "TEXT",
                maxLength: 150,
                nullable: false,
                collation: "MNEMORA_UNICODE_NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 150);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "topics",
                type: "TEXT",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 150,
                oldCollation: "MNEMORA_UNICODE_NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "sections",
                type: "TEXT",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 150,
                oldCollation: "MNEMORA_UNICODE_NOCASE");
        }
    }
}
