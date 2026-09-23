using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropMediaItemItemType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ItemType",
                table: "YearlyArchiveRegisterMediaItems");

            migrationBuilder.DropColumn(
                name: "ItemType",
                table: "YearlyArchiveFilingFacts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ItemType",
                table: "YearlyArchiveRegisterMediaItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ItemType",
                table: "YearlyArchiveFilingFacts",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "");
        }
    }
}
