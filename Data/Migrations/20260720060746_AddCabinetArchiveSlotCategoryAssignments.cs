using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCabinetArchiveSlotCategoryAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CabinetArchiveSlotCategoryAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CabinetId = table.Column<int>(type: "INTEGER", nullable: false),
                    FaceCode = table.Column<string>(type: "TEXT", nullable: false),
                    SlotCode = table.Column<string>(type: "TEXT", nullable: false),
                    CategoryName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CabinetArchiveSlotCategoryAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CabinetArchiveSlotCategoryAssignments_Cabinets_CabinetId",
                        column: x => x.CabinetId,
                        principalTable: "Cabinets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CabinetArchiveSlotCategoryAssignments_CabinetId_CategoryName",
                table: "CabinetArchiveSlotCategoryAssignments",
                columns: new[] { "CabinetId", "CategoryName" });

            migrationBuilder.CreateIndex(
                name: "IX_CabinetArchiveSlotCategoryAssignments_CabinetId_FaceCode_SlotCode",
                table: "CabinetArchiveSlotCategoryAssignments",
                columns: new[] { "CabinetId", "FaceCode", "SlotCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CabinetArchiveSlotCategoryAssignments");
        }
    }
}
