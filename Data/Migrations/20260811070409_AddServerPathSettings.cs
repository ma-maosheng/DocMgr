using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServerPathSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServerPathSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DepartmentName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PathName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Permission = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CapacityTb = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerPathSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServerPathSettings_DepartmentName_PathName",
                table: "ServerPathSettings",
                columns: new[] { "DepartmentName", "PathName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServerPathSettings");
        }
    }
}
