using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryRegisterItemProjectSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProjectName",
                table: "YearlyArchiveInventoryRegisterItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Year",
                table: "YearlyArchiveInventoryRegisterItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // 存量模拟明细：从立档事实 / 项目信息回填登记前项目快照。
            migrationBuilder.Sql(
                """
                UPDATE YearlyArchiveInventoryRegisterItems
                SET ProjectName = IFNULL((
                        SELECT TRIM(IFNULL(f.ProjectName, ''))
                        FROM YearlyArchiveFilingFacts f
                        WHERE f.Id = YearlyArchiveInventoryRegisterItems.FilingFactId
                    ), ''),
                    Year = IFNULL((
                        SELECT TRIM(IFNULL(p.ImplementYear, ''))
                        FROM YearlyArchiveFilingFacts f
                        LEFT JOIN ProjectInfos p ON p.Id = f.ProjectId
                        WHERE f.Id = YearlyArchiveInventoryRegisterItems.FilingFactId
                    ), '')
                WHERE FilingFactId > 0;
                """);

            // 存量电子明细：从电子立档单元回填登记前项目快照。
            migrationBuilder.Sql(
                """
                UPDATE YearlyArchiveInventoryRegisterItems
                SET ProjectName = IFNULL((
                        SELECT TRIM(IFNULL(u.ProjectName, ''))
                        FROM YearlyElectronicArchiveUnits u
                        WHERE u.Id = YearlyArchiveInventoryRegisterItems.ElectronicArchiveUnitId
                    ), ''),
                    Year = IFNULL((
                        SELECT TRIM(IFNULL(u.Year, ''))
                        FROM YearlyElectronicArchiveUnits u
                        WHERE u.Id = YearlyArchiveInventoryRegisterItems.ElectronicArchiveUnitId
                    ), '')
                WHERE ElectronicArchiveUnitId > 0
                  AND IFNULL(FilingFactId, 0) = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectName",
                table: "YearlyArchiveInventoryRegisterItems");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "YearlyArchiveInventoryRegisterItems");
        }
    }
}
