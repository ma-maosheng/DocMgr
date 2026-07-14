using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料归还：评估借出快照与当前档案盒台账的一致性。
    /// </summary>
    public static class ArchiveReturnContainerAssessmentSupport
    {
        public static ArchiveReturnContainerAssessment Assess(
            YearlyArchiveReturnItem returnItem,
            YearlyArchiveFilingFact? fact,
            YearlyArchiveBox? box)
        {
            string borrowedCode = returnItem.ContainerCode?.Trim() ?? string.Empty;
            string borrowedLocation = returnItem.StorageLocation?.Trim() ?? string.Empty;

            if (!string.Equals(
                    returnItem.MediaKind?.Trim(),
                    ArchiveRegisterDomainValues.MediaKindSimulated,
                    StringComparison.Ordinal))
            {
                return ArchiveReturnContainerAssessment.Ok(
                    borrowedCode,
                    borrowedLocation,
                    borrowedCode,
                    borrowedLocation,
                    null);
            }

            if (box == null || box.Id <= 0)
            {
                return ArchiveReturnContainerAssessment.BoxInvalid(
                    borrowedCode,
                    borrowedLocation,
                    "原档案盒已不在台账中（可能已销号或数据缺失），请指定归还目标盒后再登记/办结。");
            }

            if (!ArchiveContainerLifecycleStatus.OccupiesCabinet(box.ContainerLifecycleStatus))
            {
                string statusText = string.Equals(
                        box.ContainerLifecycleStatus,
                        ArchiveContainerLifecycleStatus.Retired,
                        StringComparison.Ordinal)
                    ? "已销号"
                    : string.Equals(
                        box.ContainerLifecycleStatus,
                        ArchiveContainerLifecycleStatus.Emptied,
                        StringComparison.Ordinal)
                        ? "已清空占位"
                        : "非在用";
                return ArchiveReturnContainerAssessment.BoxInvalid(
                    borrowedCode,
                    borrowedLocation,
                    $"原档案盒 [{box.ArchiveSequenceNo}] {statusText}，请指定归还目标盒后再登记/办结。");
            }

            string liveCode = box.ArchiveSequenceNo?.Trim() ?? string.Empty;
            string liveLocation = box.BoxLocationCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(liveLocation))
            {
                return ArchiveReturnContainerAssessment.BoxInvalid(
                    borrowedCode,
                    borrowedLocation,
                    $"原档案盒 [{liveCode}] 无有效档口位置，请指定归还目标盒后再登记/办结。");
            }

            bool codeChanged = !EqualsNormalized(borrowedCode, liveCode);
            bool locationChanged = !EqualsNormalized(borrowedLocation, liveLocation);
            if (codeChanged || locationChanged)
            {
                return ArchiveReturnContainerAssessment.LocationChanged(
                    borrowedCode,
                    borrowedLocation,
                    liveCode,
                    liveLocation,
                    box.Id);
            }

            // 若快照一致但立档事实当前位置滞后，仍以活盒为准展示
            string factLocation = fact == null
                ? liveLocation
                : (string.IsNullOrWhiteSpace(fact.CurrentStorageLocation)
                    ? fact.StorageLocation?.Trim() ?? string.Empty
                    : fact.CurrentStorageLocation.Trim());
            if (!EqualsNormalized(factLocation, liveLocation))
            {
                return ArchiveReturnContainerAssessment.LocationChanged(
                    borrowedCode,
                    borrowedLocation,
                    liveCode,
                    liveLocation,
                    box.Id);
            }

            return ArchiveReturnContainerAssessment.Ok(
                borrowedCode,
                borrowedLocation,
                liveCode,
                liveLocation,
                box.Id);
        }

        public static void ApplyToReturnItem(YearlyArchiveReturnItem item, ArchiveReturnContainerAssessment assessment)
        {
            item.CurrentContainerCode = assessment.CurrentContainerCode;
            item.CurrentStorageLocation = assessment.CurrentStorageLocation;
            item.ContainerStatusKind = assessment.StatusKind;
            item.ContainerStatusDisplay = assessment.StatusDisplay;
            item.ContainerStatusWarning = assessment.WarningText;
            item.LiveBoxId = assessment.LiveBoxId;
            item.BlocksWithoutRehome = assessment.BlocksWithoutRehome;
        }

        private static bool EqualsNormalized(string? left, string? right) =>
            string.Equals(
                left?.Trim() ?? string.Empty,
                right?.Trim() ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
    }
}
