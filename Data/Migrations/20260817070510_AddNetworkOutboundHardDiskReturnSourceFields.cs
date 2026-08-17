using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNetworkOutboundHardDiskReturnSourceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceNetworkOutboundRecordId",
                table: "NetworkInboundReturnHardDiskItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceNetworkOutboundRecordId",
                table: "HardDiskMediaApplications",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceNetworkOutboundRecordId",
                table: "NetworkInboundReturnHardDiskItems");

            migrationBuilder.DropColumn(
                name: "SourceNetworkOutboundRecordId",
                table: "HardDiskMediaApplications");
        }
    }
}
