using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHardDiskMediaApplicationArchiveDeputyPresident : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchiveDeputyPresident",
                table: "HardDiskMediaApplications",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiveDeputyPresidentDate",
                table: "HardDiskMediaApplications",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresident",
                table: "HardDiskMediaApplications");

            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresidentDate",
                table: "HardDiskMediaApplications");
        }
    }
}
