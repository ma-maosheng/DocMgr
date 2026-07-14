using DocMgr.Models.YearlyArchive;



namespace DocMgr.Services.YearlyArchive

{

    /// <summary>

    /// 资料灭失情况表打印数据装配。

    /// </summary>

    public sealed partial class ArchiveReturnService

    {

        private const string BlankApprovalDateText = "______年___月___日";



        public async Task<ArchiveReturnAbnormalReportPrintData> BuildAbnormalReportPrintDataAsync(

            int recordId,

            bool blankApprovalSignatures)

        {

            var record = await _returnRepository.GetByIdWithDetailsAsync(recordId)

                ?? throw new InvalidOperationException("未找到指定的归还单。");



            if (record.Status is YearlyArchiveReturnRecord.Completed or YearlyArchiveReturnRecord.Voided)

            {

                throw new InvalidOperationException("已办结或已作废的归还单不可打印灭失情况表。");

            }



            if (!ArchiveReturnDomainValues.HasAbnormalReturnItems(record.Items))

            {

                throw new InvalidOperationException("当前归还单无灭失份数，无需打印灭失情况表。");

            }



            var outbound = await _outboundRepository.GetByIdWithDetailsAsync(record.SourceOutboundRecordId);

            ArchiveReturnItemDisplaySupport.EnrichFromOutbound(record, outbound);



            return BuildAbnormalReportPrintData(record, outbound, blankApprovalSignatures);

        }



        private static ArchiveReturnAbnormalReportPrintData BuildAbnormalReportPrintData(

            YearlyArchiveReturnRecord record,

            YearlyArchiveOutboundRecord? outbound,

            bool blankApprovalSignatures)

        {

            string returnDate = record.ReturnDate == default

                ? string.Empty

                : record.ReturnDate.ToString("yyyy-MM-dd");



            string materialSummary = ArchiveReturnItemDescription.BuildMaterialSummary(record.Items);



            return new ArchiveReturnAbnormalReportPrintData

            {

                ReturnNo = record.ReturnNo,

                SourceOutboundNo = record.SourceOutboundNo,

                ReturnDateText = returnDate,

                BorrowerDept = record.BorrowerDept,

                BorrowerName = record.BorrowerName ?? string.Empty,

                MaterialSummary = string.IsNullOrWhiteSpace(materialSummary) ? "(无)" : materialSummary,

                BorrowItemLines = ArchiveReturnItemDescription.BuildBorrowPrintDetailLines(record.Items).ToList(),

                IntactReturnItemLines = ArchiveReturnItemDescription.BuildIntactReturnPrintDetailLines(record.Items).ToList(),

                LossItemLines = ArchiveReturnItemDescription.BuildLossPrintDetailLines(record.Items).ToList(),

                BlankReturnerSignature = blankApprovalSignatures,

                ReturnerSignatureDateText = blankApprovalSignatures

                    ? BlankApprovalDateText

                    : FormatDate(record.RegisteredAt ?? record.ReturnDate),

                OutboundApprovalLines = BuildOutboundApprovalLines(outbound, blankApprovalSignatures)

            };

        }



        private static List<ArchiveReturnApprovalSignatureLine> BuildOutboundApprovalLines(

            YearlyArchiveOutboundRecord? outbound,

            bool blankApprovalSignatures)

        {

            if (blankApprovalSignatures || outbound == null)

            {

                return

                [

                    CreateBlankApprovalLine("部门审核人"),

                    CreateBlankApprovalLine("资料室负责人"),

                    CreateBlankApprovalLine("生产管理科负责人"),

                    CreateBlankApprovalLine("分管领导")

                ];

            }



            return

            [

                CreateFilledApprovalLine("部门审核人", outbound.DeptAuditor, outbound.DeptAuditDate),

                CreateFilledApprovalLine("资料室负责人", outbound.ArchiveRoomHead, outbound.ArchiveRoomHeadDate),

                CreateFilledApprovalLine("生产管理科负责人", outbound.ProductionHead, outbound.ProductionHeadDate),

                CreateFilledApprovalLine("分管领导", outbound.VicePresident, outbound.VicePresidentDate)

            ];

        }



        private static ArchiveReturnApprovalSignatureLine CreateBlankApprovalLine(string roleLabel) =>

            new()

            {

                RoleLabel = roleLabel,

                SignerSlot = string.Empty,

                DateText = BlankApprovalDateText

            };



        private static ArchiveReturnApprovalSignatureLine CreateFilledApprovalLine(

            string roleLabel,

            string? signer,

            DateTime? date) =>

            new()

            {

                RoleLabel = roleLabel,

                SignerSlot = signer?.Trim() ?? string.Empty,

                DateText = FormatDate(date)

            };

    }

}

