using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnContainerStatusAndRehome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RehomeTargetBoxId",
                table: "YearlyArchiveReturnItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContainerStatusHint",
                table: "YearlyArchiveOutboundItems",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RehomeTargetBoxId",
                table: "YearlyArchiveReturnItems");

            migrationBuilder.DropColumn(
                name: "ContainerStatusHint",
                table: "YearlyArchiveOutboundItems");
        }
    }
}
