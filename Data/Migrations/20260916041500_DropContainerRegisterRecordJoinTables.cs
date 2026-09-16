using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropContainerRegisterRecordJoinTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 盒/电子袋 ↔ 登记申请的关联改为由子项级链接现算投影（ArchiveContainerRegisterRecordProjectionSupport），
            // 两张 EF 隐式多对多中间表退役。
            migrationBuilder.DropTable(
                name: "YearlyArchiveBoxYearlyArchiveRegisterRecord");

            migrationBuilder.DropTable(
                name: "YearlyArchiveRegisterRecordYearlyElectronicArchiveUnit");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YearlyArchiveBoxYearlyArchiveRegisterRecord",
                columns: table => new
                {
                    ArchiveBoxesId = table.Column<int>(type: "INTEGER", nullable: false),
                    RegisterRecordsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveBoxYearlyArchiveRegisterRecord", x => new { x.ArchiveBoxesId, x.RegisterRecordsId });
                    table.ForeignKey(
                        name: "FK_YearlyArchiveBoxYearlyArchiveRegisterRecord_YearlyArchiveBoxes_ArchiveBoxesId",
                        column: x => x.ArchiveBoxesId,
                        principalTable: "YearlyArchiveBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveBoxYearlyArchiveRegisterRecord_YearlyArchiveRegisterRecords_RegisterRecordsId",
                        column: x => x.RegisterRecordsId,
                        principalTable: "YearlyArchiveRegisterRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveRegisterRecordYearlyElectronicArchiveUnit",
                columns: table => new
                {
                    ElectronicArchiveUnitsId = table.Column<int>(type: "INTEGER", nullable: false),
                    RegisterRecordsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveRegisterRecordYearlyElectronicArchiveUnit", x => new { x.ElectronicArchiveUnitsId, x.RegisterRecordsId });
                    table.ForeignKey(
                        name: "FK_YearlyArchiveRegisterRecordYearlyElectronicArchiveUnit_YearlyArchiveRegisterRecords_RegisterRecordsId",
                        column: x => x.RegisterRecordsId,
                        principalTable: "YearlyArchiveRegisterRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveRegisterRecordYearlyElectronicArchiveUnit_YearlyElectronicArchiveUnits_ElectronicArchiveUnitsId",
                        column: x => x.ElectronicArchiveUnitsId,
                        principalTable: "YearlyElectronicArchiveUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveBoxYearlyArchiveRegisterRecord_RegisterRecordsId",
                table: "YearlyArchiveBoxYearlyArchiveRegisterRecord",
                column: "RegisterRecordsId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterRecordYearlyElectronicArchiveUnit_RegisterRecordsId",
                table: "YearlyArchiveRegisterRecordYearlyElectronicArchiveUnit",
                column: "RegisterRecordsId");
        }
    }
}
