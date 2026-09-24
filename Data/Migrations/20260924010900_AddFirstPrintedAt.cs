using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFirstPrintedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstPrintedAt",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstPrintedAt",
                table: "YearlyArchiveRegisterRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPrintedAt",
                table: "YearlyArchiveRegisterRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrintCount",
                table: "YearlyArchiveRegisterRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstPrintedAt",
                table: "YearlyArchiveOutboundRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstPrintedAt",
                table: "YearlyArchiveDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstPrintedAt",
                table: "NetworkOutboundRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstPrintedAt",
                table: "NetworkOnNetDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstPrintedAt",
                table: "NetworkInboundRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstPrintedAt",
                table: "HistoryArchiveDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstPrintedAt",
                table: "HardDiskMediaApplications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstPrintedAt",
                table: "HardDiskDisposalRecords",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstPrintedAt",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "FirstPrintedAt",
                table: "YearlyArchiveRegisterRecords");

            migrationBuilder.DropColumn(
                name: "LastPrintedAt",
                table: "YearlyArchiveRegisterRecords");

            migrationBuilder.DropColumn(
                name: "PrintCount",
                table: "YearlyArchiveRegisterRecords");

            migrationBuilder.DropColumn(
                name: "FirstPrintedAt",
                table: "YearlyArchiveOutboundRecords");

            migrationBuilder.DropColumn(
                name: "FirstPrintedAt",
                table: "YearlyArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "FirstPrintedAt",
                table: "NetworkOutboundRecords");

            migrationBuilder.DropColumn(
                name: "FirstPrintedAt",
                table: "NetworkOnNetDisposalRecords");

            migrationBuilder.DropColumn(
                name: "FirstPrintedAt",
                table: "NetworkInboundRecords");

            migrationBuilder.DropColumn(
                name: "FirstPrintedAt",
                table: "HistoryArchiveDisposalRecords");

            migrationBuilder.DropColumn(
                name: "FirstPrintedAt",
                table: "HardDiskMediaApplications");

            migrationBuilder.DropColumn(
                name: "FirstPrintedAt",
                table: "HardDiskDisposalRecords");
        }
    }
}
