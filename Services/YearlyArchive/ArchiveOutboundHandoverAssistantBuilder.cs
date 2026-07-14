using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    internal static class ArchiveOutboundHandoverAssistantBuilder
    {
        public static IReadOnlyList<ArchiveOutboundHandoverAssistantCheckItem> Build(YearlyArchiveOutboundRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var items = new List<ArchiveOutboundHandoverAssistantCheckItem>();
            items.AddRange(BuildGeneralItems());
            items.AddRange(BuildRecordSpecificItems(record));
            items.AddRange(BuildClosingItems());

            return items;
        }

        private static IEnumerable<ArchiveOutboundHandoverAssistantCheckItem> BuildGeneralItems()
        {
            yield return Item("办理前核对", "已核对申请单号、领用人、申请部门与本次实物交接信息一致。");
            yield return Item("办理前核对", "已逐项核对资料明细与待出库实物，名称、份数、领用方式与申请单一致。");
            yield return Item("档案盒与介质袋", "提档出库：已从柜内正确位置取出对应档案盒/介质袋，盒/袋编号与系统记录一致。");
            yield return Item("档案盒与介质袋", "复制出库：档案盒/介质袋原件仍留存于库内，仅交付复制件，未误取原件。");
            yield return Item("档案盒与介质袋", "出库后若盒/袋内已无剩余资料，已确认空盒/空袋的后续处置（留库、合并或注销）。");
            yield return Item("电子介质与硬盘", "拷贝出库：已核对目标硬盘编号与申请单一致，拷贝内容完整可读。");
            yield return Item("电子介质与硬盘", "库内空盘征用：已确认硬盘为“库内空盘”状态，交接时当面交予领用人并登记编号。");
            yield return Item("电子介质与硬盘", "需归还硬盘/介质：已向领用人说明归还时限、完好性及数据安全要求。");
            yield return Item("涉密与特殊事项", "涉密资料已按密级管理要求履行当面交接与登记，未发生违规带出。");
        }

        private static IEnumerable<ArchiveOutboundHandoverAssistantCheckItem> BuildRecordSpecificItems(YearlyArchiveOutboundRecord record)
        {
            var orderedItems = record.Items.OrderBy(i => i.SortOrder).ToList();
            if (orderedItems.Count == 0)
            {
                yield return Item("本单资料明细", "本申请单暂无资料明细，请确认是否应继续办理出库。");
                yield break;
            }

            int withdrawalCount = orderedItems.Count(i => i.UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal);
            int copyCount = orderedItems.Count(i => i.UsageMode == ArchiveOutboundDomainValues.UsageModeCopy);
            int duplicateCount = orderedItems.Count(i => i.UsageMode == ArchiveOutboundDomainValues.UsageModeDuplicate);

            yield return Item(
                "本单资料明细",
                $"本单共 {orderedItems.Count} 条明细：提档 {withdrawalCount} 项、复制 {copyCount} 项、拷贝 {duplicateCount} 项，已分别按领用方式办理。");

            if (ArchiveOutboundDomainValues.IsExternalDestination(record.DestinationKind))
            {
                string proof = record.ProofMaterialNote?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(proof)
                    && !string.Equals(proof, ArchiveOutboundDomainValues.ProofMaterialNoneText, StringComparison.Ordinal))
                {
                    yield return Item("涉密与特殊事项", $"外部去向：已核对证明材料「{proof}」及相关审批要求。");
                }
            }

            foreach (var (item, index) in orderedItems.Select((entry, idx) => (entry, idx + 1)))
            {
                string detail = ArchiveOutboundItemDescription.BuildSinglePrintDetailLine(item, index);
                yield return Item("本单资料明细", $"已办理：{detail}");
            }

            var containerCodes = orderedItems
                .Select(i => i.ContainerCode?.Trim() ?? string.Empty)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (containerCodes.Count > 0)
            {
                yield return Item(
                    "档案盒与介质袋",
                    $"涉及盒/袋号：{string.Join("、", containerCodes)}，已全部核对并正确出库。");
            }

            var diskCodes = orderedItems
                .Select(i => i.RequisitionedDiskCode?.Trim() ?? string.Empty)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (diskCodes.Count > 0)
            {
                yield return Item(
                    "电子介质与硬盘",
                    $"涉及征用/拷贝硬盘编号：{string.Join("、", diskCodes)}，已核对状态与交接记录。");
            }

            if (orderedItems.Any(i => i.NeedReturn))
            {
                yield return Item("档案盒与介质袋", "本单含需归还的提档资料，已向领用人说明归还要求并记录预计归还日期。");
            }
        }

        private static IEnumerable<ArchiveOutboundHandoverAssistantCheckItem> BuildClosingItems()
        {
            yield return Item("交接与系统归档", "实物清点完成后，领用人与资料室资料员已在交接单上签字。");
            yield return Item("交接与系统归档", "已上传签字后的交接单扫描件。");
            yield return Item("交接与系统归档", "已上传资料实物照片。");
            yield return Item("交接与系统归档", "以上事项确认无误后，再执行“资料出库办结”同步系统台账与立档状态。");
        }

        private static ArchiveOutboundHandoverAssistantCheckItem Item(string category, string text) =>
            new() { Category = category, Text = text };
    }
}
