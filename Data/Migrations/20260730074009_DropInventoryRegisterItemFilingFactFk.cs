using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropInventoryRegisterItemFilingFactFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_YearlyArchiveInventoryRegisterItems_YearlyArchiveFilingFacts_FilingFactId",
                table: "YearlyArchiveInventoryRegisterItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_YearlyArchiveInventoryRegisterItems_YearlyArchiveFilingFacts_FilingFactId",
                table: "YearlyArchiveInventoryRegisterItems",
                column: "FilingFactId",
                principalTable: "YearlyArchiveFilingFacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
