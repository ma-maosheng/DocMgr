using System;
using DocMgr.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// 归还单审批/交接字段。若库中已应用本迁移，后续灭失签字列由
    /// <see cref="AddArchiveReturnApprovalAndLossSigners"/> 补齐。
    /// </remarks>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260721045002_AddArchiveReturnApprovalHandoverFields")]
    public partial class AddArchiveReturnApprovalHandoverFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalOpinion",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HandoverAdmin",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HandoverApplicant",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "HandoverDate",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewerDate",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerName",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "SignedAttachmentUploaded",
                table: "YearlyArchiveReturnRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedAttachmentUploadedTime",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedAttachmentUploader",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalOpinion",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "HandoverAdmin",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "HandoverApplicant",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "HandoverDate",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "ReviewerDate",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "ReviewerName",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "SignedAttachmentUploaded",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "SignedAttachmentUploadedTime",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "SignedAttachmentUploader",
                table: "YearlyArchiveReturnRecords");
        }
    }
}
