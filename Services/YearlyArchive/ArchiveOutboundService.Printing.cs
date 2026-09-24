using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料出库申请单打印数据装配。
    /// </summary>
    public sealed partial class ArchiveOutboundService
    {
        private const string BlankDateText = "______年___月___日";

        public async Task<ArchiveOutboundPrintData> BuildPrintDataAsync(int recordId, bool blankApprovalSignatures)
        {
            var record = await _outboundRepository.GetByIdWithDetailsAsync(recordId)
                ?? throw new InvalidOperationException("未找到指定的出库申请单。");

            return await BuildPrintDataFromRecordAsync(record, blankApprovalSignatures);
        }

        public async Task<ArchiveOutboundPrintData> BuildPrintDataFromRecordAsync(
            YearlyArchiveOutboundRecord record,
            bool blankApprovalSignatures)
        {
            ArgumentNullException.ThrowIfNull(record);

            if (record.Status == YearlyArchiveOutboundRecord.Unsubmitted)
            {
                throw new InvalidOperationException("请先提交申请后再打印。");
            }

            await FillMissingOutboundItemArchivePurposesAsync(record.Items);
            var depletedFilingFactIds = await ResolveDepletedFilingFactIdsForPrintAsync(record);
            var classificationByFilingFactId = await LoadClassificationByFilingFactIdsAsync(
                record.Items.Select(item => item.FilingFactId));
            var chain = await ResolveOutboundApprovalChainAsync(record);
            return BuildPrintData(record, blankApprovalSignatures, depletedFilingFactIds, classificationByFilingFactId, chain);
        }

        public async Task RecordPrintAsync(int recordId)
        {
            var record = await _outboundRepository.GetByIdWithDetailsAsync(recordId)
                ?? throw new InvalidOperationException("未找到指定的出库申请单。");

            int printCount = record.PrintCount;
            DateTime? firstPrintedAt = record.FirstPrintedAt;
            DateTime? lastPrintedAt = record.LastPrintedAt;
            ApprovalSignatureDateSupport.RecordPrint(ref printCount, ref firstPrintedAt, ref lastPrintedAt);
            record.PrintCount = printCount;
            record.FirstPrintedAt = firstPrintedAt;
            record.LastPrintedAt = lastPrintedAt;
            record.UpdatedAt = DateTime.Now;
            await _outboundRepository.SaveOrUpdateRecordGraphAsync(record);
        }

        public async Task<ArchiveOutboundHandoverPrintData> BuildHandoverPrintDataAsync(
            int recordId,
            string? handoverRemark,
            bool blankHandoverSignatures)
        {
            var record = await _outboundRepository.GetByIdWithDetailsAsync(recordId)
                ?? throw new InvalidOperationException("未找到指定的出库申请单。");

            if (record.Status is not (
                YearlyArchiveOutboundRecord.Approved
                or YearlyArchiveOutboundRecord.SignedUploaded
                or YearlyArchiveOutboundRecord.Completed))
            {
                throw new InvalidOperationException("只有已审批及之后阶段的申请单可打印交接单。");
            }

            return await BuildHandoverPrintDataAsync(record, handoverRemark, blankHandoverSignatures);
        }

        private async Task<ArchiveOutboundHandoverPrintData> BuildHandoverPrintDataAsync(
            YearlyArchiveOutboundRecord record,
            string? handoverRemark,
            bool blankHandoverSignatures)
        {
            var factIds = record.Items
                .Select(item => item.FilingFactId)
                .Distinct()
                .ToList();
            var factsById = await _outboundRepository.GetFilingFactsByIdsForUpdateAsync(factIds);
            var classificationByFilingFactId = await LoadClassificationByFilingFactIdsAsync(factIds);
            var chain = await ResolveOutboundApprovalChainAsync(record);
            // 交接单可打时已是「已审批及之后」：审核/审批责任人一律预填；交接双方仍按办结与否留白/预填。
            bool blankApprovalSignatures = record.Status is not (
                YearlyArchiveOutboundRecord.Approved
                or YearlyArchiveOutboundRecord.SignedUploaded
                or YearlyArchiveOutboundRecord.Completed);
            return BuildHandoverPrintData(
                record,
                handoverRemark,
                blankHandoverSignatures,
                blankApprovalSignatures,
                factsById,
                classificationByFilingFactId,
                chain);
        }

        private static ArchiveOutboundHandoverPrintData BuildHandoverPrintData(
            YearlyArchiveOutboundRecord record,
            string? handoverRemark,
            bool blankHandoverSignatures,
            bool blankApprovalSignatures,
            IReadOnlyDictionary<int, YearlyArchiveFilingFact> factsById,
            IReadOnlyDictionary<int, string> classificationByFilingFactId,
            ApprovalChainResolution chain)
        {
            string remark = string.IsNullOrWhiteSpace(handoverRemark)
                ? record.HandoverRemark?.Trim() ?? string.Empty
                : handoverRemark.Trim();

            string printDate = DateTime.Now.ToString("yyyy-MM-dd");

            return new ArchiveOutboundHandoverPrintData
            {
                OutboundNo = record.OutboundNo,
                PrintDateText = printDate,
                ApplicantDept = record.ApplicantDept,
                ApplicantName = record.ApplicantName ?? string.Empty,
                MaterialSummary = string.IsNullOrWhiteSpace(record.MaterialSummary) ? "(无)" : record.MaterialSummary,
                ItemLines = ArchiveOutboundItemDescription
                    .BuildHandoverPrintDetailLines(record.Items, factsById, classificationByFilingFactId)
                    .ToList(),
                EnableDeptHead = chain.DeptHead.IsEnabled,
                EnableArchiveRoomHead = chain.ArchiveRoomHead.IsEnabled,
                EnableProductionHead = chain.ProductionHead.IsEnabled,
                EnableArchiveDeputyPresident = chain.ArchiveDeputyPresident.IsEnabled,
                EnableProductionVicePresident = chain.ProductionVicePresident.IsEnabled,
                DeptHeadBlock = BuildApprovalBlock(
                    blankApprovalSignatures ? string.Empty : record.DeptHead,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.DeptHeadDate)),
                ArchiveRoomHeadBlock = BuildApprovalBlock(
                    blankApprovalSignatures ? string.Empty : record.ArchiveRoomHead,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.ArchiveRoomHeadDate)),
                ProductionHeadBlock = BuildApprovalBlock(
                    blankApprovalSignatures ? string.Empty : record.ProductionHead,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.ProductionHeadDate)),
                ArchiveDeputyPresidentBlock = BuildApprovalBlock(
                    blankApprovalSignatures ? string.Empty : record.ArchiveDeputyPresident,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.ArchiveDeputyPresidentDate)),
                ProductionVicePresidentBlock = BuildApprovalBlock(
                    blankApprovalSignatures ? string.Empty : record.ProductionVicePresident,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.ProductionVicePresidentDate)),
                HandoverSignatureBlock = blankHandoverSignatures
                    ? BuildBlankHandoverSignatureBlock()
                    : BuildFilledHandoverSignatureBlock(record),
                HandoverRemark = remark,
                PrintCount = record.PrintCount
            };
        }

        private static string BuildBlankHandoverSignatureBlock() =>
            "\n领用人签字：                                            日期:______年___月___日\n" +
            "资料室资料员签字：                                 日期:______年___月___日";

        private static string BuildFilledHandoverSignatureBlock(YearlyArchiveOutboundRecord record)
        {
            string recipient = record.ApplicantName?.Trim() ?? string.Empty;
            string admin = record.PhysicallyCompletedBy?.Trim() ?? string.Empty;
            string recipientSlot = string.IsNullOrWhiteSpace(recipient) ? "________________" : recipient;
            string adminSlot = string.IsNullOrWhiteSpace(admin) ? "________________" : admin;
            string dateText = FormatDate(record.CompletedAt);

            return $"\n领用人签字：{recipientSlot}    日期：{dateText}\n" +
                   $"资料室资料员签字：{adminSlot}    日期：{dateText}";
        }

        private static ArchiveOutboundPrintData BuildPrintData(
            YearlyArchiveOutboundRecord record,
            bool blankApprovalSignatures,
            IReadOnlySet<int> depletedFilingFactIds,
            IReadOnlyDictionary<int, string> classificationByFilingFactId,
            ApprovalChainResolution chain)
        {
            string applyDate = record.ApplyDate == default
                ? string.Empty
                : record.ApplyDate.ToString("yyyy-MM-dd");

            string archiveYear = record.ArchiveYear?.ToString() ?? string.Empty;
            string longTermDepletionNotice = depletedFilingFactIds.Count > 0
                ? ArchiveSimulatedLongTermWithdrawalDepletionSupport.BuildPrintReviewNoticeText()
                : string.Empty;

            // 打印不输出意见正文：签字即代表同意。
            return new ArchiveOutboundPrintData
            {
                OutboundNo = record.OutboundNo,
                ApplyDateText = applyDate,
                ApplicantName = record.ApplicantName,
                ApplicantDept = record.ApplicantDept,
                ArchiveYearText = archiveYear,
                ProjectName = record.ProjectName,
                Reason = record.Reason,
                DestinationText = FormatDestination(record),
                ConfidentialMaterialDispositionText = FormatConfidentialMaterialDisposition(record),
                LongTermSimulatedStockDepletionNoticeText = longTermDepletionNotice,
                ProofMaterialNote = FormatProofMaterialName(record),
                MaterialSummary = string.IsNullOrWhiteSpace(record.MaterialSummary) ? "(无)" : record.MaterialSummary,
                ExpectedReturnDateText = FormatExpectedReturnDate(record),
                ItemLines = ArchiveOutboundItemDescription
                    .BuildPrintDetailLines(record.Items, depletedFilingFactIds, classificationByFilingFactId)
                    .ToList(),
                EnableDeptHead = chain.DeptHead.IsEnabled,
                EnableArchiveRoomHead = chain.ArchiveRoomHead.IsEnabled,
                EnableProductionHead = chain.ProductionHead.IsEnabled,
                EnableArchiveDeputyPresident = chain.ArchiveDeputyPresident.IsEnabled,
                EnableProductionVicePresident = chain.ProductionVicePresident.IsEnabled,
                DeptHeadBlock = BuildApprovalBlock(
                    blankApprovalSignatures ? string.Empty : record.DeptHead,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.DeptHeadDate)),
                ArchiveRoomHeadBlock = BuildApprovalBlock(
                    blankApprovalSignatures ? string.Empty : record.ArchiveRoomHead,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.ArchiveRoomHeadDate)),
                ProductionHeadBlock = BuildApprovalBlock(
                    blankApprovalSignatures ? string.Empty : record.ProductionHead,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.ProductionHeadDate)),
                ArchiveDeputyPresidentBlock = BuildApprovalBlock(
                    blankApprovalSignatures ? string.Empty : record.ArchiveDeputyPresident,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.ArchiveDeputyPresidentDate)),
                ProductionVicePresidentBlock = BuildApprovalBlock(
                    blankApprovalSignatures ? string.Empty : record.ProductionVicePresident,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.ProductionVicePresidentDate)),
                // 办结前留白供手签；已办结重打时预填交接人（见 handover-signature-print-blank）。
                HandoverSignatureBlock = record.IsCompleted
                    ? BuildFilledHandoverSignatureBlock(record)
                    : BuildBlankHandoverSignatureBlock(),
                PrintCount = record.PrintCount
            };
        }

        private static string FormatDestination(YearlyArchiveOutboundRecord record)
        {
            bool isExternal = string.Equals(
                record.DestinationKind,
                ArchiveOutboundDomainValues.DestinationExternal,
                StringComparison.Ordinal);

            string internalMark = isExternal ? "□" : "■";
            string externalMark = isExternal ? "■" : "□";
            string unit = isExternal ? record.ExternalUnit?.Trim() ?? string.Empty : string.Empty;

            return $"{internalMark}本部门（内部）  {externalMark}外部（单位）：{unit}";
        }

        private static string FormatProofMaterialName(YearlyArchiveOutboundRecord record)
        {
            string note = record.ProofMaterialNote?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(note)
                || string.Equals(note, ArchiveOutboundDomainValues.ProofMaterialNoneText, StringComparison.Ordinal))
            {
                return "无";
            }

            return note;
        }

        private static string FormatExpectedReturnDate(YearlyArchiveOutboundRecord record)
        {
            bool requiresReturn = record.Items.Any(item => item.NeedReturn || item.RequisitionedDiskNeedReturn);
            if (!requiresReturn)
            {
                return "无";
            }

            return record.ExpectedReturnDate?.ToString("yyyy-MM-dd") ?? "无";
        }

        private async Task<IReadOnlyDictionary<int, string>> LoadClassificationByFilingFactIdsAsync(
            IEnumerable<int> filingFactIds)
        {
            var ids = filingFactIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            var factsById = await _outboundRepository.GetFilingFactsByIdsForUpdateAsync(ids);
            var mediaItemIds = factsById.Values
                .Select(fact => fact.MediaItemId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            var mediaItems = await _filingFactRepository.GetRegisterMediaItemsWithSupplementsAsync(mediaItemIds);
            return SimulatedMediaItemClassificationSupport.MapClassificationByFilingFactId(
                factsById.Values,
                mediaItems.ToDictionary(item => item.Id));
        }

        private const string ConfidentialDispositionInstructionText =
            "申请人负有所借涉密资料的保管、使用、移交和销毁责任，日常工作中应消除一切失泄密隐患，杜绝失泄密事件发生。";

        /// <summary>
        /// 与申请单明细「涉密情况」对齐：任一明细涉密则输出处置说明，否则「不适用」。
        /// </summary>
        private static string FormatConfidentialMaterialDisposition(YearlyArchiveOutboundRecord record) =>
            record.Items.Any(IsConfidentialOutboundItem)
                ? ConfidentialDispositionInstructionText
                : "不适用";

        private static bool IsConfidentialOutboundItem(YearlyArchiveOutboundItem item)
        {
            string level = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(item.ConfidentialLevel);
            return !string.IsNullOrWhiteSpace(level)
                && !string.Equals(level, ArchiveRegisterDomainValues.ConfidentialLevelNone, StringComparison.Ordinal);
        }

        private static string BuildApprovalBlock(string signer, string dateText) =>
            PrintApprovalSignatureSupport.FormatInline(signer, dateText);

        private static string FormatDate(DateTime? value) =>
            value.HasValue ? value.Value.ToString("yyyy-MM-dd") : BlankDateText;
    }
}
