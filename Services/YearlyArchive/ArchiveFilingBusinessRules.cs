using System;
using System.Collections.Generic;
using System.Linq;
using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 年度资料立档业务规则。
    /// </summary>
    internal static class ArchiveFilingBusinessRules
    {
        internal const string DefaultElectronicBagCarrierType = "硬盘袋";
        internal const string DefaultOpticalDiscBagCarrierType = "光盘袋";
        internal const string BorrowedHardDiskSourceOption = "资料室借出硬盘";
        internal const string ExternalHardDiskSourceOption = "外来硬盘";
        internal const string BlankHardDiskSourceOption = "选择一块空硬盘，立档";
        internal const string DirectRetainedHardDiskSourceOption = "使用该硬盘直接立档";
        internal const string SingleOpticalDiscSourceOption = "使用1张光盘，立档";
        internal const string AppendExistingHardDiskOption = "数据并入本项目已立档硬盘";
        internal const string HardDiskSelectionModeBlankTarget = "BlankTarget";

        /// <summary>
        /// 根据第一步介质编号推断硬盘留存来源：有有效编号为资料室借出硬盘，否则为外来硬盘。
        /// </summary>
        internal static string ResolveRetainedHardDiskSourceFromStepOneMediumCode(string? stepOneMediumCode)
        {
            if (string.IsNullOrWhiteSpace(stepOneMediumCode))
            {
                return ExternalHardDiskSourceOption;
            }

            string trimmed = stepOneMediumCode.Trim();
            if (string.Equals(trimmed, "—", StringComparison.Ordinal))
            {
                return ExternalHardDiskSourceOption;
            }

            return BorrowedHardDiskSourceOption;
        }

        /// <summary>
        /// 解析电子介质立档界面决策。
        /// </summary>
        internal static ElectronicArchiveUiDecision ResolveUiDecision(ElectronicArchiveScenarioInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            var mediaTypes = input.SelectedMediaTypes
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
            string normalizedDisposition = input.Disposition?.Trim() ?? string.Empty;
            bool hasExistingHardDiskArchive = input.ExistingElectronicUnits.Any(unit => IsHardDiskArchiveCarrierType(unit.StorageCarrierType));
            bool hasExistingOpticalDiscArchive = input.ExistingElectronicUnits.Any(unit => IsOpticalDiscArchiveCarrierType(unit.StorageCarrierType));

            bool isUsbScenario = mediaTypes.Count == 1 && string.Equals(mediaTypes[0], ArchiveRegisterDomainValues.ElectronicMediaTypeUsbDrive, StringComparison.Ordinal);
            bool isInnerNetworkScenario = mediaTypes.Count == 1 && string.Equals(mediaTypes[0], ArchiveRegisterDomainValues.ElectronicMediaTypeInnerNetwork, StringComparison.Ordinal);
            bool isOpticalDiscScenario = mediaTypes.Count == 1 && string.Equals(mediaTypes[0], ArchiveRegisterDomainValues.ElectronicMediaTypeOpticalDisc, StringComparison.Ordinal);
            bool isHardDiskScenario = mediaTypes.Count == 1 && string.Equals(mediaTypes[0], ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk, StringComparison.Ordinal);
            bool isHardDiskReturnScenario = isHardDiskScenario && string.Equals(normalizedDisposition, ArchiveRegisterDomainValues.ElectronicDispositionReturn, StringComparison.Ordinal);
            bool isHardDiskRetainedScenario = isHardDiskScenario && string.Equals(normalizedDisposition, ArchiveRegisterDomainValues.ElectronicDispositionRetain, StringComparison.Ordinal);
            bool isOpticalDiscArchiveScenario = isOpticalDiscScenario && string.Equals(normalizedDisposition, ArchiveRegisterDomainValues.ElectronicDispositionRetain, StringComparison.Ordinal);
            bool isCopyScenario = isUsbScenario || isInnerNetworkScenario || isHardDiskReturnScenario;

            string retainedSourceForRules = isHardDiskRetainedScenario
                ? ResolveRetainedHardDiskSourceFromStepOneMediumCode(input.StepOneMediumCode)
                : string.Empty;

            var availableModes = BuildAvailableModes(
                isCopyScenario,
                isOpticalDiscArchiveScenario,
                isHardDiskRetainedScenario,
                hasExistingHardDiskArchive);
            ElectronicArchiveSubmissionMode? selectedMode = ResolveSelectedMode(availableModes, input.SelectedSubmissionMode);
            if (selectedMode == null && availableModes.Count > 0)
            {
                selectedMode = availableModes.FirstOrDefault(item => item.IsDefault)?.Mode ?? availableModes[0].Mode;
            }

            var stepFourLayout = BuildStepFourLayout(selectedMode, retainedSourceForRules, isHardDiskRetainedScenario);
            bool canAppend = hasExistingHardDiskArchive && !isOpticalDiscArchiveScenario && (isCopyScenario || isHardDiskRetainedScenario);
            string appendRestrictionReason = canAppend
                ? string.Empty
                : isOpticalDiscArchiveScenario
                    ? "光盘留存场景每张光盘需单独立档，不允许并档。"
                    : "当前场景仅允许新建立档。";

            return new ElectronicArchiveUiDecision(
                Input: input,
                AvailableModes: availableModes,
                SelectedMode: selectedMode,
                CanAppend: canAppend,
                AppendRestrictionReason: appendRestrictionReason,
                StepFourLayout: stepFourLayout,
                StorageCarrierType: ResolveStorageCarrierType(selectedMode),
                SummaryHint: BuildSummaryHint(selectedMode, canAppend, hasExistingOpticalDiscArchive));
        }

        /// <summary>
        /// 解析目标硬盘选择模式。
        /// </summary>
        internal static string ResolveHardDiskSelectionMode(string? hardDiskCopyTargetMode)
        {
            return string.Equals(hardDiskCopyTargetMode?.Trim(), BlankHardDiskSourceOption, StringComparison.Ordinal)
                ? HardDiskSelectionModeBlankTarget
                : string.Empty;
        }

        private static IReadOnlyList<ElectronicArchiveSubmissionModeOption> BuildAvailableModes(
            bool isCopyScenario,
            bool isOpticalDiscArchiveScenario,
            bool isHardDiskRetainedScenario,
            bool hasExistingHardDiskArchive)
        {
            bool hasSupportedScenario = isCopyScenario || isOpticalDiscArchiveScenario || isHardDiskRetainedScenario;

            ElectronicArchiveSubmissionMode opticalMode = isOpticalDiscArchiveScenario
                ? ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew
                : isHardDiskRetainedScenario
                    ? ElectronicArchiveSubmissionMode.RetainedHardDiskCopyToOpticalDisc
                    : ElectronicArchiveSubmissionMode.CopyNewOpticalDisc;
            ElectronicArchiveSubmissionMode appendMode = isHardDiskRetainedScenario
                ? ElectronicArchiveSubmissionMode.RetainedHardDiskAppendExistingHardDisk
                : ElectronicArchiveSubmissionMode.CopyAppendExistingHardDisk;

            bool opticalEnabled = hasSupportedScenario;
            bool blankHardDiskEnabled = isCopyScenario || isHardDiskRetainedScenario;
            bool directRetainedHardDiskEnabled = isHardDiskRetainedScenario;
            bool appendEnabled = (isCopyScenario || isHardDiskRetainedScenario) && hasExistingHardDiskArchive;

            string opticalDisabledReason = opticalEnabled
                ? string.Empty
                : "当前场景不支持电子介质立档。";
            string blankHardDiskDisabledReason = blankHardDiskEnabled
                ? string.Empty
                : isOpticalDiscArchiveScenario
                    ? "光盘留存场景每张光盘需单独立档，不能使用库内空硬盘。"
                    : "当前场景不支持该立档方式。";
            string directRetainedHardDiskDisabledReason = directRetainedHardDiskEnabled
                ? string.Empty
                : isCopyScenario
                    ? "当前场景来源介质并非留存硬盘，不能直接使用该硬盘立档。"
                    : isOpticalDiscArchiveScenario
                        ? "光盘留存场景每张光盘需单独立档，不能直接使用硬盘立档。"
                        : "当前场景不支持该立档方式。";
            string appendDisabledReason = appendEnabled
                ? string.Empty
                : isOpticalDiscArchiveScenario
                    ? "光盘留存场景每张光盘需单独立档，不允许并档。"
                    : !hasExistingHardDiskArchive
                        ? "所属项目当年无已立档硬盘袋，暂不可并档。"
                        : "当前场景不支持并档。";

            bool isOpticalDefault = opticalEnabled && (isCopyScenario || isOpticalDiscArchiveScenario);
            bool isDirectHardDiskDefault = directRetainedHardDiskEnabled && isHardDiskRetainedScenario;

            return
            [
                new ElectronicArchiveSubmissionModeOption(
                    opticalMode,
                    SingleOpticalDiscSourceOption,
                    "1、选择年度数据光盘专用档口；2、赋码确认。",
                    opticalEnabled,
                    opticalDisabledReason,
                    false,
                    isOpticalDefault),
                new ElectronicArchiveSubmissionModeOption(
                    ElectronicArchiveSubmissionMode.CopyNewHardDisk,
                    BlankHardDiskSourceOption,
                    "在本步选择库内空硬盘；随后选择年度数据硬盘专用档口并赋码确认。",
                    blankHardDiskEnabled,
                    blankHardDiskDisabledReason,
                    false,
                    false),
                new ElectronicArchiveSubmissionModeOption(
                    ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew,
                    DirectRetainedHardDiskSourceOption,
                    "1、选择年度数据硬盘专用档口；2、赋码确认；3、按申请信息完成拟留存硬盘的新增登记、归还或入库处置。",
                    directRetainedHardDiskEnabled,
                    directRetainedHardDiskDisabledReason,
                    false,
                    isDirectHardDiskDefault),
                new ElectronicArchiveSubmissionModeOption(
                    appendMode,
                    AppendExistingHardDiskOption,
                    "将数据并入本项目已立档硬盘（沿用原电子袋与原数据硬盘）。",
                    appendEnabled,
                    appendDisabledReason,
                    true,
                    false)
            ];
        }

        private static ElectronicArchiveSubmissionMode? ResolveSelectedMode(
            IReadOnlyList<ElectronicArchiveSubmissionModeOption> availableModes,
            ElectronicArchiveSubmissionMode? selectedMode)
        {
            if (selectedMode == null)
            {
                return null;
            }

            return availableModes.Any(item => item.Mode == selectedMode && item.IsEnabled)
                ? selectedMode
                : null;
        }

        private static ElectronicArchiveStepFourLayoutDescriptor BuildStepFourLayout(
            ElectronicArchiveSubmissionMode? selectedMode,
            string retainedHardDiskSource,
            bool isHardDiskRetainedScenario)
        {
            if (selectedMode == null)
            {
                return new ElectronicArchiveStepFourLayoutDescriptor(false, false, false);
            }

            bool showExternalRegistration = isHardDiskRetainedScenario
                && string.Equals(retainedHardDiskSource, ExternalHardDiskSourceOption, StringComparison.Ordinal)
                && selectedMode is ElectronicArchiveSubmissionMode.CopyNewHardDisk
                    or ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew
                    or ElectronicArchiveSubmissionMode.RetainedHardDiskCopyToOpticalDisc
                    or ElectronicArchiveSubmissionMode.RetainedHardDiskAppendExistingHardDisk;
            bool showExternalFormattedBlankLocation = isHardDiskRetainedScenario
                && string.Equals(retainedHardDiskSource, ExternalHardDiskSourceOption, StringComparison.Ordinal)
                && selectedMode is ElectronicArchiveSubmissionMode.CopyNewHardDisk
                    or ElectronicArchiveSubmissionMode.RetainedHardDiskCopyToOpticalDisc
                    or ElectronicArchiveSubmissionMode.RetainedHardDiskAppendExistingHardDisk;
            bool showBlankInventoryHardDiskSelection = selectedMode == ElectronicArchiveSubmissionMode.CopyNewHardDisk;

            return new ElectronicArchiveStepFourLayoutDescriptor(
                showExternalRegistration,
                showExternalFormattedBlankLocation,
                showBlankInventoryHardDiskSelection);
        }

        private static string ResolveStorageCarrierType(ElectronicArchiveSubmissionMode? selectedMode)
        {
            return selectedMode switch
            {
                ElectronicArchiveSubmissionMode.CopyNewOpticalDisc or
                ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew or
                ElectronicArchiveSubmissionMode.RetainedHardDiskCopyToOpticalDisc => DefaultOpticalDiscBagCarrierType,
                _ => DefaultElectronicBagCarrierType
            };
        }

        private static string BuildSummaryHint(ElectronicArchiveSubmissionMode? selectedMode, bool canAppend, bool hasExistingOpticalDiscArchive)
        {
            string appendHint = canAppend
                ? "本项目已有硬盘袋时可切换为并档。"
                : "当前场景不允许并档。";

            return selectedMode switch
            {
                ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew => "光盘留存场景每张光盘必须独立立档。",
                ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew => "当前留存硬盘将直接作为数据盘入袋立档。",
                ElectronicArchiveSubmissionMode.RetainedHardDiskCopyToOpticalDisc or ElectronicArchiveSubmissionMode.RetainedHardDiskAppendExistingHardDisk
                    => "当前留存硬盘将在完成立档后格式化并按空盘入库。",
                _ when hasExistingOpticalDiscArchive && !canAppend => appendHint + " 既有光盘袋不作为并档目标。",
                _ => appendHint
            };
        }

        internal static bool IsHardDiskArchiveCarrierType(string? storageCarrierType)
            => !string.IsNullOrWhiteSpace(storageCarrierType)
                && storageCarrierType.Contains("硬盘", StringComparison.OrdinalIgnoreCase);

        internal static bool IsOpticalDiscArchiveCarrierType(string? storageCarrierType)
            => !string.IsNullOrWhiteSpace(storageCarrierType)
                && storageCarrierType.Contains("光盘", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 是否为拷贝型硬盘立档（需校验目标硬盘可用容量；光盘立档不在此列）。
        /// </summary>
        internal static bool IsCopySubmissionMode(ElectronicArchiveSubmissionMode submissionMode)
            => submissionMode is ElectronicArchiveSubmissionMode.CopyNewHardDisk
                or ElectronicArchiveSubmissionMode.CopyAppendExistingHardDisk;

        /// <summary>
        /// 解析登记单在电子介质立档中的“所属项目”键（与袋上 ProjectName 对齐）。
        /// 内部项目用 <see cref="YearlyArchiveRegisterRecord.ProjectName"/>；外来资料无项目时用提供单位。
        /// </summary>
        internal static string ResolveElectronicArchiveProjectName(YearlyArchiveRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            if (!string.IsNullOrWhiteSpace(record.ProjectName))
            {
                return record.ProjectName.Trim();
            }

            if (string.Equals(record.SourceType?.Trim(), ArchiveRegisterDomainValues.SourceTypeExternal, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(record.ProvideUnit))
            {
                return record.ProvideUnit.Trim();
            }

            return string.Empty;
        }
    }
}
