using System;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 硬盘或光盘与资料子项的关联。资料归属介质，不归属介质袋。
    /// </summary>
    public sealed class YearlyElectronicArchiveUnitMediaItemLink
    {
        public int Id { get; set; }

        /// <summary>
        /// 资料所在硬盘。与 <see cref="OpticalDiscMediumId"/> 二选一。
        /// </summary>
        public int? HardDiskMediumId { get; set; }

        /// <summary>
        /// 资料所在光盘。与 <see cref="HardDiskMediumId"/> 二选一。
        /// </summary>
        public int? OpticalDiscMediumId { get; set; }

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

        public HardDiskMedium? HardDiskMedium { get; set; }

        public OpticalDiscMedium? OpticalDiscMedium { get; set; }

        public YearlyArchiveRegisterMediaItem MediaItem { get; set; } = null!;
    }
}
