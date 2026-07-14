using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料归还回执打印数据装配。
    /// </summary>
    public sealed partial class ArchiveReturnService
    {
        private const string BlankDateText = "______年___月___日";

        public async Task<ArchiveReturnReceiptPrintData> BuildReceiptPrintDataAsync(int recordId, bool blankHandoverSignatures)
        {
            var record = await _returnRepository.GetByIdWithDetailsAsync(recordId)
                ?? throw new InvalidOperationException("未找到指定的归还单。");

            if (record.Status is YearlyArchiveReturnRecord.Draft or YearlyArchiveReturnRecord.Voided)
            {
                throw new InvalidOperationException("只有“已登记”或“已办结”状态的归还单可打印回执。");
            }

            var abnormalGate = await ValidateAbnormalReturnGateAsync(record);
            if (!abnormalGate.Success)
            {
                throw new InvalidOperationException(abnormalGate.Message);
            }

            var outbound = await _outboundRepository.GetByIdWithDetailsAsync(record.SourceOutboundRecordId);
            return BuildReceiptPrintData(record, outbound, blankHandoverSignatures);
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
            bool blankHandoverSignatures)
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
                : FormatDate(record.CompletedAt ?? record.ReturnDate);

            return new ArchiveReturnReceiptPrintData
            {
                ReturnNo = record.ReturnNo,
                SourceOutboundNo = record.SourceOutboundNo,
                ReturnDateText = returnDate,
                BorrowerDept = record.BorrowerDept,
                BorrowerName = record.BorrowerName ?? string.Empty,
                RegisteredByName = record.RegisteredByName ?? string.Empty,
                ExpectedReturnDateText = expectedReturnDate,
                MaterialSummary = string.IsNullOrWhiteSpace(materialSummary) ? "(无)" : materialSummary,
                ItemLines = ArchiveReturnItemDescription.BuildPrintDetailLines(record.Items).ToList(),
                HandoverSignatureBlock = blankHandoverSignatures
                    ? BuildBlankHandoverSignatureBlock()
                    : BuildFilledHandoverSignatureBlock(record, handoverDate),
                Remark = record.Remark?.Trim() ?? string.Empty,
                LossDescription = hasLossReturn ? record.LossDescription?.Trim() ?? string.Empty : string.Empty,
                HasLossReturn = hasLossReturn,
                PrintCount = record.PrintCount
            };
        }

        private static string BuildBlankHandoverSignatureBlock() =>
            "\n归还人（借出人）签字：                                            日期:______年___月___日\n" +
            "资料室资料员签字：                                 日期:______年___月___日";

        private static string BuildFilledHandoverSignatureBlock(YearlyArchiveReturnRecord record, string handoverDate)
        {
            string borrower = record.BorrowerName?.Trim() ?? string.Empty;
            string handler = record.HandlerName?.Trim() ?? string.Empty;
            string borrowerSlot = string.IsNullOrWhiteSpace(borrower) ? "________________" : borrower;
            string handlerSlot = string.IsNullOrWhiteSpace(handler) ? "________________" : handler;
            string borrowerDate = handoverDate;
            string handlerDate = FormatDate(record.CompletedAt);

            return $"\n归还人（借出人）签字：{borrowerSlot}    日期：{borrowerDate}\n" +
                   $"资料室资料员签字：{handlerSlot}    日期：{handlerDate}";
        }

        private static string FormatDate(DateTime? value) =>
            value.HasValue ? value.Value.ToString("yyyy-MM-dd") : BlankDateText;
    }
}
