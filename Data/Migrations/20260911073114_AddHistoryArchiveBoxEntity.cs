using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoryArchiveBoxEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistoryArchiveBoxes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoxCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CabinetName = table.Column<string>(type: "TEXT", nullable: false),
                    Side = table.Column<string>(type: "TEXT", nullable: false),
                    Row = table.Column<int>(type: "INTEGER", nullable: false),
                    Column = table.Column<int>(type: "INTEGER", nullable: false),
                    BoxIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    BoxSpecification = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PlacementMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    LifecycleStatus = table.Column<string>(type: "TEXT", nullable: false),
                    ArchivedBy = table.Column<string>(type: "TEXT", nullable: false),
                    ArchivedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryArchiveBoxes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoryArchiveBoxLedgerLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HistoryArchiveBoxId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    RecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryArchiveBoxLedgerLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoryArchiveBoxLedgerLinks_HistoryArchiveBoxes_HistoryArchiveBoxId",
                        column: x => x.HistoryArchiveBoxId,
                        principalTable: "HistoryArchiveBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveBoxes_BoxCode",
                table: "HistoryArchiveBoxes",
                column: "BoxCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveBoxes_CabinetName_Side_Row_Column",
                table: "HistoryArchiveBoxes",
                columns: new[] { "CabinetName", "Side", "Row", "Column" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveBoxLedgerLinks_HistoryArchiveBoxId_MaterialKind_RecordId",
                table: "HistoryArchiveBoxLedgerLinks",
                columns: new[] { "HistoryArchiveBoxId", "MaterialKind", "RecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveBoxLedgerLinks_MaterialKind_RecordId",
                table: "HistoryArchiveBoxLedgerLinks",
                columns: new[] { "MaterialKind", "RecordId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoryArchiveBoxLedgerLinks");

            migrationBuilder.DropTable(
                name: "HistoryArchiveBoxes");
        }
    }
}
