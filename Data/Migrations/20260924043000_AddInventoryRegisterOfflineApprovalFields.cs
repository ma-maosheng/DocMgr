using System;
using DocMgr.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <summary>
    /// 盘库登记（硬盘 / 年度资料）补齐 B 流线下签批字段。
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260924043000_AddInventoryRegisterOfflineApprovalFields")]
    public class AddInventoryRegisterOfflineApprovalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            AddOfflineApprovalColumns(migrationBuilder, "HardDiskInventoryRegisterRecords");
            AddOfflineApprovalColumns(migrationBuilder, "YearlyArchiveInventoryRegisterRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropOfflineApprovalColumns(migrationBuilder, "HardDiskInventoryRegisterRecords");
            DropOfflineApprovalColumns(migrationBuilder, "YearlyArchiveInventoryRegisterRecords");
        }

        private static void AddOfflineApprovalColumns(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalOpinion",
                table: table,
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: table,
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedTime",
                table: table,
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveDeputyPresident",
                table: table,
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiveDeputyPresidentDate",
                table: table,
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveRoomHead",
                table: table,
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiveRoomHeadDate",
                table: table,
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedBy",
                table: table,
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedTime",
                table: table,
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeptHead",
                table: table,
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeptHeadDate",
                table: table,
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstPrintedAt",
                table: table,
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPrintedAt",
                table: table,
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrintCount",
                table: table,
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProductionHead",
                table: table,
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductionHeadDate",
                table: table,
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductionVicePresident",
                table: table,
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductionVicePresidentDate",
                table: table,
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SignedAttachmentUploaded",
                table: table,
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedAttachmentUploadedTime",
                table: table,
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedAttachmentUploader",
                table: table,
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: table,
                type: "TEXT",
                nullable: true);
        }

        private static void DropOfflineApprovalColumns(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.DropColumn(name: "ApprovalOpinion", table: table);
            migrationBuilder.DropColumn(name: "ApprovedBy", table: table);
            migrationBuilder.DropColumn(name: "ApprovedTime", table: table);
            migrationBuilder.DropColumn(name: "ArchiveDeputyPresident", table: table);
            migrationBuilder.DropColumn(name: "ArchiveDeputyPresidentDate", table: table);
            migrationBuilder.DropColumn(name: "ArchiveRoomHead", table: table);
            migrationBuilder.DropColumn(name: "ArchiveRoomHeadDate", table: table);
            migrationBuilder.DropColumn(name: "ConfirmedBy", table: table);
            migrationBuilder.DropColumn(name: "ConfirmedTime", table: table);
            migrationBuilder.DropColumn(name: "DeptHead", table: table);
            migrationBuilder.DropColumn(name: "DeptHeadDate", table: table);
            migrationBuilder.DropColumn(name: "FirstPrintedAt", table: table);
            migrationBuilder.DropColumn(name: "LastPrintedAt", table: table);
            migrationBuilder.DropColumn(name: "PrintCount", table: table);
            migrationBuilder.DropColumn(name: "ProductionHead", table: table);
            migrationBuilder.DropColumn(name: "ProductionHeadDate", table: table);
            migrationBuilder.DropColumn(name: "ProductionVicePresident", table: table);
            migrationBuilder.DropColumn(name: "ProductionVicePresidentDate", table: table);
            migrationBuilder.DropColumn(name: "SignedAttachmentUploaded", table: table);
            migrationBuilder.DropColumn(name: "SignedAttachmentUploadedTime", table: table);
            migrationBuilder.DropColumn(name: "SignedAttachmentUploader", table: table);
            migrationBuilder.DropColumn(name: "SubmittedAt", table: table);
        }
    }
}
