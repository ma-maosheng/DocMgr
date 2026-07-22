using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRegisterProofMaterialNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProofMaterialNote",
                table: "YearlyArchiveRegisterRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "无");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProofMaterialNote",
                table: "YearlyArchiveRegisterRecords");
        }
    }
}
