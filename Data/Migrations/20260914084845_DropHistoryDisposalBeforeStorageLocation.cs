using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropHistoryDisposalBeforeStorageLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite 删列走 ef_temp_ 表重建；清掉上次失败残留的临时表，避免重试报 already exists。
            migrationBuilder.Sql("DROP TABLE IF EXISTS ef_temp_HistoryArchiveDisposalItems;");

            migrationBuilder.DropColumn(
                name: "BeforeStorageLocation",
                table: "HistoryArchiveDisposalItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS ef_temp_HistoryArchiveDisposalItems;");

            migrationBuilder.AddColumn<string>(
                name: "BeforeStorageLocation",
                table: "HistoryArchiveDisposalItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
