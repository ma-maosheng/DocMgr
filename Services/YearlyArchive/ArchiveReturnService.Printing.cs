using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料归还签批交接单/交接单打印数据装配。
    /// </summary>
    public sealed partial class ArchiveReturnService
    {
        private const string BlankDateText = "______年___月___日";
        private const string DocumentTitleSignedHandover = "河北省第三测绘院资料室年度资料归还签批交接单";
        private const string DocumentTitleHandoverSheet = "河北省第三测绘院资料室年度资料归还交接单";

        public async Task<ArchiveReturnReceiptPrintData> BuildReceiptPrintDataAsync(int recordId, bool blankHandoverSignatures)
        {
            var record = await _returnRepository.GetByIdWithDetailsAsync(recordId)
                ?? throw new InvalidOperationException("未找到指定的归还单。");

            if (record.Status is YearlyArchiveReturnRecord.WithdrawnVoid
                or YearlyArchiveReturnRecord.ForceVoided)
            {
                throw new InvalidOperationException("已作废的归还单不可打印交接单。");
            }

            // 申请侧：草稿/已提交可打签批交接单；审批侧：已审批及之后可打交接单。
            bool isApplicationPrintable = record.Status is YearlyArchiveReturnRecord.Draft
                or YearlyArchiveReturnRecord.Submitted;
            bool isApprovalPrintable = record.Status is YearlyArchiveReturnRecord.Approved
                or YearlyArchiveReturnRecord.SignedUploaded
                or YearlyArchiveReturnRecord.Completed;
            if (!isApplicationPrintable && !isApprovalPrintable)
            {
                throw new InvalidOperationException("当前状态不允许打印交接单。");
            }

            if (!isApplicationPrintable)
            {
                var abnormalGate = await ValidateAbnormalReturnGateAsync(record);
                if (!abnormalGate.Success)
                {
                    throw new InvalidOperationException(abnormalGate.Message);
                }
            }

            var outbound = await _outboundRepository.GetByIdWithDetailsAsync(record.SourceOutboundRecordId);
            string documentTitle = isApplicationPrintable
                ? DocumentTitleSignedHandover
                : DocumentTitleHandoverSheet;
            IReadOnlyDictionary<int, string> classificationByFilingFactId = await LoadClassificationByFilingFactIdsAsync(
                record.Items.Select(item => item.FilingFactId));
            return BuildReceiptPrintData(record, outbound, blankHandoverSignatures, documentTitle, classificationByFilingFactId);
        }

        public async Task RecordPrintAsync(int recordId)
        {
            var record = await _returnRepository.GetByIdWithDetailsAsync(recordId)
                ?? throw new InvalidOperationException("未找到指定的归还单。");

            record.PrintCount++;
            record.LastPrintedAt = DateTime.Now;
            record.UpdatedAt = DateTime.Now;
            await _returnRepository.SaveOrUpdateRecordGraphAsync(record);
        }

        private static ArchiveReturnReceiptPrintData BuildReceiptPrintData(
            YearlyArchiveReturnRecord record,
            YearlyArchiveOutboundRecord? outbound,
            bool blankHandoverSignatures,
            string documentTitle,
            IReadOnlyDictionary<int, string> classificationByFilingFactId)
        {
            string returnDate = record.ReturnDate == default
                ? string.Empty
                : record.ReturnDate.ToString("yyyy-MM-dd");

            string expectedReturnDate = outbound?.ExpectedReturnDate?.ToString("yyyy-MM-dd") ?? "无";

            ArchiveReturnItemDisplaySupport.EnrichFromOutbound(record, outbound);
            string materialSummary = ArchiveReturnItemDescription.BuildMaterialSummary(record.Items);
            bool hasLossReturn = ArchiveReturnDomainValues.HasAbnormalReturnItems(record.Items);

            string handoverDate = blankHandoverSignatures
                ? BlankDateText
                : FormatDate(record.HandoverDate ?? record.CompletedAt ?? record.ReturnDate);

            return new ArchiveReturnReceiptPrintData
            {
                DocumentTitle = documentTitle,
                ReturnNo = record.ReturnNo,
                SourceOutboundNo = record.SourceOutboundNo,
                ReturnDateText = returnDate,
                BorrowerDept = record.BorrowerDept,
                BorrowerName = record.BorrowerName ?? string.Empty,
                RegisteredByName = record.RegisteredByName ?? string.Empty,
                ExpectedReturnDateText = expectedReturnDate,
                MaterialSummary = string.IsNullOrWhiteSpace(materialSummary) ? "(无)" : materialSummary,
                ItemLines = ArchiveReturnItemDescription.BuildPrintDetailLines(record.Items, classificationByFilingFactId).ToList(),
                HandoverSignatureLines = BuildHandoverSignatureLines(record, blankHandoverSignatures, handoverDate),
                ApprovalSignatureLines = BuildReturnFormApprovalSignatureLines(
                    record,
                    outbound,
                    blankHandoverSignatures,
                    hasLossReturn),
                Remark = record.Remark?.Trim() ?? string.Empty,
                LossDescription = hasLossReturn ? record.LossDescription?.Trim() ?? string.Empty : string.Empty,
                HasLossReturn = hasLossReturn,
                PrintCount = record.PrintCount
            };
        }

        /// <summary>
        /// 签批交接单审核审批签字：正常归还仅部门负责人；灭失时含全部审核审批人。
        /// 表单左侧标签去掉「借出时」前缀，保证单行显示；说明栏仍提示为借出时签字人。
        /// 已审批后优先使用归还单录入值，否则回退出库单借出时签字。
        /// </summary>
        private static List<ArchiveReturnApprovalSignatureLine> BuildReturnFormApprovalSignatureLines(
            YearlyArchiveReturnRecord record,
            YearlyArchiveOutboundRecord? outbound,
            bool blankSignatures,
            bool hasLossReturn)
        {
            var allLines = BuildReturnApprovalLines(record, outbound, blankSignatures)
                .Select(ToReturnFormApprovalLine)
                .ToList();
            if (hasLossReturn)
            {
                return allLines;
            }

            // 正常归还：仅部门负责人。
            return allLines.Count > 0
                ? new List<ArchiveReturnApprovalSignatureLine> { allLines[0] }
                : new List<ArchiveReturnApprovalSignatureLine>
                {
                    CreateBlankApprovalLine("部门负责人")
                };
        }

        /// <summary>去掉「借出时」前缀，使签批交接单左侧标签可单行放下。</summary>
        private static ArchiveReturnApprovalSignatureLine ToReturnFormApprovalLine(
            ArchiveReturnApprovalSignatureLine line)
        {
            const string borrowTimePrefix = "借出时";
            string roleLabel = line.RoleLabel?.Trim() ?? string.Empty;
            if (roleLabel.StartsWith(borrowTimePrefix, StringComparison.Ordinal))
            {
                roleLabel = roleLabel[borrowTimePrefix.Length..].Trim();
            }

            return new ArchiveReturnApprovalSignatureLine
            {
                RoleLabel = roleLabel,
                SignerSlot = line.SignerSlot,
                DateText = line.DateText
            };
        }

        private static List<ArchiveReturnApprovalSignatureLine> BuildHandoverSignatureLines(
            YearlyArchiveReturnRecord record,
            bool blankSignatures,
            string handoverDate)
        {
            if (blankSignatures)
            {
                return
                [
                    new()
                    {
                        RoleLabel = "归还人签字：",
                        SignerSlot = string.Empty,
                        DateText = BlankDateText
                    },
                    new()
                    {
                        RoleLabel = "资料室资料管理员签字：",
                        SignerSlot = string.Empty,
                        DateText = BlankDateText
                    }
                ];
            }

            string applicant = !string.IsNullOrWhiteSpace(record.HandoverApplicant)
                ? record.HandoverApplicant.Trim()
                : record.BorrowerName?.Trim() ?? string.Empty;
            string admin = !string.IsNullOrWhiteSpace(record.HandoverAdmin)
                ? record.HandoverAdmin.Trim()
                : record.HandlerName?.Trim() ?? string.Empty;
            string adminDate = FormatDate(record.CompletedAt ?? record.HandoverDate);

            return
            [
                new()
                {
                    RoleLabel = "归还人签字：",
                    SignerSlot = applicant,
                    DateText = string.IsNullOrWhiteSpace(handoverDate) ? BlankDateText : handoverDate
                },
                new()
                {
                    RoleLabel = "资料室资料管理员签字：",
                    SignerSlot = admin,
                    DateText = string.IsNullOrWhiteSpace(adminDate) ? BlankDateText : adminDate
                }
            ];
        }

        private static string FormatDate(DateTime? value) =>
            value.HasValue ? value.Value.ToString("yyyy-MM-dd") : BlankDateText;

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
    }
}
