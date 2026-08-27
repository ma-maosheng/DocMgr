using DocMgr.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260827030000_DropOtherMapSheetCount")]
    public class DropOtherMapSheetCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite 的 DropColumn 依赖迁移 TargetModel 重建表。本迁移无 Designer 快照时会失败。
            // 按目标列清单重建，SheetCount 已删除的库也可重复执行。
            migrationBuilder.Sql(
                """
                PRAGMA foreign_keys = OFF;

                CREATE TABLE "OtherMaps_Rebuild" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_OtherMaps" PRIMARY KEY AUTOINCREMENT,
                    "Category" TEXT NOT NULL,
                    "SequenceNumber" TEXT NOT NULL,
                    "Scale" TEXT NOT NULL,
                    "BoxNumber" TEXT NOT NULL,
                    "BoxSpecification" TEXT NOT NULL,
                    "MapName" TEXT NOT NULL,
                    "Registrant" TEXT NOT NULL,
                    "RegistrationDate" TEXT NOT NULL,
                    "Modifier" TEXT NOT NULL,
                    "ModificationDate" TEXT NOT NULL,
                    "Remark" TEXT NOT NULL,
                    "MaterialCategory" TEXT NOT NULL,
                    "StartYear" TEXT NOT NULL,
                    "EndYear" TEXT NOT NULL
                );

                INSERT INTO "OtherMaps_Rebuild" (
                    "Id", "Category", "SequenceNumber", "Scale", "BoxNumber", "BoxSpecification",
                    "MapName", "Registrant", "RegistrationDate", "Modifier", "ModificationDate",
                    "Remark", "MaterialCategory", "StartYear", "EndYear")
                SELECT
                    "Id", "Category", "SequenceNumber", "Scale", "BoxNumber", "BoxSpecification",
                    "MapName", "Registrant", "RegistrationDate", "Modifier", "ModificationDate",
                    "Remark", "MaterialCategory", "StartYear", "EndYear"
                FROM "OtherMaps";

                DROP TABLE "OtherMaps";
                ALTER TABLE "OtherMaps_Rebuild" RENAME TO "OtherMaps";

                PRAGMA foreign_keys = ON;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SheetCount",
                table: "OtherMaps",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
