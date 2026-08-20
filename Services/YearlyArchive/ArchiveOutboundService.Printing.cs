using DocMgr.Models.Shared;
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
            return BuildPrintData(record, blankApprovalSignatures, depletedFilingFactIds, classificationByFilingFactId);
        }

        public async Task RecordPrintAsync(int recordId)
        {
            var record = await _outboundRepository.GetByIdWithDetailsAsync(recordId)
                ?? throw new InvalidOperationException("未找到指定的出库申请单。");

            record.PrintCount++;
            record.LastPrintedAt = DateTime.Now;
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
            return BuildHandoverPrintData(record, handoverRemark, blankHandoverSignatures, factsById, classificationByFilingFactId);
        }

        private static ArchiveOutboundHandoverPrintData BuildHandoverPrintData(
            YearlyArchiveOutboundRecord record,
            string? handoverRemark,
            bool blankHandoverSignatures,
            IReadOnlyDictionary<int, YearlyArchiveFilingFact> factsById,
            IReadOnlyDictionary<int, string> classificationByFilingFactId)
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
            IReadOnlyDictionary<int, string> classificationByFilingFactId)
        {
            string applyDate = record.ApplyDate == default
                ? string.Empty
                : record.ApplyDate.ToString("yyyy-MM-dd");

            string archiveYear = record.ArchiveYear?.ToString() ?? string.Empty;
            string longTermDepletionNotice = depletedFilingFactIds.Count > 0
                ? ArchiveSimulatedLongTermWithdrawalDepletionSupport.BuildPrintReviewNoticeText()
                : string.Empty;

            // 历史数据可能仅部分节点有「同意」；打印前规范为全有或全无。
            var opinions = ApprovalOpinionUniformitySupport.NormalizeUniform(
                blankApprovalSignatures ? string.Empty : record.DeptAuditOpinion,
                blankApprovalSignatures ? string.Empty : record.ArchiveRoomHeadOpinion,
                blankApprovalSignatures ? string.Empty : record.ProductionHeadOpinion,
                blankApprovalSignatures ? string.Empty : record.VicePresidentOpinion);

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
                DeptAuditBlock = BuildApprovalBlock(
                    opinions[0],
                    blankApprovalSignatures ? string.Empty : record.DeptAuditor,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.DeptAuditDate)),
                ArchiveRoomHeadBlock = BuildApprovalBlock(
                    opinions[1],
                    blankApprovalSignatures ? string.Empty : record.ArchiveRoomHead,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.ArchiveRoomHeadDate)),
                ProductionHeadBlock = BuildApprovalBlock(
                    opinions[2],
                    blankApprovalSignatures ? string.Empty : record.ProductionHead,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.ProductionHeadDate)),
                VicePresidentBlock = BuildApprovalBlock(
                    opinions[3],
                    blankApprovalSignatures ? string.Empty : record.VicePresident,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.VicePresidentDate)),
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

        private static string BuildApprovalBlock(string opinion, string signer, string dateText)
        {
            string renderedOpinion = string.IsNullOrWhiteSpace(opinion) ? string.Empty : $"意见：{opinion.Trim()}  ";
            string signatureSlot = string.IsNullOrWhiteSpace(signer) ? "________________" : signer.Trim();
            string renderedDate = string.IsNullOrWhiteSpace(dateText) ? BlankDateText : dateText;
            return $"{renderedOpinion}签字：{signatureSlot}    日期：{renderedDate}";
        }

        private static string FormatDate(DateTime? value) =>
            value.HasValue ? value.Value.ToString("yyyy-MM-dd") : BlankDateText;
    }
}
