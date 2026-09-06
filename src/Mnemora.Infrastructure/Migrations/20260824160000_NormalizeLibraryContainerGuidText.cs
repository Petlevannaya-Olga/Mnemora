using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mnemora.Infrastructure.Persistence;

namespace Mnemora.Infrastructure.Migrations;

[DbContext(typeof(MnemoraDbContext))]
[Migration("20260824160000_NormalizeLibraryContainerGuidText")]
public sealed class NormalizeLibraryContainerGuidText : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Старые root-контейнеры создавались SQL-ом в lower-case, а Guid-параметры
        // Microsoft.Data.Sqlite сериализует в upper-case. Для TEXT это разные значения.
        // На время этой транзакции откладываем проверку FK, чтобы синхронно обновить
        // внешние ссылки и ключи контейнеров без разрыва связей.
        migrationBuilder.Sql("PRAGMA defer_foreign_keys = ON;");

        migrationBuilder.Sql(
            """
            UPDATE materials
            SET container_id = upper(container_id)
            WHERE container_id <> upper(container_id);
            """);

        migrationBuilder.Sql(
            """
            UPDATE library_containers
            SET parent_id = upper(parent_id)
            WHERE parent_id IS NOT NULL
              AND parent_id <> upper(parent_id);
            """);

        migrationBuilder.Sql(
            """
            UPDATE library_containers
            SET id = upper(id)
            WHERE id <> upper(id);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Нормализация регистра не меняет значение Guid и не имеет корректного
        // обратного преобразования к исторически смешанному представлению.
    }
}
