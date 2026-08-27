using DocMgr.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260827080000_AddHistoryArchiveDisposal")]
    public class AddHistoryArchiveDisposal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LifecycleStatus",
                table: "TopoMaps",
                type: "TEXT",
                nullable: false,
                defaultValue: "在库");

            migrationBuilder.AddColumn<string>(
                name: "LastStorageLocation",
                table: "TopoMaps",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LifecycleStatus",
                table: "AerialPhotos",
                type: "TEXT",
                nullable: false,
                defaultValue: "在库");

            migrationBuilder.AddColumn<string>(
                name: "LastStorageLocation",
                table: "AerialPhotos",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LifecycleStatus",
                table: "OtherMaps",
                type: "TEXT",
                nullable: false,
                defaultValue: "在库");

            migrationBuilder.AddColumn<string>(
                name: "LastStorageLocation",
                table: "OtherMaps",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "HistoryArchiveDisposalRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DisposalNo = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialKind = table.Column<string>(type: "TEXT", nullable: false),
                    DispositionMethod = table.Column<string>(type: "TEXT", nullable: false),
                    TransferTarget = table.Column<string>(type: "TEXT", nullable: false),
                    OtherRemark = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicantUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicantName = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicantDept = table.Column<string>(type: "TEXT", nullable: false),
                    ApplyTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ApprovedBy = table.Column<string>(type: "TEXT", nullable: false),
                    ApprovedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovalOpinion = table.Column<string>(type: "TEXT", nullable: false),
                    ArchiveRoomHead = table.Column<string>(type: "TEXT", nullable: false),
                    ArchiveRoomHeadDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ArchiveDeputyPresident = table.Column<string>(type: "TEXT", nullable: false),
                    ArchiveDeputyPresidentDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "TEXT", nullable: false),
                    ConfirmedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignedAttachmentUploaded = table.Column<bool>(type: "INTEGER", nullable: false),
                    SignedAttachmentUploadedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignedAttachmentUploader = table.Column<string>(type: "TEXT", nullable: false),
                    ScenePhotoUploaded = table.Column<bool>(type: "INTEGER", nullable: false),
                    PhysicalRemovalConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PhysicalRemovalConfirmedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PhysicalRemovalConfirmedBy = table.Column<string>(type: "TEXT", nullable: false),
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
                    table.PrimaryKey("PK_HistoryArchiveDisposalRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoryArchiveDisposalItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DisposalRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    BoxCode = table.Column<string>(type: "TEXT", nullable: false),
                    BoxSpecification = table.Column<string>(type: "TEXT", nullable: false),
                    CabinetName = table.Column<string>(type: "TEXT", nullable: false),
                    FaceCode = table.Column<string>(type: "TEXT", nullable: false),
                    SlotCode = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeStorageLocation = table.Column<string>(type: "TEXT", nullable: false),
                    ContentSummary = table.Column<string>(type: "TEXT", nullable: false),
                    LedgerRecordCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceRecordKeys = table.Column<string>(type: "TEXT", nullable: false),
                    IsMixedPlacement = table.Column<bool>(type: "INTEGER", nullable: false),
                    RelatedBoxCodes = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryArchiveDisposalItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoryArchiveDisposalItems_HistoryArchiveDisposalRecords_DisposalRecordId",
                        column: x => x.DisposalRecordId,
                        principalTable: "HistoryArchiveDisposalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveDisposalRecords_DisposalNo",
                table: "HistoryArchiveDisposalRecords",
                column: "DisposalNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveDisposalRecords_Status",
                table: "HistoryArchiveDisposalRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveDisposalRecords_ApplyTime",
                table: "HistoryArchiveDisposalRecords",
                column: "ApplyTime");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveDisposalRecords_MaterialKind",
                table: "HistoryArchiveDisposalRecords",
                column: "MaterialKind");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveDisposalItems_DisposalRecordId",
                table: "HistoryArchiveDisposalItems",
                column: "DisposalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveDisposalItems_BoxCode",
                table: "HistoryArchiveDisposalItems",
                column: "BoxCode");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryArchiveDisposalItems_DisposalRecordId_BoxCode",
                table: "HistoryArchiveDisposalItems",
                columns: new[] { "DisposalRecordId", "BoxCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "HistoryArchiveDisposalItems");
            migrationBuilder.DropTable(name: "HistoryArchiveDisposalRecords");

            migrationBuilder.DropColumn(name: "LifecycleStatus", table: "TopoMaps");
            migrationBuilder.DropColumn(name: "LastStorageLocation", table: "TopoMaps");
            migrationBuilder.DropColumn(name: "LifecycleStatus", table: "AerialPhotos");
            migrationBuilder.DropColumn(name: "LastStorageLocation", table: "AerialPhotos");
            migrationBuilder.DropColumn(name: "LifecycleStatus", table: "OtherMaps");
            migrationBuilder.DropColumn(name: "LastStorageLocation", table: "OtherMaps");
        }
    }
}
