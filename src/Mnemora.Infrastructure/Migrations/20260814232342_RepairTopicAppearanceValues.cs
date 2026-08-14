using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mnemora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RepairTopicAppearanceValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE topics
                SET color = 'Teal'
                WHERE color IS NULL OR trim(color) = '';
                """);

            migrationBuilder.Sql(
                """
                UPDATE topics
                SET icon = 'Bookmark'
                WHERE icon IS NULL OR trim(icon) = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
