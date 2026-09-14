using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropYearlyArchiveBoxStructuralColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite 删列走「建 ef_temp_ 表 → 删原表 → 重命名」的重建流程，
            // 重命名时 SQLite 会重新解析全部视图；若此刻视图仍引用被删的原表，
            // 将报 "error in view vw_ArchiveContainerSummaries: no such table"。
            // 因此先删视图、删列后重建。DROP VIEW IF EXISTS 顺带清掉历史失败残留的 ef_temp_ 表，
            // 避免重试时报 "table ef_temp_YearlyArchiveBoxes already exists"。
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ArchiveContainerSummaries;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS ef_temp_YearlyArchiveBoxes;");

            migrationBuilder.DropColumn(
                name: "BoxIndex",
                table: "YearlyArchiveBoxes");

            migrationBuilder.DropColumn(
                name: "CabinetName",
                table: "YearlyArchiveBoxes");

            migrationBuilder.DropColumn(
                name: "Column",
                table: "YearlyArchiveBoxes");

            migrationBuilder.DropColumn(
                name: "Row",
                table: "YearlyArchiveBoxes");

            migrationBuilder.DropColumn(
                name: "Side",
                table: "YearlyArchiveBoxes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite 删列走 ef_temp_ 表重建，重命名时会重新解析视图；
            // 若视图仍引用被删列的原表将失败，因此 Down 前先删视图（由后续迁移重建）。
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ArchiveContainerSummaries;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS ef_temp_YearlyArchiveBoxes;");

            migrationBuilder.AddColumn<int>(
                name: "BoxIndex",
                table: "YearlyArchiveBoxes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CabinetName",
                table: "YearlyArchiveBoxes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Column",
                table: "YearlyArchiveBoxes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Row",
                table: "YearlyArchiveBoxes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Side",
                table: "YearlyArchiveBoxes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
