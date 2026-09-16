using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropYearlyElectronicArchiveUnitMediaLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 电子袋 ↔ 登记介质条目的整体关联改为由子项级 YearlyElectronicArchiveUnitMediaItemLinks 现算投影
            // （ArchiveContainerRegisterRecordProjectionSupport.IsMediaEntryArchived），本表退役。
            migrationBuilder.DropTable(
                name: "YearlyElectronicArchiveUnitMediaLinks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YearlyElectronicArchiveUnitMediaLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    YearlyArchiveRegisterMediaId = table.Column<int>(type: "INTEGER", nullable: false),
                    YearlyElectronicArchiveUnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyElectronicArchiveUnitMediaLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyElectronicArchiveUnitMediaLinks_YearlyArchiveRegisterMedias_YearlyArchiveRegisterMediaId",
                        column: x => x.YearlyArchiveRegisterMediaId,
                        principalTable: "YearlyArchiveRegisterMedias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YearlyElectronicArchiveUnitMediaLinks_YearlyElectronicArchiveUnits_YearlyElectronicArchiveUnitId",
                        column: x => x.YearlyElectronicArchiveUnitId,
                        principalTable: "YearlyElectronicArchiveUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyElectronicArchiveUnitMediaLinks_YearlyArchiveRegisterMediaId",
                table: "YearlyElectronicArchiveUnitMediaLinks",
                column: "YearlyArchiveRegisterMediaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyElectronicArchiveUnitMediaLinks_YearlyElectronicArchiveUnitId_YearlyArchiveRegisterMediaId",
                table: "YearlyElectronicArchiveUnitMediaLinks",
                columns: new[] { "YearlyElectronicArchiveUnitId", "YearlyArchiveRegisterMediaId" },
                unique: true);
        }
    }
}
