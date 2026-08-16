using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNetworkOutboundRegisterMediaRequisitionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedReturnDate",
                table: "YearlyArchiveRegisterMedias",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequisitionedDiskNeedReturn",
                table: "YearlyArchiveRegisterMedias",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RequisitionedHardDiskCode",
                table: "YearlyArchiveRegisterMedias",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RequisitionedMediumId",
                table: "YearlyArchiveRegisterMedias",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseInStockBlankHardDisk",
                table: "YearlyArchiveRegisterMedias",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedReturnDate",
                table: "YearlyArchiveRegisterMedias");

            migrationBuilder.DropColumn(
                name: "RequisitionedDiskNeedReturn",
                table: "YearlyArchiveRegisterMedias");

            migrationBuilder.DropColumn(
                name: "RequisitionedHardDiskCode",
                table: "YearlyArchiveRegisterMedias");

            migrationBuilder.DropColumn(
                name: "RequisitionedMediumId",
                table: "YearlyArchiveRegisterMedias");

            migrationBuilder.DropColumn(
                name: "UseInStockBlankHardDisk",
                table: "YearlyArchiveRegisterMedias");
        }
    }
}
