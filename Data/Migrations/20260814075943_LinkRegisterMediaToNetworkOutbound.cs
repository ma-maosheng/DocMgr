using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkRegisterMediaToNetworkOutbound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NetworkOutboundRecordId",
                table: "YearlyArchiveRegisterMedias",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaterialName",
                table: "NetworkOutboundRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OtherRequests",
                table: "NetworkOutboundRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ServerPath",
                table: "NetworkOutboundRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "OriginOutboundItemId",
                table: "NetworkOnNetAssets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterMedias_NetworkOutboundRecordId",
                table: "YearlyArchiveRegisterMedias",
                column: "NetworkOutboundRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetAssets_OriginOutboundItemId",
                table: "NetworkOnNetAssets",
                column: "OriginOutboundItemId",
                unique: true,
                filter: "[OriginOutboundItemId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_YearlyArchiveRegisterMedias_NetworkOutboundRecords_NetworkOutboundRecordId",
                table: "YearlyArchiveRegisterMedias",
                column: "NetworkOutboundRecordId",
                principalTable: "NetworkOutboundRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_YearlyArchiveRegisterMedias_NetworkOutboundRecords_NetworkOutboundRecordId",
                table: "YearlyArchiveRegisterMedias");

            migrationBuilder.DropIndex(
                name: "IX_YearlyArchiveRegisterMedias_NetworkOutboundRecordId",
                table: "YearlyArchiveRegisterMedias");

            migrationBuilder.DropIndex(
                name: "IX_NetworkOnNetAssets_OriginOutboundItemId",
                table: "NetworkOnNetAssets");

            migrationBuilder.DropColumn(
                name: "NetworkOutboundRecordId",
                table: "YearlyArchiveRegisterMedias");

            migrationBuilder.DropColumn(
                name: "MaterialName",
                table: "NetworkOutboundRecords");

            migrationBuilder.DropColumn(
                name: "OtherRequests",
                table: "NetworkOutboundRecords");

            migrationBuilder.DropColumn(
                name: "ServerPath",
                table: "NetworkOutboundRecords");

            migrationBuilder.DropColumn(
                name: "OriginOutboundItemId",
                table: "NetworkOnNetAssets");
        }
    }
}
