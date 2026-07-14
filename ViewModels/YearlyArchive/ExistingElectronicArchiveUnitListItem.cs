using DocMgr.Models.YearlyArchive;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 第三步「该项目当年已存在的电子介质袋」列表行：区分可并档硬盘袋与光盘袋。
    /// </summary>
    public sealed class ExistingElectronicArchiveUnitListItem
    {
        public required YearlyElectronicArchiveUnit Unit { get; init; }

        public string ElectronicArchiveNo => Unit.ElectronicArchiveNo;

        public string StorageLocation => Unit.StorageLocation;

        public string Remarks => Unit.Remarks;

        /// <summary>
        /// 袋内硬盘号为空表示光盘袋，不参与并档。
        /// </summary>
        public bool IsOpticalDiscBag => string.IsNullOrWhiteSpace(Unit.LinkedMediumCodes);

        public bool CanSelectForAppend => !IsOpticalDiscBag;

        public string LinkedMediumCodesDisplay => IsOpticalDiscBag ? "光盘" : Unit.LinkedMediumCodes.Trim();

        public static ExistingElectronicArchiveUnitListItem From(YearlyElectronicArchiveUnit unit)
            => new() { Unit = unit };
    }
}
