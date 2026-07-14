using System;

namespace DocMgr.Models.SystemSettings
{
    /// <summary>
    /// 数据库操作审计日志。
    /// </summary>
    public class DbOperationLog
    {
        public long Id { get; set; }

        public DateTime OperationTime { get; set; }

        public int? UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? SessionId { get; set; }

        /// <summary>
        /// 触发保存的界面（主窗口标题或页面名称）。
        /// </summary>
        public string SourcePage { get; set; } = string.Empty;

        /// <summary>
        /// 触发保存的按钮或菜单项名称。
        /// </summary>
        public string SourceButton { get; set; } = string.Empty;

        /// <summary>
        /// 实体 CLR 类型名。
        /// </summary>
        public string EntityType { get; set; } = string.Empty;

        /// <summary>
        /// 数据库表名。
        /// </summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// 主键或业务标识摘要。
        /// </summary>
        public string EntityKey { get; set; } = string.Empty;

        /// <summary>
        /// Added / Modified / Deleted
        /// </summary>
        public string Operation { get; set; } = string.Empty;

        /// <summary>
        /// 变更字段 JSON。
        /// </summary>
        public string ChangedColumns { get; set; } = string.Empty;

        /// <summary>
        /// 便于列表浏览的单行摘要。
        /// </summary>
        public string Summary { get; set; } = string.Empty;
    }
}
