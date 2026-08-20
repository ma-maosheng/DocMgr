using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSimulatedMediaItemDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YearlyArchiveRegisterSimulatedMediaItemDetails",
                columns: table => new
                {
                    MediaItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialCategory = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: ""),
                    SubCategory = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: ""),
                    OrganizationForm = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearlyArchiveRegisterSimulatedMediaItemDetails", x => x.MediaItemId);
                    table.ForeignKey(
                        name: "FK_YearlyArchiveRegisterSimulatedMediaItemDetails_YearlyArchiveRegisterMediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "YearlyArchiveRegisterMediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterSimulatedMediaItemDetails_MaterialCategory",
                table: "YearlyArchiveRegisterSimulatedMediaItemDetails",
                column: "MaterialCategory");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterSimulatedMediaItemDetails_OrganizationForm",
                table: "YearlyArchiveRegisterSimulatedMediaItemDetails",
                column: "OrganizationForm");

            migrationBuilder.CreateIndex(
                name: "IX_YearlyArchiveRegisterSimulatedMediaItemDetails_SubCategory",
                table: "YearlyArchiveRegisterSimulatedMediaItemDetails",
                column: "SubCategory");

            // 有损回填：仅资料子项；证明介质组保持旧 MediaType。
            migrationBuilder.Sql("""
                INSERT INTO YearlyArchiveRegisterSimulatedMediaItemDetails (MediaItemId, MaterialCategory, SubCategory, OrganizationForm)
                SELECT i.Id,
                    CASE
                        WHEN m.MediaType IN ('散页图件', '大幅图件') THEN '图件'
                        ELSE '文本'
                    END,
                    '其他',
                    CASE
                        WHEN m.MediaType IN ('装订文本') THEN '装订'
                        WHEN m.MediaType IN ('打印纸', '绘图纸', '打印相纸', '感光胶片', '感光相纸') THEN '装订'
                        ELSE '散页'
                    END
                FROM YearlyArchiveRegisterMediaItems i
                INNER JOIN YearlyArchiveRegisterMedias m ON m.Id = i.YearlyArchiveRegisterMediaId
                WHERE m.MediaKind = '模拟'
                  AND i.ItemType = '资料'
                  AND NOT EXISTS (
                      SELECT 1 FROM YearlyArchiveRegisterSimulatedMediaItemDetails d WHERE d.MediaItemId = i.Id
                  );
                """);

            migrationBuilder.Sql("""
                UPDATE YearlyArchiveRegisterMedias
                SET MediaType = CASE
                    WHEN MediaType IN ('散页图件', '大幅图件') THEN '绘图纸'
                    ELSE '打印纸'
                END
                WHERE MediaKind = '模拟'
                  AND MediaType IN ('装订文本', '散页文本', '散页图件', '大幅图件', '其他')
                  AND NOT EXISTS (
                      SELECT 1 FROM YearlyArchiveRegisterMediaItems i
                      WHERE i.YearlyArchiveRegisterMediaId = YearlyArchiveRegisterMedias.Id
                        AND i.ItemType = '证明'
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YearlyArchiveRegisterSimulatedMediaItemDetails");
        }
    }
}
