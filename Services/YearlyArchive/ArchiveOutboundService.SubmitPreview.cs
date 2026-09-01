using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 借出申请提交前校验与拟执行逻辑预览。
    /// </summary>
    public sealed partial class ArchiveOutboundService
    {
        public async Task<ArchiveOutboundSubmitPreviewResult> PreviewSubmitApplicationAsync(int recordId, User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            var record = await _outboundRepository.GetByIdWithDetailsAsync(recordId);
            if (record == null)
            {
                return new ArchiveOutboundSubmitPreviewResult(
                    ["未找到指定的出库申请单。"],
                    string.Empty);
            }

            if (!CanSubmitApplication(user))
            {
                return new ArchiveOutboundSubmitPreviewResult(
                    ["仅部门资料管理员可提交资料借出申请。"],
                    string.Empty);
            }

            if (record.ApplicantUserId != user.Id)
            {
                return new ArchiveOutboundSubmitPreviewResult(
                    ["仅申请人本人可提交该申请。"],
                    string.Empty);
            }

            if (record.Status != YearlyArchiveOutboundRecord.Unsubmitted)
            {
                return new ArchiveOutboundSubmitPreviewResult(
                    ["只有“未提交”状态的申请单才能提交。"],
                    string.Empty);
            }

            var errors = CollectSubmitValidationErrors(record, record.Items, requireSubmittedFields: true);
            errors.AddRange(await CollectSimulatedOutboundStockErrorsAsync(record));
            errors.AddRange(await CollectElectronicWithdrawalReservationErrorsAsync(record));
            errors.AddRange(await CollectElectronicWithdrawalErrorsAsync(record.Items));
            errors.AddRange(await CollectCopyDiskCapacityErrorsAsync(record.Items));

            if (errors.Count > 0)
            {
                return new ArchiveOutboundSubmitPreviewResult(
                    errors,
                    string.Empty);
            }

            string summary = ArchiveOutboundApplicationSubmitPreviewBuilder.Build(record);
            var depletionWarnings = await CollectSimulatedLongTermStockDepletionWarningsAsync(record);
            string depletionReminder = ArchiveSimulatedLongTermWithdrawalDepletionSupport
                .BuildApplicantReminderText(depletionWarnings);
            return new ArchiveOutboundSubmitPreviewResult(Array.Empty<string>(), summary, depletionReminder);
        }

        private async Task<List<string>> CollectElectronicWithdrawalErrorsAsync(
            IReadOnlyList<YearlyArchiveOutboundItem> items)
        {
            var errors = new List<string>();

            foreach (var group in ArchiveOutboundContainerUnitSupport.GroupItems(items))
            {
                var unitItems = group.ToList();
                var sample = unitItems[0];
                if (!string.Equals(sample.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal))
                {
                    continue;
                }

                if (sample.UsageMode != ArchiveOutboundDomainValues.UsageModeWithdrawal)
                {
                    continue;
                }

                string unitTitle = ArchiveOutboundContainerUnitSupport.FormatUnitTitle(sample.MediaKind, sample.ContainerCode);

                foreach (var item in unitItems)
                {
                    if (string.Equals(
                            item.SelectionScopeKind,
                            ArchiveSearchSelectionScopeKind.ContentEntry,
                            StringComparison.Ordinal))
                    {
                        string label = string.IsNullOrWhiteSpace(item.MaterialName) ? item.ItemName : item.MaterialName;
                        errors.Add($"• {unitTitle}：电子介质资料「{label}」为部分内容选取，不可使用提档方式（提档须提走整块物理介质）。");
                    }
                }

                string containerCode = sample.ContainerCode?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(containerCode))
                {
                    errors.Add($"• {unitTitle}：缺少盒/袋编号，无法校验电子介质提档完整性。");
                    continue;
                }

                var inArchiveFacts = await _outboundRepository.GetInArchiveFilingFactsByContainerAsync(
                    sample.MediaKind,
                    containerCode);

                if (inArchiveFacts.Count == 0)
                {
                    continue;
                }

                var selectedFactIds = unitItems
                    .Select(item => item.FilingFactId)
                    .Where(id => id > 0)
                    .ToHashSet();

                var missingFacts = inArchiveFacts
                    .Where(fact => !selectedFactIds.Contains(fact.Id))
                    .ToList();

                if (missingFacts.Count > 0)
                {
                    string missingLabels = string.Join(
                        "、",
                        missingFacts
                            .Select(fact => string.IsNullOrWhiteSpace(fact.MaterialName) ? fact.ItemName : fact.MaterialName)
                            .Where(label => !string.IsNullOrWhiteSpace(label))
                            .Take(5));

                    if (missingFacts.Count > 5)
                    {
                        missingLabels += $" 等共 {missingFacts.Count} 项";
                    }

                    errors.Add(
                        $"• {unitTitle}：提档方式须提走介质袋内全部在库资料，当前申请未包含：{missingLabels}。请补全资料或改用拷贝方式。");
                }
            }

            return errors;
        }
    }
}
