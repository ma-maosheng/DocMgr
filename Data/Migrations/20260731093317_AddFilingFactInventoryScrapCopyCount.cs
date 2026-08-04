using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFilingFactInventoryScrapCopyCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InventoryScrapCopyCount",
                table: "YearlyArchiveFilingFacts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InventoryScrapCopyCount",
                table: "YearlyArchiveFilingFacts");
        }
    }
}
