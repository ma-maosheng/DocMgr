using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 复杂电子申请单提交后核对：登记单状态、介质明细、借出硬盘登记锁是否与操作台一致。
    /// </summary>
    internal static class ArchiveRegisterComplexElectronicPostSubmitVerifier
    {
        public static async Task<IReadOnlyList<string>> VerifyAsync(
            IArchiveRegisterRepository registerRepository,
            IArchiveRegisterService registerService,
            string formNo,
            IReadOnlyList<YearlyArchiveRegisterMedia> submittedMediaEntries,
            bool expectsBorrowedHardDiskLock)
        {
            ArgumentNullException.ThrowIfNull(registerRepository);
            ArgumentNullException.ThrowIfNull(registerService);
            ArgumentNullException.ThrowIfNull(submittedMediaEntries);

            var issues = new List<string>();
            var persisted = await registerService.GetByFormNoAsync(formNo);
            if (persisted == null)
            {
                issues.Add($"提交后未在库中找到登记单 [{formNo}]。");
                return issues;
            }

            if (persisted.Status != YearlyArchiveRegisterRecord.Submitted)
            {
                issues.Add($"登记单 [{formNo}] 状态应为“已提交”，实际为 {persisted.Status}。");
            }

            if (!persisted.ProjectId.HasValue || persisted.ProjectId.Value <= 0)
            {
                issues.Add($"登记单 [{formNo}] 未持久化所属项目 Id。");
            }

            int expectedElectronicCount = submittedMediaEntries.Count(entry =>
                string.Equals(entry.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase));
            int persistedElectronicCount = persisted.MediaEntries.Count(entry =>
                string.Equals(entry.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase));

            if (expectedElectronicCount != persistedElectronicCount)
            {
                issues.Add(
                    $"登记单 [{formNo}] 电子介质条数不一致：提交前 {expectedElectronicCount}，库内 {persistedElectronicCount}。");
            }

            if (expectsBorrowedHardDiskLock)
            {
                string? borrowedCode = submittedMediaEntries
                    .Where(entry => entry.IsBorrowedHardDisk && !string.IsNullOrWhiteSpace(entry.BorrowedHardDiskCode))
                    .Select(entry => entry.BorrowedHardDiskCode!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(borrowedCode))
                {
                    issues.Add($"登记单 [{formNo}] 标记为借出硬盘场景但未找到借出编号。");
                }
                else
                {
                    var lockedMedia = await registerRepository.GetHardDiskMediaByRegisterLockAsync(
                        persisted.Id,
                        persisted.FormNo,
                        onlyNotDeleted: true);
                    var medium = lockedMedia.FirstOrDefault(item =>
                        string.Equals(item.DiskCode, borrowedCode, StringComparison.OrdinalIgnoreCase));

                    if (medium == null)
                    {
                        issues.Add($"借出硬盘 [{borrowedCode}] 未建立资料登记占用锁（HardDiskRegisterLock）。");
                    }
                    else if (!HardDiskRegisterLock.IsArchiveRegister(medium.RegisterLock))
                    {
                        issues.Add($"借出硬盘 [{borrowedCode}] 未建立年度资料登记占用锁。");
                    }
                }
            }

            return issues;
        }
    }
}
