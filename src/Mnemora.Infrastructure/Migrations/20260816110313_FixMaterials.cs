using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mnemora.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixMaterials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_material_tags",
                table: "material_tags");

            migrationBuilder.DropIndex(
                name: "IX_material_tags_material_id_value",
                table: "material_tags");

            migrationBuilder.DropColumn(
                name: "id",
                table: "material_tags");

            migrationBuilder.RenameIndex(
                name: "IX_materials_topic_id",
                table: "materials",
                newName: "ix_materials_topic_id");

            migrationBuilder.RenameIndex(
                name: "IX_materials_article_id",
                table: "materials",
                newName: "ix_materials_article_id");

            migrationBuilder.AlterColumn<string>(
                name: "value",
                table: "material_tags",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                collation: "MNEMORA_UNICODE_NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50);

            migrationBuilder.AddPrimaryKey(
                name: "PK_material_tags",
                table: "material_tags",
                columns: new[] { "material_id", "value" });

            migrationBuilder.CreateIndex(
                name: "ix_material_tags_value",
                table: "material_tags",
                column: "value");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_material_tags",
                table: "material_tags");

            migrationBuilder.DropIndex(
                name: "ix_material_tags_value",
                table: "material_tags");

            migrationBuilder.RenameIndex(
                name: "ix_materials_topic_id",
                table: "materials",
                newName: "IX_materials_topic_id");

            migrationBuilder.RenameIndex(
                name: "ix_materials_article_id",
                table: "materials",
                newName: "IX_materials_article_id");

            migrationBuilder.AlterColumn<string>(
                name: "value",
                table: "material_tags",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 50,
                oldCollation: "MNEMORA_UNICODE_NOCASE");

            migrationBuilder.AddColumn<Guid>(
                name: "id",
                table: "material_tags",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE material_tags
                SET id =
                    lower(
                        hex(randomblob(4)) || '-' ||
                        hex(randomblob(2)) || '-' ||
                        hex(randomblob(2)) || '-' ||
                        hex(randomblob(2)) || '-' ||
                        hex(randomblob(6))
                    );
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "material_tags",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_material_tags",
                table: "material_tags",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "IX_material_tags_material_id_value",
                table: "material_tags",
                columns: new[] { "material_id", "value" },
                unique: true);
        }
    }
}
