using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropHistoryLedgerBoxProjectionColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite 删列走 ef_temp_ 表重建；清掉上次失败残留的临时表，避免重试报 already exists。
            migrationBuilder.Sql("DROP TABLE IF EXISTS ef_temp_TopoMaps;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS ef_temp_OtherMaps;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS ef_temp_AerialPhotos;");

            migrationBuilder.DropColumn(
                name: "BoxNumber",
                table: "TopoMaps");

            migrationBuilder.DropColumn(
                name: "BoxSpecification",
                table: "TopoMaps");

            migrationBuilder.DropColumn(
                name: "BoxNumber",
                table: "OtherMaps");

            migrationBuilder.DropColumn(
                name: "BoxSpecification",
                table: "OtherMaps");

            migrationBuilder.DropColumn(
                name: "BoxNumber",
                table: "AerialPhotos");

            migrationBuilder.DropColumn(
                name: "BoxSpecification",
                table: "AerialPhotos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS ef_temp_TopoMaps;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS ef_temp_OtherMaps;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS ef_temp_AerialPhotos;");

            migrationBuilder.AddColumn<string>(
                name: "BoxNumber",
                table: "TopoMaps",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BoxSpecification",
                table: "TopoMaps",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BoxNumber",
                table: "OtherMaps",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BoxSpecification",
                table: "OtherMaps",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BoxNumber",
                table: "AerialPhotos",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BoxSpecification",
                table: "AerialPhotos",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
