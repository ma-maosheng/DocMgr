using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNetworkInboundReturnHardDiskItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReturnBorrowedHardDiskWithInbound",
                table: "NetworkInboundRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "NetworkInboundReturnHardDiskItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InboundRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    MediumId = table.Column<int>(type: "INTEGER", nullable: false),
                    DiskCode = table.Column<string>(type: "TEXT", nullable: false),
                    SourceApplicationId = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceOutboundRecordId = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetBlankSlotLocation = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkInboundReturnHardDiskItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkInboundReturnHardDiskItems_NetworkInboundRecords_InboundRecordId",
                        column: x => x.InboundRecordId,
                        principalTable: "NetworkInboundRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundReturnHardDiskItems_InboundRecordId",
                table: "NetworkInboundReturnHardDiskItems",
                column: "InboundRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkInboundReturnHardDiskItems_MediumId",
                table: "NetworkInboundReturnHardDiskItems",
                column: "MediumId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NetworkInboundReturnHardDiskItems");

            migrationBuilder.DropColumn(
                name: "ReturnBorrowedHardDiskWithInbound",
                table: "NetworkInboundRecords");
        }
    }
}
