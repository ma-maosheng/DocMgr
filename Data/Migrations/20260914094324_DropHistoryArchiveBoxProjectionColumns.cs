using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropHistoryArchiveBoxProjectionColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite 删列走 ef_temp_ 表重建；清掉上次失败残留的临时表，避免重试报 already exists。
            migrationBuilder.Sql("DROP TABLE IF EXISTS ef_temp_HistoryArchiveBoxes;");

            migrationBuilder.DropIndex(
                name: "IX_HistoryArchiveBoxes_CabinetName_Side_Row_Column",
                table: "HistoryArchiveBoxes");

            migrationBuilder.DropColumn(
                name: "BoxIndex",
                table: "HistoryArchiveBoxes");

            migrationBuilder.DropColumn(
                name: "CabinetName",
                table: "HistoryArchiveBoxes");

            migrationBuilder.DropColumn(
                name: "Column",
                table: "HistoryArchiveBoxes");

            migrationBuilder.DropColumn(
                name: "Row",
                table: "HistoryArchiveBoxes");

            migrationBuilder.DropColumn(
                name: "Side",
                table: "HistoryArchiveBoxes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS ef_temp_HistoryArchiveBoxes;");

            migrationBuilder.AddColumn<int>(
                name: "BoxIndex",
                table: "HistoryArchiveBoxes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CabinetName",
                table: "HistoryArchiveBoxes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Column",
                table: "HistoryArchiveBoxes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Row",
                table: "HistoryArchiveBoxes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Side",
                table: "HistoryArchiveBoxes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveBoxes_CabinetName_Side_Row_Column",
                table: "HistoryArchiveBoxes",
                columns: new[] { "CabinetName", "Side", "Row", "Column" });
        }
    }
}
