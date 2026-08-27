using DocMgr.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260827021800_AddOtherMapMaterialCategoryAndYears")]
    public class AddOtherMapMaterialCategoryAndYears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaterialCategory",
                table: "OtherMaps",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StartYear",
                table: "OtherMaps",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EndYear",
                table: "OtherMaps",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaterialCategory",
                table: "OtherMaps");

            migrationBuilder.DropColumn(
                name: "StartYear",
                table: "OtherMaps");

            migrationBuilder.DropColumn(
                name: "EndYear",
                table: "OtherMaps");
        }
    }
}
