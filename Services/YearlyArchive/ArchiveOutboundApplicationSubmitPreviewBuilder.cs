using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 借出申请提交拟执行逻辑说明生成器。
    /// </summary>
    internal static class ArchiveOutboundApplicationSubmitPreviewBuilder
    {
        public static string Build(YearlyArchiveOutboundRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var lines = new List<string>
            {
                $"申请单号：{record.OutboundNo}",
                $"申请人：{record.ApplicantName}（{record.ApplicantDept}）",
                $"资料去向：{FormatDestination(record)}",
            };

            if (record.ExpectedReturnDate.HasValue)
            {
                lines.Add($"预计归还日期：{record.ExpectedReturnDate:yyyy-MM-dd}");
            }

            lines.Add(string.Empty);
            lines.Add("提交后将执行以下逻辑：");
            lines.Add("1. 申请单状态变更为「已提交」，进入审批流程。");
            lines.Add($"2. 审批截止日设为提交后 {ArchiveOutboundDomainValues.DefaultApprovalDeadlineDays} 个自然日。");

            int syncIndex = 3;
            foreach (var group in ArchiveOutboundContainerUnitSupport.GroupItems(record.Items))
            {
                var unitItems = group.ToList();
                var sample = unitItems[0];
                string unitTitle = ArchiveOutboundContainerUnitSupport.FormatUnitTitle(sample.MediaKind, sample.ContainerCode);
                lines.Add($"{syncIndex}. {unitTitle}（共 {unitItems.Count} 条资料）");

                string usageMode = sample.UsageMode;
                if (usageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal)
                {
                    bool isElectronic = string.Equals(
                        sample.MediaKind,
                        ArchiveRegisterDomainValues.MediaKindElectronic,
                        StringComparison.Ordinal);

                    if (isElectronic)
                    {
                        lines.Add("   · 领用方式：提档（电子介质原件提走出库）。");
                        lines.Add("   · 提示：任何方式出库的电子介质资料，其资料都将不再归还资料室。");
                        if (ArchiveOutboundDomainValues.IsHardDiskStorageCarrier(sample.StorageCarrierType))
                        {
                            string diskReturn = sample.NeedReturn ? "需归还" : "不需归还";
                            lines.Add($"   · 硬盘：{diskReturn}。");
                            if (sample.NeedReturn)
                            {
                                DateTime? dueDate = ArchiveOutboundReturnSupport.ResolveItemExpectedReturnDate(sample, record);
                                if (dueDate.HasValue)
                                {
                                    lines.Add($"   · 硬盘归还要求：请在 {dueDate:yyyy-MM-dd} 前归还硬盘。");
                                }
                            }
                        }
                    }
                    else
                    {
                        string returnText = sample.NeedReturn ? "需归还" : "不需归还";
                        lines.Add($"   · 领用方式：提档（提档资料{returnText}）。");
                        if (sample.NeedReturn)
                        {
                            DateTime? dueDate = ArchiveOutboundReturnSupport.ResolveItemExpectedReturnDate(sample, record);
                            if (dueDate.HasValue)
                            {
                                lines.Add($"   · 归还要求：请在 {dueDate:yyyy-MM-dd} 前归还提档资料。");
                            }
                        }
                    }

                    lines.Add("   · 提交同步：校验并创建提档预订（Active）；若资料已被其他在途申请占用或份数不足，将拒绝提交。");
                    lines.Add("   · 审批办结后办理实物出库；撤回或作废时释放预订。");
                }
                else if (usageMode == ArchiveOutboundDomainValues.UsageModeCopy)
                {
                    lines.Add("   · 领用方式：复制（档案盒原件留存库内）。");
                    if (string.Equals(
                            sample.MediaKind,
                            ArchiveRegisterDomainValues.MediaKindSimulated,
                            StringComparison.Ordinal))
                    {
                        lines.Add("   · 提示：拷贝方式出库的模拟介质资料，其资料不再归还资料室。");
                    }

                    lines.Add("   · 提交同步：创建复制待办记录（Pending），审批办结后交付复制件。");
                }
                else if (usageMode == ArchiveOutboundDomainValues.UsageModeDuplicate)
                {
                    lines.Add("   · 领用方式：拷贝（电子介质袋原件留存库内）。");
                    lines.Add("   · 提示：任何方式出库的电子介质资料，其资料都将不再归还资料室。");
                    lines.Add("   · 提交同步：创建拷贝待办记录（Pending），审批办结后办理资料拷贝。");

                    if (string.Equals(
                            sample.ElectronicMediaSource,
                            ArchiveOutboundDomainValues.ElectronicMediaSourceInStockBlank,
                            StringComparison.Ordinal)
                        && !string.IsNullOrWhiteSpace(sample.RequisitionedDiskCode))
                    {
                        string diskReturn = sample.RequisitionedDiskNeedReturn ? "需归还" : "不需归还";
                        lines.Add($"   · 库内空盘：征用硬盘 [{sample.RequisitionedDiskCode.Trim()}]（{diskReturn}）。");
                        if (sample.RequisitionedDiskNeedReturn)
                        {
                            DateTime? dueDate = ArchiveOutboundReturnSupport.ResolveItemExpectedReturnDate(sample, record);
                            if (dueDate.HasValue)
                            {
                                lines.Add($"   · 硬盘归还要求：请在 {dueDate:yyyy-MM-dd} 前归还。");
                            }
                        }

                        lines.Add("   · 提交同步：锁定该硬盘为出库征用状态，办结或撤回后释放。");
                    }
                    else
                    {
                        string medium = ArchiveOutboundDomainValues.GetDuplicateMediumDisplay(sample.ElectronicMediumType);
                        lines.Add($"   · 拷贝目标：{medium}（自备介质，不使用库内空盘）。");
                    }
                }

                syncIndex++;
            }

            lines.Add(string.Empty);
            lines.Add("请核对以上逻辑无误后确认提交。提交后请打印申请单并等待审批。");
            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatDestination(YearlyArchiveOutboundRecord record)
        {
            if (ArchiveOutboundDomainValues.IsExternalDestination(record.DestinationKind))
            {
                string unit = record.ExternalUnit?.Trim() ?? string.Empty;
                return string.IsNullOrWhiteSpace(unit)
                    ? "外部（单位）"
                    : $"外部（单位）：{unit}";
            }

            return "本部门（内部）";
        }
    }
}
