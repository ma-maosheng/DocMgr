using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveSourceTypeProvideUnitToMediaItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProvideUnit",
                table: "YearlyArchiveRegisterRecords");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "YearlyArchiveRegisterRecords");

            migrationBuilder.AddColumn<string>(
                name: "ProvideUnit",
                table: "YearlyArchiveRegisterMediaItems",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "YearlyArchiveRegisterMediaItems",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProvideUnit",
                table: "YearlyArchiveRegisterMediaItems");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "YearlyArchiveRegisterMediaItems");

            migrationBuilder.AddColumn<string>(
                name: "ProvideUnit",
                table: "YearlyArchiveRegisterRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "YearlyArchiveRegisterRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
