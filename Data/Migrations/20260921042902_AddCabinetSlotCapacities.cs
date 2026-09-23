using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCabinetSlotCapacities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HardDiskSlotCapacity",
                table: "Cabinets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "OpticalDiscSlotCapacity",
                table: "Cabinets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HardDiskSlotCapacity",
                table: "Cabinets");

            migrationBuilder.DropColumn(
                name: "OpticalDiscSlotCapacity",
                table: "Cabinets");
        }
    }
}
