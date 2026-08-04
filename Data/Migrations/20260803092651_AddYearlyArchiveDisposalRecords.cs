using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddYearlyArchiveDisposalRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YearlyArchiveDisposalRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DisposalNo = table.Column<string>(type: "TEXT", nullable: false),
                    MediaKind = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DisposalReason = table.Column<string>(type: "TEXT", nullable: false),
                    DispositionMethod = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicantUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicantName = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicantDept = table.Column<string>(type: "TEXT", nullable: false),
                    ApplyTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ApprovedBy = table.Column<string>(type: "TEXT", nullable: false),
                    ApprovedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovalOpinion = table.Column<string>(type: "TEXT", nullable: false),
                    ConfirmedBy = table.Column<string>(type: "TEXT", nullable: false),
                    ConfirmedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignedAttachmentUploaded = table.Column<bool>(type: "INTEGER", nullable: false),
                    SignedAttachmentUploadedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignedAttachmentUploader = table.Column<string>(type: "TEXT", nullable: false),
                    ScenePhotoUploaded = table.Column<bool>(type: "INTEGER", nullable: false),
                    PhysicalRemovalConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PhysicalRemovalConfirmedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PhysicalRemovalConfirmedBy = table.Column<string>(type: "TEXT", nullable: false),
                    FormatRetainedConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    FormatRetainedConfirmedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FormatRetainedConfirmedBy = table.Column<string>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedBy = table.Column<string>(type: "TEXT", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WithdrawReason = table.Column<string>(type: "TEXT", nullable: false),
                    PrintCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveDisposalRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YearlyArchiveDisposalItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DisposalRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    FilingFactId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContainerCode = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeStorageLocation = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRegisterKind = table.Column<string>(type: "TEXT", nullable: false),
                    DisposalReason = table.Column<string>(type: "TEXT", nullable: false),
                    DispositionMethod = table.Column<string>(type: "TEXT", nullable: false),
                    MaterialName = table.Column<string>(type: "TEXT", nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", nullable: false),
                    FormNo = table.Column<string>(type: "TEXT", nullable: false),
                    InventoryLostCopyCount = table.Column<int>(type: "INTEGER", nullable: false),
                    InventoryScrapCopyCount = table.Column<int>(type: "INTEGER", nullable: false),
                    BeforeLifecycleStatus = table.Column<string>(type: "TEXT", nullable: false),
                    MediumKind = table.Column<string>(type: "TEXT", nullable: false),
                    MediumId = table.Column<int>(type: "INTEGER", nullable: false),
                    MediumCode = table.Column<string>(type: "TEXT", nullable: false),
                    ElectronicArchiveUnitId = table.Column<int>(type: "INTEGER", nullable: false),
                    ElectronicArchiveNo = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeMediaStatus = table.Column<string>(type: "TEXT", nullable: false),
                    TargetBlankSlotLocation = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveDisposalItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveDisposalItems_YearlyArchiveDisposalRecords_DisposalRecordId",
                        column: x => x.DisposalRecordId,
                        principalTable: "YearlyArchiveDisposalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalItems_ContainerId",
                table: "YearlyArchiveDisposalItems",
                column: "ContainerId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalItems_DisposalRecordId",
                table: "YearlyArchiveDisposalItems",
                column: "DisposalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalItems_DisposalRecordId_FilingFactId",
                table: "YearlyArchiveDisposalItems",
                columns: new[] { "DisposalRecordId", "FilingFactId" });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalItems_DisposalRecordId_MediumKind_MediumId",
                table: "YearlyArchiveDisposalItems",
                columns: new[] { "DisposalRecordId", "MediumKind", "MediumId" });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalItems_FilingFactId",
                table: "YearlyArchiveDisposalItems",
                column: "FilingFactId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalItems_MediumId",
                table: "YearlyArchiveDisposalItems",
                column: "MediumId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalRecords_ApplyTime",
                table: "YearlyArchiveDisposalRecords",
                column: "ApplyTime");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalRecords_DisposalNo",
                table: "YearlyArchiveDisposalRecords",
                column: "DisposalNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalRecords_MediaKind",
                table: "YearlyArchiveDisposalRecords",
                column: "MediaKind");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveDisposalRecords_Status",
                table: "YearlyArchiveDisposalRecords",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YearlyArchiveDisposalItems");

            migrationBuilder.DropTable(
                name: "YearlyArchiveDisposalRecords");
        }
    }
}
