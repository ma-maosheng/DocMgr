using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// 灭失归还审批：生产科负责人 / 生产副院长签字字段。
    /// 审批交接基础字段见 <see cref="AddArchiveReturnApprovalHandoverFields"/>。
    /// </remarks>
    public partial class AddArchiveReturnApprovalAndLossSigners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductionHead",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductionHeadDate",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VicePresident",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "VicePresidentDate",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductionHead",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "ProductionHeadDate",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "VicePresident",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "VicePresidentDate",
                table: "YearlyArchiveReturnRecords");
        }
    }
}
