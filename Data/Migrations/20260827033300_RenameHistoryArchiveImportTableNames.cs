using DocMgr.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260827033300_RenameHistoryArchiveImportTableNames")]
    public class RenameHistoryArchiveImportTableNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "TopoMaps",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_TopoMaps_Category",
                table: "TopoMaps",
                column: "Category");

            migrationBuilder.Sql(
                """
                UPDATE "TopoMaps"
                SET "Category" = '地形图' || COALESCE("Scale", '')
                WHERE trim(COALESCE("Category", '')) = '';

                UPDATE "AerialPhotos"
                SET "Category" = replace("Category", '历史存档航摄影像', '像片')
                WHERE "Category" LIKE '历史存档航摄影像%';

                UPDATE "OtherMaps"
                SET "Category" = '其他资料' || "Category"
                WHERE trim(COALESCE("Category", '')) <> ''
                  AND "Category" NOT LIKE '其他资料%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "AerialPhotos"
                SET "Category" = replace("Category", '像片', '历史存档航摄影像')
                WHERE "Category" LIKE '像片%';

                UPDATE "OtherMaps"
                SET "Category" = substr("Category", length('其他资料') + 1)
                WHERE "Category" LIKE '其他资料%';
                """);

            migrationBuilder.DropIndex(
                name: "IX_TopoMaps_Category",
                table: "TopoMaps");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "TopoMaps");
        }
    }
}
