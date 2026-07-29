using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveHardDiskDisposalReasonToItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisposalReason",
                table: "HardDiskDisposalItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // 存量明细：先按主表整单原因回填（旧「损毁」规范为「损坏」）。
            migrationBuilder.Sql(
                """
                UPDATE HardDiskDisposalItems
                SET DisposalReason = IFNULL((
                    SELECT CASE TRIM(r.DisposalReason)
                        WHEN '损毁' THEN '损坏'
                        ELSE TRIM(r.DisposalReason)
                    END
                    FROM HardDiskDisposalRecords r
                    WHERE r.Id = HardDiskDisposalItems.DisposalRecordId
                ), '');
                """);

            // 主表原因为空时，按处置前介质状态回推。
            migrationBuilder.Sql(
                """
                UPDATE HardDiskDisposalItems
                SET DisposalReason = CASE TRIM(BeforeMediaStatus)
                    WHEN '在库(空盘)' THEN '淘汰'
                    WHEN '在库(损坏)' THEN '损坏'
                    WHEN '在库(盘失)' THEN '盘失'
                    ELSE DisposalReason
                END
                WHERE TRIM(IFNULL(DisposalReason, '')) = '';
                """);

            migrationBuilder.Sql(
                """
                UPDATE HardDiskDisposalItems
                SET DisposalReason = '损坏'
                WHERE DisposalReason = '损毁';
                """);

            migrationBuilder.Sql(
                """
                UPDATE HardDiskDisposalRecords
                SET DisposalReason = REPLACE(DisposalReason, '损毁', '损坏')
                WHERE DisposalReason LIKE '%损毁%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisposalReason",
                table: "HardDiskDisposalItems");
        }
    }
}
