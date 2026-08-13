using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNetworkInboundProvideUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProvideUnit",
                table: "NetworkInboundRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE NetworkInboundRecords
                SET SourceKind = '档外资料（内部）'
                WHERE SourceKind IN ('档外资料', '外部离线', '其他');
                """);

            migrationBuilder.Sql(
                """
                UPDATE NetworkInboundRecords
                SET ProvideUnit = '资料室'
                WHERE SourceKind IN ('立档资料', '已立档资料');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProvideUnit",
                table: "NetworkInboundRecords");
        }
    }
}
