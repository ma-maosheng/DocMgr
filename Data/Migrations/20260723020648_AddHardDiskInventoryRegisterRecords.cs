using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHardDiskInventoryRegisterRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HardDiskInventoryRegisterRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegisterNo = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
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
                    table.PrimaryKey("PK_HardDiskInventoryRegisterRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HardDiskInventoryRegisterItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RegisterRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    MediumId = table.Column<int>(type: "INTEGER", nullable: false),
                    DiskCode = table.Column<string>(type: "TEXT", nullable: false),
                    SerialNumber = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeMediaStatus = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeStorageLocation = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeMediaNature = table.Column<string>(type: "TEXT", nullable: false),
                    TargetStorageLocation = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardDiskInventoryRegisterItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HardDiskInventoryRegisterItems_HardDiskInventoryRegisterRecords_RegisterRecordId",
                        column: x => x.RegisterRecordId,
                        principalTable: "HardDiskInventoryRegisterRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HardDiskInventoryRegisterItems_HardDiskMedia_MediumId",
                        column: x => x.MediumId,
                        principalTable: "HardDiskMedia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskInventoryRegisterItems_MediumId",
                table: "HardDiskInventoryRegisterItems",
                column: "MediumId");

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskInventoryRegisterItems_RegisterRecordId",
                table: "HardDiskInventoryRegisterItems",
                column: "RegisterRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskInventoryRegisterItems_RegisterRecordId_MediumId",
                table: "HardDiskInventoryRegisterItems",
                columns: new[] { "RegisterRecordId", "MediumId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskInventoryRegisterRecords_ApplyTime",
                table: "HardDiskInventoryRegisterRecords",
                column: "ApplyTime");

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskInventoryRegisterRecords_RegisterNo",
                table: "HardDiskInventoryRegisterRecords",
                column: "RegisterNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskInventoryRegisterRecords_Status",
                table: "HardDiskInventoryRegisterRecords",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HardDiskInventoryRegisterItems");

            migrationBuilder.DropTable(
                name: "HardDiskInventoryRegisterRecords");
        }
    }
}
