using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnifyApplicationWorkflowStatusSevenState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ForceVoidReason",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ForceVoidedAt",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedUploadedAt",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WithdrawnAt",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "ForceVoidReason",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "ForceVoidedAt",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "SignedUploadedAt",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "WithdrawnAt",
                table: "YearlyArchiveReturnRecords");
        }
    }
}
