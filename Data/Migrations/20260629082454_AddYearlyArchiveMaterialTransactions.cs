using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddYearlyArchiveMaterialTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YearlyArchiveMaterialTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FilingFactId = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    BusinessNo = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    DedupKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    BeforeLifecycleStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AfterLifecycleStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    BeforeContainerCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AfterContainerCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BeforeStorageLocation = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AfterStorageLocation = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Remark = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    OperatorName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OperatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveMaterialTransactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveMaterialTransactions_DedupKey",
                table: "YearlyArchiveMaterialTransactions",
                column: "DedupKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveMaterialTransactions_FilingFactId_OperatedAt",
                table: "YearlyArchiveMaterialTransactions",
                columns: new[] { "FilingFactId", "OperatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YearlyArchiveMaterialTransactions");
        }
    }
}
