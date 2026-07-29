using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveHardDiskDisposalMethodToItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DispositionMethod",
                table: "HardDiskDisposalItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // 存量明细：先按主表整单处置方式回填。
            migrationBuilder.Sql(
                """
                UPDATE HardDiskDisposalItems
                SET DispositionMethod = IFNULL((
                    SELECT TRIM(r.DispositionMethod)
                    FROM HardDiskDisposalRecords r
                    WHERE r.Id = HardDiskDisposalItems.DisposalRecordId
                ), '');
                """);

            // 在库(盘失)明细：统一规范为「库内注销」。
            migrationBuilder.Sql(
                """
                UPDATE HardDiskDisposalItems
                SET DispositionMethod = '库内注销'
                WHERE TRIM(IFNULL(BeforeMediaStatus, '')) = '在库(盘失)';
                """);

            // 主表汇总按明细去重重建。
            migrationBuilder.Sql(
                """
                UPDATE HardDiskDisposalRecords
                SET DispositionMethod = IFNULL((
                    SELECT REPLACE(GROUP_CONCAT(DISTINCT TRIM(i.DispositionMethod)), ',', '、')
                    FROM HardDiskDisposalItems i
                    WHERE i.DisposalRecordId = HardDiskDisposalRecords.Id
                      AND TRIM(IFNULL(i.DispositionMethod, '')) <> ''
                ), DispositionMethod);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DispositionMethod",
                table: "HardDiskDisposalItems");
        }
    }
}
