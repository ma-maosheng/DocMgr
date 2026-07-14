using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 立档测试提交后核对硬盘台账/归还登记是否与操作台业务一致。
    /// </summary>
    internal static class ArchiveFilingCursorTestHardDiskSyncVerifier
    {
        public static async Task<IReadOnlyList<string>> VerifyAsync(
            IArchiveFilingRepository filingRepository,
            ElectronicArchiveSubmissionRequest request,
            ElectronicArchiveSubmissionResult result,
            YearlyArchiveRegisterMedia mediaEntry)
        {
            ArgumentNullException.ThrowIfNull(filingRepository);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(mediaEntry);

            var issues = new List<string>();
            string reportText = result.DatabaseChanges?.ToDisplayText() ?? string.Empty;

            switch (request.SubmissionMode)
            {
                case ElectronicArchiveSubmissionMode.CopyNewOpticalDisc:
                case ElectronicArchiveSubmissionMode.RetainedOpticalDiscSingleNew:
                case ElectronicArchiveSubmissionMode.RetainedHardDiskCopyToOpticalDisc:
                    if (ContainsHardDiskLedgerChange(reportText))
                    {
                        issues.Add("拷贝/光盘入袋场景不应变更物理硬盘台账，但提交明细中出现硬盘台账写入。");
                    }

                    break;

                case ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew:
                    await VerifyRetainedHardDiskDirectNewAsync(
                        filingRepository,
                        request,
                        reportText,
                        mediaEntry,
                        issues);
                    break;

                case ElectronicArchiveSubmissionMode.CopyNewHardDisk:
                    await VerifyCopyNewHardDiskAsync(filingRepository, request, reportText, issues);
                    break;

                default:
                    issues.Add($"未配置硬盘同步校验规则：{request.SubmissionMode}");
                    break;
            }

            if (reportText.Contains("跳过自动归还登记", StringComparison.Ordinal)
                || reportText.Contains("本次未同步", StringComparison.Ordinal))
            {
                issues.Add($"提交明细提示硬盘侧未同步：{ExtractDeferredReason(reportText)}");
            }

            return issues;
        }

        private static async Task VerifyRetainedHardDiskDirectNewAsync(
            IArchiveFilingRepository filingRepository,
            ElectronicArchiveSubmissionRequest request,
            string reportText,
            YearlyArchiveRegisterMedia mediaEntry,
            List<string> issues)
        {
            string? diskCode = request.BorrowedHardDiskCandidate?.DiskCode?.Trim()
                ?? mediaEntry.BorrowedHardDiskCode?.Trim()
                ?? request.ArchiveUnit.LinkedMediumCodes?.Trim();

            if (string.IsNullOrWhiteSpace(diskCode))
            {
                diskCode = request.PendingExternalHardDisk?.DiskCode?.Trim();
            }

            if (string.IsNullOrWhiteSpace(diskCode))
            {
                issues.Add("硬盘留存直接入袋场景未解析到关联硬盘编号。");
                return;
            }

            var medium = await filingRepository.GetHardDiskMediumByDiskCodeWithLedgerAsync(diskCode);
            if (medium?.Ledger == null)
            {
                issues.Add($"提交后未在库中找到硬盘 [{diskCode}] 或其台账。");
                return;
            }

            if (request.BorrowedHardDiskCandidate != null)
            {
                bool returnRecorded = reportText.Contains("自动办理归还登记", StringComparison.Ordinal)
                    || reportText.Contains("归还登记(资料)", StringComparison.Ordinal);

                if (!returnRecorded)
                {
                    issues.Add($"借出留存硬盘 [{diskCode}] 立档后应写入归还登记(资料)，提交明细中未发现。");
                }

                if (medium.RegisterLock != null)
                {
                    issues.Add($"借出留存硬盘 [{diskCode}] 立档后仍占用 HardDiskRegisterLock，与操作台预期不符。");
                }

                if (!string.Equals(medium.Ledger.MediaStatus, HardDiskMedium.StatusInStockData, StringComparison.Ordinal))
                {
                    issues.Add(
                        $"借出留存硬盘 [{diskCode}] 台账状态应为 [{HardDiskMedium.StatusInStockData}]，实际为 [{medium.Ledger.MediaStatus}]。");
                }
            }
            else if (request.PendingExternalHardDisk != null)
            {
                if (!reportText.Contains("外来硬盘登记入账", StringComparison.Ordinal))
                {
                    issues.Add($"外来留存硬盘 [{diskCode}] 立档后应写入 HardDiskMedium/Ledger，提交明细中未发现。");
                }

                if (!string.Equals(medium.Ledger.MediaStatus, HardDiskMedium.StatusInStockData, StringComparison.Ordinal))
                {
                    issues.Add(
                        $"外来留存硬盘 [{diskCode}] 台账状态应为 [{HardDiskMedium.StatusInStockData}]，实际为 [{medium.Ledger.MediaStatus}]。");
                }
            }
            else if (!ContainsHardDiskLedgerChange(reportText))
            {
                issues.Add($"硬盘留存直接入袋 [{diskCode}] 应在提交明细中出现硬盘台账变更。");
            }
        }

        private static async Task VerifyCopyNewHardDiskAsync(
            IArchiveFilingRepository filingRepository,
            ElectronicArchiveSubmissionRequest request,
            string reportText,
            List<string> issues)
        {
            string diskCode = request.ArchiveUnit.LinkedMediumCodes?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(diskCode))
            {
                issues.Add("拷贝型新建硬盘袋场景缺少关联硬盘编号。");
                return;
            }

            if (!ContainsHardDiskLedgerChange(reportText))
            {
                issues.Add($"拷贝型立档关联硬盘 [{diskCode}] 应在提交明细中出现硬盘台账变更。");
            }

            var medium = await filingRepository.GetHardDiskMediumByDiskCodeWithLedgerAsync(diskCode);
            if (medium?.Ledger == null)
            {
                issues.Add($"提交后未在库中找到拷贝目标硬盘 [{diskCode}]。");
                return;
            }

            if (!string.Equals(medium.Ledger.MediaStatus, HardDiskMedium.StatusInStockData, StringComparison.Ordinal))
            {
                issues.Add(
                    $"拷贝目标硬盘 [{diskCode}] 台账状态应为 [{HardDiskMedium.StatusInStockData}]，实际为 [{medium.Ledger.MediaStatus}]。");
            }
        }

        private static bool ContainsHardDiskLedgerChange(string reportText)
            => reportText.Contains("硬盘台账（HardDiskMedium / HardDiskLedger）", StringComparison.Ordinal);

        private static string ExtractDeferredReason(string reportText)
        {
            var line = reportText
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(text => text.Contains("跳过", StringComparison.Ordinal)
                    || text.Contains("未同步", StringComparison.Ordinal)
                    || text.Contains("留待", StringComparison.Ordinal));

            return string.IsNullOrWhiteSpace(line) ? "见数据库变更明细" : line.Trim();
        }
    }
}
