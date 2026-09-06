using Microsoft.EntityFrameworkCore.Migrations;

namespace Mnemora.Infrastructure.Migrations;

public partial class DetachMaterialsFromTopics : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Расположение материала теперь определяется LibraryContainer.
        // topic_id временно сохраняем как переходное поле для старого кода,
        // но больше не требуем существования соответствующей Topic-записи:
        // у корня раздела и вложенных папок такой записи нет.
        migrationBuilder.DropForeignKey(
            name: "FK_materials_topics_topic_id",
            table: "materials");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddForeignKey(
            name: "FK_materials_topics_topic_id",
            table: "materials",
            column: "topic_id",
            principalTable: "topics",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }
}
