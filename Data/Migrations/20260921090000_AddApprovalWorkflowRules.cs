using DocMgr.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260921090000_AddApprovalWorkflowRules")]
    public class AddApprovalWorkflowRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalWorkflowRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BusinessType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RuleName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    ConditionLogic = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Condition1FieldKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Condition1Value = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Condition2FieldKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Condition2Value = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    EnableDeptHead = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableArchiveRoomHead = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableProductionHead = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableArchiveDeputyPresident = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableProductionVicePresident = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultDeptHeadUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    DefaultArchiveRoomHeadUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    DefaultProductionHeadUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    DefaultArchiveDeputyPresidentUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    DefaultProductionVicePresidentUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalWorkflowRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflowRules_BusinessType_Priority_IsEnabled",
                table: "ApprovalWorkflowRules",
                columns: new[] { "BusinessType", "Priority", "IsEnabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalWorkflowRules");
        }
    }
}
