using DocMgr.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260818070000_AddNetworkOnNetDisposalReviewSigners")]
    public class AddNetworkOnNetDisposalReviewSigners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchiveRoomHead",
                table: "NetworkOnNetDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiveRoomHeadDate",
                table: "NetworkOnNetDisposalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveDeputyPresident",
                table: "NetworkOnNetDisposalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchiveDeputyPresidentDate",
                table: "NetworkOnNetDisposalRecords",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchiveRoomHead",
                table: "NetworkOnNetDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveRoomHeadDate",
                table: "NetworkOnNetDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresident",
                table: "NetworkOnNetDisposalRecords");

            migrationBuilder.DropColumn(
                name: "ArchiveDeputyPresidentDate",
                table: "NetworkOnNetDisposalRecords");
        }
    }
}
