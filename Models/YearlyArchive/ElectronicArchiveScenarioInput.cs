using System.Collections.Generic;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 电子介质立档界面场景输入快照。
    /// </summary>
    public sealed record ElectronicArchiveScenarioInput
    {
        /// <summary>
        /// 项目名称。
        /// </summary>
        public string ProjectName { get; init; } = string.Empty;

        /// <summary>
        /// 归属年度。
        /// </summary>
        public string Year { get; init; } = string.Empty;

        /// <summary>
        /// 本次选中的来源介质类型集合。
        /// </summary>
        public IReadOnlyList<string> SelectedMediaTypes { get; init; } = [];

        /// <summary>
        /// 当前介质处置方式。
        /// </summary>
        public string Disposition { get; init; } = string.Empty;

        /// <summary>
        /// 本次选中的电子介质条目主键集合。
        /// </summary>
        public IReadOnlyList<int> SelectedMediaEntryIds { get; init; } = [];

        /// <summary>
        /// 当前项目年度下已存在的电子介质袋集合。
        /// </summary>
        public IReadOnlyList<YearlyElectronicArchiveUnit> ExistingElectronicUnits { get; init; } = [];

        /// <summary>
        /// 当前界面选择的立档动作。
        /// </summary>
        public ElectronicArchiveArchiveAction SelectedArchiveAction { get; init; } = ElectronicArchiveArchiveAction.New;

        /// <summary>
        /// 当前界面已选择的电子介质立档提交模式。
        /// </summary>
        public ElectronicArchiveSubmissionMode? SelectedSubmissionMode { get; init; }

        /// <summary>
        /// 当前选中的既有电子介质袋主键。
        /// </summary>
        public int? SelectedExistingElectronicUnitId { get; init; }

        /// <summary>
        /// 第一步所选电子介质表单的介质编号（有有效编号视为资料室借出硬盘；空或「—」视为外来硬盘）。用于硬盘留存场景下推断留存来源。
        /// </summary>
        public string? StepOneMediumCode { get; init; }

        /// <summary>
        /// 硬盘留存来源选择（与 <see cref="StepOneMediumCode"/> 推断结果保持一致，供提交快照等使用）。
        /// </summary>
        public string SelectedRetainedHardDiskSource { get; init; } = string.Empty;
    }
}
