using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocMgr.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConvertHardDiskApplicationStatusToInt : Migration
    {
        /// <summary>
        /// 旧文案（现行 Text* 与历史 Legacy*/其他别名）→ 新 int 状态码的映射 SQL，
        /// 对应 <see cref="DocMgr.Models.Shared.ApplicationWorkflowStatus.TryParseStoredText"/> 的解析逻辑。
        /// </summary>
        private const string StatusTextToIntCaseSql = @"
            CASE ApplicationStatus
                WHEN '当前草稿-待提交' THEN 0
                WHEN '未提交' THEN 0
                WHEN '草稿' THEN 0
                WHEN '已提交-待审批' THEN 1
                WHEN '已提交' THEN 1
                WHEN '已登记' THEN 1
                WHEN '已登记归还信息' THEN 1
                WHEN '已审批-待实物交接' THEN 2
                WHEN '已审批' THEN 2
                WHEN '已实物交接-待上传签批交接单' THEN 3
                WHEN '已上传签字件' THEN 3
                WHEN '已办结审批' THEN 3
                WHEN '已办结（业务已闭环）' THEN 4
                WHEN '已办结' THEN 4
                WHEN '已办结出库' THEN 4
                WHEN '已作废（撤回）' THEN 5
                WHEN '已撤回作废' THEN 5
                WHEN '已作废' THEN 5
                WHEN '已作废（强制）' THEN 6
                WHEN '已强制作废' THEN 6
                ELSE 0
            END";

        /// <summary>新 int 状态码 → 现行 Text* 文案的映射 SQL，用于 Down() 回滚。</summary>
        private const string StatusIntToTextCaseSql = @"
            CASE ApplicationStatus
                WHEN 0 THEN '当前草稿-待提交'
                WHEN 1 THEN '已提交-待审批'
                WHEN 2 THEN '已审批-待实物交接'
                WHEN 3 THEN '已实物交接-待上传签批交接单'
                WHEN 4 THEN '已办结（业务已闭环）'
                WHEN 5 THEN '已作废（撤回）'
                WHEN 6 THEN '已作废（强制）'
                ELSE '当前草稿-待提交'
            END";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite 的 ApplicationStatus 原为 TEXT（中文状态文案），直接 CAST 为 INTEGER 会全部得 0，
            // 因此先新增整型临时列，按 ApplicationWorkflowStatus.TryParseStoredText 的口径回填数据，
            // 再替换原列，确保历史数据正确迁移为新的 int 状态码。
            migrationBuilder.Sql(
                "ALTER TABLE \"HardDiskMediaApplications\" ADD COLUMN \"ApplicationStatusInt\" INTEGER NOT NULL DEFAULT 0;");

            migrationBuilder.Sql(
                $"UPDATE \"HardDiskMediaApplications\" SET \"ApplicationStatusInt\" = {StatusTextToIntCaseSql};");

            migrationBuilder.Sql(
                "ALTER TABLE \"HardDiskMediaApplications\" DROP COLUMN \"ApplicationStatus\";");

            migrationBuilder.Sql(
                "ALTER TABLE \"HardDiskMediaApplications\" RENAME COLUMN \"ApplicationStatusInt\" TO \"ApplicationStatus\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"HardDiskMediaApplications\" ADD COLUMN \"ApplicationStatusText\" TEXT NOT NULL DEFAULT '';");

            migrationBuilder.Sql(
                $"UPDATE \"HardDiskMediaApplications\" SET \"ApplicationStatusText\" = {StatusIntToTextCaseSql};");

            migrationBuilder.Sql(
                "ALTER TABLE \"HardDiskMediaApplications\" DROP COLUMN \"ApplicationStatus\";");

            migrationBuilder.Sql(
                "ALTER TABLE \"HardDiskMediaApplications\" RENAME COLUMN \"ApplicationStatusText\" TO \"ApplicationStatus\";");
        }
    }
}
