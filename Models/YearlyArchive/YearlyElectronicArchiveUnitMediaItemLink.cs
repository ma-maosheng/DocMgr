using System;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 电子介质袋与资料子项的关联（立档明细，供检索）。
    /// </summary>
    public sealed class YearlyElectronicArchiveUnitMediaItemLink
    {
        public int Id { get; set; }

        public int YearlyElectronicArchiveUnitId { get; set; }

        public int YearlyArchiveRegisterMediaItemId { get; set; }

        /// <summary>
        /// 立档时写入目标介质的存储路径（拷贝型立档可编辑）。
        /// </summary>
        public string FilingStoragePath { get; set; } = string.Empty;

        /// <summary>
        /// 入袋时使用的物理介质编号（硬盘编号/光盘编号等）。
        /// </summary>
        public string MediumCode { get; set; } = string.Empty;

        public string FormNo { get; set; } = string.Empty;

        public string MaterialName { get; set; } = string.Empty;

        /// <summary>
        /// 登记子项名称快照（数据库列名 ContentSummary，历史遗留）。
        /// </summary>
        public string ItemName { get; set; } = string.Empty;

        public decimal DataSizeMb { get; set; }

        public DateTime CreatedAt { get; set; }

        public YearlyElectronicArchiveUnit ElectronicArchiveUnit { get; set; } = null!;

        public YearlyArchiveRegisterMediaItem MediaItem { get; set; } = null!;
    }
}
