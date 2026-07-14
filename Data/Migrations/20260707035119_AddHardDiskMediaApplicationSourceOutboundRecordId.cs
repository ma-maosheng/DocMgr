using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHardDiskMediaApplicationSourceOutboundRecordId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceOutboundRecordId",
                table: "HardDiskMediaApplications",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HardDiskMediaApplications_SourceOutboundRecordId",
                table: "HardDiskMediaApplications",
                column: "SourceOutboundRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HardDiskMediaApplications_SourceOutboundRecordId",
                table: "HardDiskMediaApplications");

            migrationBuilder.DropColumn(
                name: "SourceOutboundRecordId",
                table: "HardDiskMediaApplications");
        }
    }
}
