using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNetworkTransferTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NetworkInboundRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InboundNo = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceKind = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectName = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: false),
                    SourceResultSetId = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceResultSetNo = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicantUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicantName = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicantDept = table.Column<string>(type: "TEXT", nullable: false),
                    ApplyTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProdLeader = table.Column<string>(type: "TEXT", nullable: false),
                    ProdDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RndLeader = table.Column<string>(type: "TEXT", nullable: false),
                    RndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeputyLeader = table.Column<string>(type: "TEXT", nullable: false),
                    DeputyDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Deliverer = table.Column<string>(type: "TEXT", nullable: false),
                    DeliverDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Administrator = table.Column<string>(type: "TEXT", nullable: false),
                    AdminDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeptLeader = table.Column<string>(type: "TEXT", nullable: false),
                    DeptDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignedAttachmentUploaded = table.Column<bool>(type: "INTEGER", nullable: false),
                    SignedAttachmentUploadedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignedAttachmentUploader = table.Column<string>(type: "TEXT", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HandoverConfirmedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedBy = table.Column<string>(type: "TEXT", nullable: false),
                    WithdrawnAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WithdrawReason = table.Column<string>(type: "TEXT", nullable: false),
                    PrintCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkInboundRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NetworkOnNetAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AssetNo = table.Column<string>(type: "TEXT", nullable: false),
                    AssetKind = table.Column<string>(type: "TEXT", nullable: false),
                    AssetName = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectName = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<string>(type: "TEXT", nullable: false),
                    ServerPath = table.Column<string>(type: "TEXT", nullable: false),
                    ConfidentialLevel = table.Column<string>(type: "TEXT", nullable: false),
                    DataSizeText = table.Column<string>(type: "TEXT", nullable: false),
                    VersionText = table.Column<string>(type: "TEXT", nullable: false),
                    OriginKind = table.Column<string>(type: "TEXT", nullable: false),
                    OriginInboundItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    ParentAssetId = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceFilingFactId = table.Column<int>(type: "INTEGER", nullable: true),
                    LifecycleStatus = table.Column<string>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: false),
                    RegisteredBy = table.Column<string>(type: "TEXT", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkOnNetAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NetworkOnNetDisposalRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DisposalNo = table.Column<string>(type: "TEXT", nullable: false),
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
                    table.PrimaryKey("PK_NetworkOnNetDisposalRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NetworkOutboundRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OutboundNo = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DestinationKind = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectName = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: false),
                    TargetRegisterRecordId = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetRegisterFormNo = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicantUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ApplicantName = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicantDept = table.Column<string>(type: "TEXT", nullable: false),
                    ApplyTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProdLeader = table.Column<string>(type: "TEXT", nullable: false),
                    ProdDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RndLeader = table.Column<string>(type: "TEXT", nullable: false),
                    RndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeputyLeader = table.Column<string>(type: "TEXT", nullable: false),
                    DeputyDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Deliverer = table.Column<string>(type: "TEXT", nullable: false),
                    DeliverDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Administrator = table.Column<string>(type: "TEXT", nullable: false),
                    AdminDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeptLeader = table.Column<string>(type: "TEXT", nullable: false),
                    DeptDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignedAttachmentUploaded = table.Column<bool>(type: "INTEGER", nullable: false),
                    SignedAttachmentUploadedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignedAttachmentUploader = table.Column<string>(type: "TEXT", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HandoverConfirmedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedBy = table.Column<string>(type: "TEXT", nullable: false),
                    WithdrawnAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WithdrawReason = table.Column<string>(type: "TEXT", nullable: false),
                    PrintCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastPrintedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkOutboundRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NetworkInboundItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InboundRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    AssetKind = table.Column<string>(type: "TEXT", nullable: false),
                    AssetName = table.Column<string>(type: "TEXT", nullable: false),
                    ConfidentialLevel = table.Column<string>(type: "TEXT", nullable: false),
                    DataSizeText = table.Column<string>(type: "TEXT", nullable: false),
                    TargetServerPath = table.Column<string>(type: "TEXT", nullable: false),
                    SourceKind = table.Column<string>(type: "TEXT", nullable: false),
                    SourceResultSetItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceFilingFactId = table.Column<int>(type: "INTEGER", nullable: true),
                    FormNo = table.Column<string>(type: "TEXT", nullable: false),
                    MaterialName = table.Column<string>(type: "TEXT", nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", nullable: false),
                    ContainerCode = table.Column<string>(type: "TEXT", nullable: false),
                    StorageLocation = table.Column<string>(type: "TEXT", nullable: false),
                    OnNetAssetId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkInboundItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkInboundItems_NetworkInboundRecords_InboundRecordId",
                        column: x => x.InboundRecordId,
                        principalTable: "NetworkInboundRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkOnNetDisposalItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DisposalRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    OnNetAssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssetNo = table.Column<string>(type: "TEXT", nullable: false),
                    AssetKind = table.Column<string>(type: "TEXT", nullable: false),
                    AssetName = table.Column<string>(type: "TEXT", nullable: false),
                    ServerPath = table.Column<string>(type: "TEXT", nullable: false),
                    BeforeLifecycleStatus = table.Column<string>(type: "TEXT", nullable: false),
                    DisposalReason = table.Column<string>(type: "TEXT", nullable: false),
                    DispositionMethod = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkOnNetDisposalItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkOnNetDisposalItems_NetworkOnNetAssets_OnNetAssetId",
                        column: x => x.OnNetAssetId,
                        principalTable: "NetworkOnNetAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NetworkOnNetDisposalItems_NetworkOnNetDisposalRecords_DisposalRecordId",
                        column: x => x.DisposalRecordId,
                        principalTable: "NetworkOnNetDisposalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkOutboundItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OutboundRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    OnNetAssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssetNo = table.Column<string>(type: "TEXT", nullable: false),
                    AssetKind = table.Column<string>(type: "TEXT", nullable: false),
                    AssetName = table.Column<string>(type: "TEXT", nullable: false),
                    ServerPath = table.Column<string>(type: "TEXT", nullable: false),
                    ConfidentialLevel = table.Column<string>(type: "TEXT", nullable: false),
                    DataSizeText = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectName = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkOutboundItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkOutboundItems_NetworkOnNetAssets_OnNetAssetId",
                        column: x => x.OnNetAssetId,
                        principalTable: "NetworkOnNetAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NetworkOutboundItems_NetworkOutboundRecords_OutboundRecordId",
                        column: x => x.OutboundRecordId,
                        principalTable: "NetworkOutboundRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundItems_InboundRecordId",
                table: "NetworkInboundItems",
                column: "InboundRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundItems_OnNetAssetId",
                table: "NetworkInboundItems",
                column: "OnNetAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundItems_SourceFilingFactId",
                table: "NetworkInboundItems",
                column: "SourceFilingFactId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundRecords_ApplyTime",
                table: "NetworkInboundRecords",
                column: "ApplyTime");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundRecords_InboundNo",
                table: "NetworkInboundRecords",
                column: "InboundNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundRecords_SourceResultSetId",
                table: "NetworkInboundRecords",
                column: "SourceResultSetId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundRecords_Status",
                table: "NetworkInboundRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetAssets_AssetNo",
                table: "NetworkOnNetAssets",
                column: "AssetNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetAssets_LifecycleStatus",
                table: "NetworkOnNetAssets",
                column: "LifecycleStatus");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetAssets_OriginInboundItemId",
                table: "NetworkOnNetAssets",
                column: "OriginInboundItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetAssets_OriginKind",
                table: "NetworkOnNetAssets",
                column: "OriginKind");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetAssets_ParentAssetId",
                table: "NetworkOnNetAssets",
                column: "ParentAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetDisposalItems_DisposalRecordId",
                table: "NetworkOnNetDisposalItems",
                column: "DisposalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetDisposalItems_DisposalRecordId_OnNetAssetId",
                table: "NetworkOnNetDisposalItems",
                columns: new[] { "DisposalRecordId", "OnNetAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetDisposalItems_OnNetAssetId",
                table: "NetworkOnNetDisposalItems",
                column: "OnNetAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetDisposalRecords_ApplyTime",
                table: "NetworkOnNetDisposalRecords",
                column: "ApplyTime");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetDisposalRecords_DisposalNo",
                table: "NetworkOnNetDisposalRecords",
                column: "DisposalNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetDisposalRecords_Status",
                table: "NetworkOnNetDisposalRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOutboundItems_OnNetAssetId",
                table: "NetworkOutboundItems",
                column: "OnNetAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOutboundItems_OutboundRecordId",
                table: "NetworkOutboundItems",
                column: "OutboundRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOutboundItems_OutboundRecordId_OnNetAssetId",
                table: "NetworkOutboundItems",
                columns: new[] { "OutboundRecordId", "OnNetAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOutboundRecords_ApplyTime",
                table: "NetworkOutboundRecords",
                column: "ApplyTime");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOutboundRecords_OutboundNo",
                table: "NetworkOutboundRecords",
                column: "OutboundNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOutboundRecords_Status",
                table: "NetworkOutboundRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOutboundRecords_TargetRegisterRecordId",
                table: "NetworkOutboundRecords",
                column: "TargetRegisterRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NetworkInboundItems");

            migrationBuilder.DropTable(
                name: "NetworkOnNetDisposalItems");

            migrationBuilder.DropTable(
                name: "NetworkOutboundItems");

            migrationBuilder.DropTable(
                name: "NetworkInboundRecords");

            migrationBuilder.DropTable(
                name: "NetworkOnNetDisposalRecords");

            migrationBuilder.DropTable(
                name: "NetworkOnNetAssets");

            migrationBuilder.DropTable(
                name: "NetworkOutboundRecords");
        }
    }
}
