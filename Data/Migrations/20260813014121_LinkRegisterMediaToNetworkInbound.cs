using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkRegisterMediaToNetworkInbound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "YearlyArchiveRegisterRecordId",
                table: "YearlyArchiveRegisterMedias",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "NetworkInboundRecordId",
                table: "YearlyArchiveRegisterMedias",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetServerPath",
                table: "NetworkInboundRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterMedias_NetworkInboundRecordId",
                table: "YearlyArchiveRegisterMedias",
                column: "NetworkInboundRecordId");

            migrationBuilder.AddForeignKey(
                name: "FK_YearlyArchiveRegisterMedias_NetworkInboundRecords_NetworkInboundRecordId",
                table: "YearlyArchiveRegisterMedias",
                column: "NetworkInboundRecordId",
                principalTable: "NetworkInboundRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_YearlyArchiveRegisterMedias_NetworkInboundRecords_NetworkInboundRecordId",
                table: "YearlyArchiveRegisterMedias");

            migrationBuilder.DropIndex(
                name: "IX_YearlyArchiveRegisterMedias_NetworkInboundRecordId",
                table: "YearlyArchiveRegisterMedias");

            migrationBuilder.DropColumn(
                name: "NetworkInboundRecordId",
                table: "YearlyArchiveRegisterMedias");

            migrationBuilder.DropColumn(
                name: "TargetServerPath",
                table: "NetworkInboundRecords");

            migrationBuilder.AlterColumn<int>(
                name: "YearlyArchiveRegisterRecordId",
                table: "YearlyArchiveRegisterMedias",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
