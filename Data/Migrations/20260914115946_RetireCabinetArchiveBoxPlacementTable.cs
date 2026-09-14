using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class RetireCabinetArchiveBoxPlacementTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 摆放表退役前的数据搬迁：
            // 1) 历史盒放置方式以摆放表为准回填（保留手工设置的盒面向外）；
            // 2) 年度盒放置方式归一化遗留中文域值（竖放/横放 → SpineOut/FrontOut）。
            migrationBuilder.Sql(@"
UPDATE HistoryArchiveBoxes
SET PlacementMode = (
    SELECT CASE WHEN p.PlacementMode = 'FrontOut' THEN 'FrontOut' ELSE 'SpineOut' END
    FROM CabinetArchiveBoxPlacements p
    WHERE p.BoxCode = HistoryArchiveBoxes.BoxCode)
WHERE EXISTS (
    SELECT 1 FROM CabinetArchiveBoxPlacements p
    WHERE p.BoxCode = HistoryArchiveBoxes.BoxCode);

UPDATE YearlyArchiveBoxes
SET PlacementMode = CASE WHEN PlacementMode IN ('FrontOut', '横放') THEN 'FrontOut' ELSE 'SpineOut' END;
");

            migrationBuilder.DropTable(
                name: "CabinetArchiveBoxPlacements");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CabinetArchiveBoxPlacements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoxCode = table.Column<string>(type: "TEXT", nullable: false),
                    BoxSpecification = table.Column<string>(type: "TEXT", nullable: false),
                    CabinetName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    FaceCode = table.Column<string>(type: "TEXT", nullable: false),
                    PlacementMode = table.Column<string>(type: "TEXT", nullable: false),
                    SlotCode = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRecordKey = table.Column<string>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CabinetArchiveBoxPlacements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CabinetArchiveBoxPlacements_BoxCode",
                table: "CabinetArchiveBoxPlacements",
                column: "BoxCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CabinetArchiveBoxPlacements_CabinetName_FaceCode_SlotCode",
                table: "CabinetArchiveBoxPlacements",
                columns: new[] { "CabinetName", "FaceCode", "SlotCode" });

            migrationBuilder.CreateIndex(
                name: "IX_CabinetArchiveBoxPlacements_SourceType_SourceRecordKey",
                table: "CabinetArchiveBoxPlacements",
                columns: new[] { "SourceType", "SourceRecordKey" });
        }
    }
}
