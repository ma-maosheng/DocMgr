using System.Collections.Generic;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 电子介质立档界面统一决策结果。
    /// </summary>
    public sealed record ElectronicArchiveUiDecision(
        ElectronicArchiveScenarioInput Input,
        IReadOnlyList<ElectronicArchiveSubmissionModeOption> AvailableModes,
        ElectronicArchiveSubmissionMode? SelectedMode,
        bool CanAppend,
        string AppendRestrictionReason,
        ElectronicArchiveStepFourLayoutDescriptor StepFourLayout,
        string StorageCarrierType,
        string SummaryHint);
}
