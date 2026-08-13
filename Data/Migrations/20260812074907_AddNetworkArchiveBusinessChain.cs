using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNetworkArchiveBusinessChain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NetworkOnNetAssets_OriginInboundItemId",
                table: "NetworkOnNetAssets");

            migrationBuilder.AddColumn<int>(
                name: "BusinessChainId",
                table: "YearlyArchiveRegisterRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceNetworkOutboundNo",
                table: "YearlyArchiveRegisterRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SourceNetworkOutboundRecordId",
                table: "YearlyArchiveRegisterRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BusinessChainId",
                table: "NetworkOutboundRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BusinessChainId",
                table: "NetworkInboundRecords",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NetworkArchiveBusinessChains",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChainNo = table.Column<string>(type: "TEXT", nullable: false),
                    ScenarioKind = table.Column<string>(type: "TEXT", nullable: false),
                    PrimaryBusinessType = table.Column<string>(type: "TEXT", nullable: false),
                    PrimaryBusinessId = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusSummary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkArchiveBusinessChains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NetworkArchiveBusinessTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BusinessChainId = table.Column<int>(type: "INTEGER", nullable: false),
                    TaskKind = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    BusinessType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    BusinessId = table.Column<int>(type: "INTEGER", nullable: true),
                    BusinessNo = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    DedupKey = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    ResultMessage = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkArchiveBusinessTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkArchiveBusinessTasks_NetworkArchiveBusinessChains_BusinessChainId",
                        column: x => x.BusinessChainId,
                        principalTable: "NetworkArchiveBusinessChains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterRecords_BusinessChainId",
                table: "YearlyArchiveRegisterRecords",
                column: "BusinessChainId");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterRecords_SourceNetworkOutboundRecordId",
                table: "YearlyArchiveRegisterRecords",
                column: "SourceNetworkOutboundRecordId",
                unique: true,
                filter: "[SourceNetworkOutboundRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOutboundRecords_BusinessChainId",
                table: "NetworkOutboundRecords",
                column: "BusinessChainId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetAssets_OriginInboundItemId",
                table: "NetworkOnNetAssets",
                column: "OriginInboundItemId",
                unique: true,
                filter: "[OriginInboundItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundReturnHardDiskItems_InboundRecordId_MediumId",
                table: "NetworkInboundReturnHardDiskItems",
                columns: new[] { "InboundRecordId", "MediumId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundRecords_BusinessChainId",
                table: "NetworkInboundRecords",
                column: "BusinessChainId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkArchiveBusinessChains_ChainNo",
                table: "NetworkArchiveBusinessChains",
                column: "ChainNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkArchiveBusinessChains_PrimaryBusinessType_PrimaryBusinessId",
                table: "NetworkArchiveBusinessChains",
                columns: new[] { "PrimaryBusinessType", "PrimaryBusinessId" });

            migrationBuilder.CreateIndex(
                name: "IX_NetworkArchiveBusinessChains_ScenarioKind",
                table: "NetworkArchiveBusinessChains",
                column: "ScenarioKind");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkArchiveBusinessTasks_BusinessChainId",
                table: "NetworkArchiveBusinessTasks",
                column: "BusinessChainId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkArchiveBusinessTasks_BusinessType_BusinessId",
                table: "NetworkArchiveBusinessTasks",
                columns: new[] { "BusinessType", "BusinessId" });

            migrationBuilder.CreateIndex(
                name: "IX_NetworkArchiveBusinessTasks_DedupKey",
                table: "NetworkArchiveBusinessTasks",
                column: "DedupKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkInboundRecords_NetworkArchiveBusinessChains_BusinessChainId",
                table: "NetworkInboundRecords",
                column: "BusinessChainId",
                principalTable: "NetworkArchiveBusinessChains",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkOutboundRecords_NetworkArchiveBusinessChains_BusinessChainId",
                table: "NetworkOutboundRecords",
                column: "BusinessChainId",
                principalTable: "NetworkArchiveBusinessChains",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_YearlyArchiveRegisterRecords_NetworkArchiveBusinessChains_BusinessChainId",
                table: "YearlyArchiveRegisterRecords",
                column: "BusinessChainId",
                principalTable: "NetworkArchiveBusinessChains",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NetworkInboundRecords_NetworkArchiveBusinessChains_BusinessChainId",
                table: "NetworkInboundRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_NetworkOutboundRecords_NetworkArchiveBusinessChains_BusinessChainId",
                table: "NetworkOutboundRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_YearlyArchiveRegisterRecords_NetworkArchiveBusinessChains_BusinessChainId",
                table: "YearlyArchiveRegisterRecords");

            migrationBuilder.DropTable(
                name: "NetworkArchiveBusinessTasks");

            migrationBuilder.DropTable(
                name: "NetworkArchiveBusinessChains");

            migrationBuilder.DropIndex(
                name: "IX_YearlyArchiveRegisterRecords_BusinessChainId",
                table: "YearlyArchiveRegisterRecords");

            migrationBuilder.DropIndex(
                name: "IX_YearlyArchiveRegisterRecords_SourceNetworkOutboundRecordId",
                table: "YearlyArchiveRegisterRecords");

            migrationBuilder.DropIndex(
                name: "IX_NetworkOutboundRecords_BusinessChainId",
                table: "NetworkOutboundRecords");

            migrationBuilder.DropIndex(
                name: "IX_NetworkOnNetAssets_OriginInboundItemId",
                table: "NetworkOnNetAssets");

            migrationBuilder.DropIndex(
                name: "IX_NetworkInboundReturnHardDiskItems_InboundRecordId_MediumId",
                table: "NetworkInboundReturnHardDiskItems");

            migrationBuilder.DropIndex(
                name: "IX_NetworkInboundRecords_BusinessChainId",
                table: "NetworkInboundRecords");

            migrationBuilder.DropColumn(
                name: "BusinessChainId",
                table: "YearlyArchiveRegisterRecords");

            migrationBuilder.DropColumn(
                name: "SourceNetworkOutboundNo",
                table: "YearlyArchiveRegisterRecords");

            migrationBuilder.DropColumn(
                name: "SourceNetworkOutboundRecordId",
                table: "YearlyArchiveRegisterRecords");

            migrationBuilder.DropColumn(
                name: "BusinessChainId",
                table: "NetworkOutboundRecords");

            migrationBuilder.DropColumn(
                name: "BusinessChainId",
                table: "NetworkInboundRecords");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkOnNetAssets_OriginInboundItemId",
                table: "NetworkOnNetAssets",
                column: "OriginInboundItemId");
        }
    }
}
