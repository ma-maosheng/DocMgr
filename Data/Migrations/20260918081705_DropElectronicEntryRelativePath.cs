using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropElectronicEntryRelativePath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentEntryRelativePath",
                table: "YearlyArchiveSearchResultSetItems");

            migrationBuilder.DropColumn(
                name: "RelativePath",
                table: "YearlyArchiveRegisterElectronicMediaItemEntries");

            migrationBuilder.DropColumn(
                name: "ContentEntryRelativePath",
                table: "YearlyArchiveOutboundItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentEntryRelativePath",
                table: "YearlyArchiveSearchResultSetItems",
                type: "TEXT",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RelativePath",
                table: "YearlyArchiveRegisterElectronicMediaItemEntries",
                type: "TEXT",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContentEntryRelativePath",
                table: "YearlyArchiveOutboundItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
