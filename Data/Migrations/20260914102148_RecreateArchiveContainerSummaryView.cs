using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecreateArchiveContainerSummaryView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 重建视图：上一迁移（DropYearlyArchiveBoxStructuralColumns）删列时因 SQLite
            // 表重建与视图解析冲突，已先 DROP VIEW，此处负责将其建回。
            // 列清单与 InitialCreate 中的定义保持一致。
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ArchiveContainerSummaries;");
            migrationBuilder.Sql(
                @"CREATE VIEW vw_ArchiveContainerSummaries AS
SELECT
    0 AS Kind,
    ArchiveSequenceNo AS ContainerCode,
    ProjectName,
    Year,
    ArchivedBy,
    ArchivedDate,
    Remarks
FROM YearlyArchiveBoxes
UNION ALL
SELECT
    1 AS Kind,
    ElectronicArchiveNo AS ContainerCode,
    ProjectName,
    Year,
    ArchivedBy,
    ArchivedDate,
    Remarks
FROM YearlyElectronicArchiveUnits;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"CREATE VIEW vw_ArchiveContainerSummaries AS
SELECT
    0 AS Kind,
    ArchiveSequenceNo AS ContainerCode,
    ProjectName,
    Year,
    ArchivedBy,
    ArchivedDate,
    Remarks
FROM YearlyArchiveBoxes
UNION ALL
SELECT
    1 AS Kind,
    ElectronicArchiveNo AS ContainerCode,
    ProjectName,
    Year,
    ArchivedBy,
    ArchivedDate,
    Remarks
FROM YearlyElectronicArchiveUnits;");
        }
    }
}
