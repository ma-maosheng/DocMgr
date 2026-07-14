using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnLossCopyCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LossDescription",
                table: "YearlyArchiveReturnRecords",
                type: "TEXT",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "IntactReturnCopyCount",
                table: "YearlyArchiveReturnItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LossCopyCount",
                table: "YearlyArchiveReturnItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE YearlyArchiveReturnItems
                SET IntactReturnCopyCount = ReturnCopyCount,
                    LossCopyCount = 0
                WHERE ItemCondition IN ('Complete', 'Intact')
                   OR ItemCondition IS NULL
                   OR TRIM(ItemCondition) = '';

                UPDATE YearlyArchiveReturnItems
                SET IntactReturnCopyCount = 0,
                    LossCopyCount = ReturnCopyCount
                WHERE ItemCondition NOT IN ('Complete', 'Intact')
                  AND ItemCondition IS NOT NULL
                  AND TRIM(ItemCondition) <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LossDescription",
                table: "YearlyArchiveReturnRecords");

            migrationBuilder.DropColumn(
                name: "IntactReturnCopyCount",
                table: "YearlyArchiveReturnItems");

            migrationBuilder.DropColumn(
                name: "LossCopyCount",
                table: "YearlyArchiveReturnItems");
        }
    }
}
