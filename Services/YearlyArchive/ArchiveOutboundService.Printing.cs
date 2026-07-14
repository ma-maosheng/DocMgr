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
            return BuildPrintData(record, blankApprovalSignatures, depletedFilingFactIds);
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

            if (record.Status is not (YearlyArchiveOutboundRecord.SignedUploaded or YearlyArchiveOutboundRecord.Completed))
            {
                throw new InvalidOperationException("只有进入资料出库办理阶段的申请单可打印交接单。");
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
            return BuildHandoverPrintData(record, handoverRemark, blankHandoverSignatures, factsById);
        }

        private static ArchiveOutboundHandoverPrintData BuildHandoverPrintData(
            YearlyArchiveOutboundRecord record,
            string? handoverRemark,
            bool blankHandoverSignatures,
            IReadOnlyDictionary<int, YearlyArchiveFilingFact> factsById)
        {
            string remark = string.IsNullOrWhiteSpace(handoverRemark)
                ? record.HandoverRemark?.Trim() ?? string.Empty
                : handoverRemark.Trim();

            string printDate = DateTime.Now.ToString("yyyy-MM-dd");

            string recipient = blankHandoverSignatures ? string.Empty : record.ApplicantName?.Trim() ?? string.Empty;
            string admin = blankHandoverSignatures ? string.Empty : record.PhysicallyCompletedBy?.Trim() ?? string.Empty;
            string recipientDate = blankHandoverSignatures ? BlankDateText : FormatDate(record.CompletedAt);
            string adminDate = blankHandoverSignatures ? BlankDateText : FormatDate(record.CompletedAt);

            string recipientSlot = string.IsNullOrWhiteSpace(recipient) ? "________________" : recipient;
            string adminSlot = string.IsNullOrWhiteSpace(admin) ? "________________" : admin;

            return new ArchiveOutboundHandoverPrintData
            {
                OutboundNo = record.OutboundNo,
                PrintDateText = printDate,
                ApplicantDept = record.ApplicantDept,
                ApplicantName = record.ApplicantName ?? string.Empty,
                MaterialSummary = string.IsNullOrWhiteSpace(record.MaterialSummary) ? "(无)" : record.MaterialSummary,
                ItemLines = ArchiveOutboundItemDescription.BuildHandoverPrintDetailLines(record.Items, factsById).ToList(),
                HandoverSignatureBlock = blankHandoverSignatures
                    ? BuildBlankHandoverSignatureBlock()
                    : $"领用人签字：{recipientSlot}    日期：{recipientDate}\n资料室资料员签字：{adminSlot}    日期：{adminDate}",
                HandoverRemark = remark,
                PrintCount = record.PrintCount
            };
        }

        private static string BuildBlankHandoverSignatureBlock() =>
            "\n领用人签字：                                            日期:______年___月___日\n" +
            "资料室资料员签字：                                 日期:______年___月___日";

        private static ArchiveOutboundPrintData BuildPrintData(
            YearlyArchiveOutboundRecord record,
            bool blankApprovalSignatures,
            IReadOnlySet<int> depletedFilingFactIds)
        {
            string applyDate = record.ApplyDate == default
                ? string.Empty
                : record.ApplyDate.ToString("yyyy-MM-dd");

            string archiveYear = record.ArchiveYear?.ToString() ?? string.Empty;
            string longTermDepletionNotice = depletedFilingFactIds.Count > 0
                ? ArchiveSimulatedLongTermWithdrawalDepletionSupport.BuildPrintReviewNoticeText()
                : string.Empty;

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
                    .BuildPrintDetailLines(record.Items, depletedFilingFactIds)
                    .ToList(),
                DeptAuditBlock = BuildApprovalBlock(
                    blankApprovalSignatures ? string.Empty : record.DeptAuditOpinion,
                    blankApprovalSignatures ? string.Empty : record.DeptAuditor,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.DeptAuditDate)),
                ArchiveRoomHeadBlock = BuildApprovalBlock(
                    blankApprovalSignatures ? string.Empty : record.ArchiveRoomHeadOpinion,
                    blankApprovalSignatures ? string.Empty : record.ArchiveRoomHead,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.ArchiveRoomHeadDate)),
                ProductionHeadBlock = BuildApprovalBlock(
                    blankApprovalSignatures ? string.Empty : record.ProductionHeadOpinion,
                    blankApprovalSignatures ? string.Empty : record.ProductionHead,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.ProductionHeadDate)),
                VicePresidentBlock = BuildApprovalBlock(
                    blankApprovalSignatures ? string.Empty : record.VicePresidentOpinion,
                    blankApprovalSignatures ? string.Empty : record.VicePresident,
                    blankApprovalSignatures ? BlankDateText : FormatDate(record.VicePresidentDate)),
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

        private const string ConfidentialSelfRetainDispositionText =
            "申请人负责按保密要求对自用涉密资料进行自行销毁或按相关规定进行处置";

        private static string FormatConfidentialMaterialDisposition(YearlyArchiveOutboundRecord record) =>
            HasConfidentialSelfRetainWithoutReturn(record)
                ? ConfidentialSelfRetainDispositionText
                : "不适用";

        private static bool HasConfidentialSelfRetainWithoutReturn(YearlyArchiveOutboundRecord record)
        {
            if (!ArchiveOutboundDomainValues.IsExternalDestination(record.DestinationKind))
            {
                return record.Items.Any(IsConfidentialSelfRetainWithoutReturnItem);
            }

            return false;
        }

        private static bool IsConfidentialSelfRetainWithoutReturnItem(YearlyArchiveOutboundItem item)
        {
            if (!IsConfidentialOutboundItem(item))
            {
                return false;
            }

            return item.UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal && !item.NeedReturn;
        }

        private static bool IsConfidentialOutboundItem(YearlyArchiveOutboundItem item)
        {
            string level = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(item.ConfidentialLevel);
            return !string.Equals(level, ArchiveRegisterDomainValues.ConfidentialLevelNone, StringComparison.Ordinal);
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
