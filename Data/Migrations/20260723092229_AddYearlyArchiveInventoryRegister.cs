using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddYearlyArchiveInventoryRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InventoryLostCopyCount",
                table: "YearlyArchiveFilingFacts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "YearlyArchiveInventoryRegisterRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegisterNo = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaKind = table.Column<string>(type: "TEXT", nullable: false),
                    RegisterKind = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicantUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicantName = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicantDept = table.Column<string>(type: "TEXT", nullable: false),
                    ApplyTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedBy = table.Column<string>(type: "TEXT", nullable: false),
                    WithdrawnAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WithdrawReason = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveInventoryRegisterRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveInventoryRegisterItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegisterRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    FilingFactId = table.Column<int>(type: "INTEGER", nullable: false),
                    MediaItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerCode = table.Column<string>(type: "TEXT", nullable: false),
                    LostCopyCount = table.Column<int>(type: "INTEGER", nullable: false),
                    BeforeAvailableCopyCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MediumKind = table.Column<string>(type: "TEXT", nullable: false),
                    MediumId = table.Column<int>(type: "INTEGER", nullable: false),
                    MediumCode = table.Column<string>(type: "TEXT", nullable: false),
                    ElectronicArchiveUnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    ElectronicArchiveNo = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeMediaStatus = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeStorageLocation = table.Column<string>(type: "TEXT", nullable: false),
                    MaterialName = table.Column<string>(type: "TEXT", nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveInventoryRegisterItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveInventoryRegisterItems_YearlyArchiveFilingFacts_FilingFactId",
                        column: x => x.FilingFactId,
                        principalTable: "YearlyArchiveFilingFacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveInventoryRegisterItems_YearlyArchiveInventoryRegisterRecords_RegisterRecordId",
                        column: x => x.RegisterRecordId,
                        principalTable: "YearlyArchiveInventoryRegisterRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterItems_FilingFactId",
                table: "YearlyArchiveInventoryRegisterItems",
                column: "FilingFactId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterItems_MediumId",
                table: "YearlyArchiveInventoryRegisterItems",
                column: "MediumId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterItems_RegisterRecordId",
                table: "YearlyArchiveInventoryRegisterItems",
                column: "RegisterRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterItems_RegisterRecordId_FilingFactId",
                table: "YearlyArchiveInventoryRegisterItems",
                columns: new[] { "RegisterRecordId", "FilingFactId" });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterItems_RegisterRecordId_MediumKind_MediumId",
                table: "YearlyArchiveInventoryRegisterItems",
                columns: new[] { "RegisterRecordId", "MediumKind", "MediumId" });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterRecords_ApplyTime",
                table: "YearlyArchiveInventoryRegisterRecords",
                column: "ApplyTime");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterRecords_MediaKind",
                table: "YearlyArchiveInventoryRegisterRecords",
                column: "MediaKind");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterRecords_RegisterNo",
                table: "YearlyArchiveInventoryRegisterRecords",
                column: "RegisterNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveInventoryRegisterRecords_Status",
                table: "YearlyArchiveInventoryRegisterRecords",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YearlyArchiveInventoryRegisterItems");

            migrationBuilder.DropTable(
                name: "YearlyArchiveInventoryRegisterRecords");

            migrationBuilder.DropColumn(
                name: "InventoryLostCopyCount",
                table: "YearlyArchiveFilingFacts");
        }
    }
}
