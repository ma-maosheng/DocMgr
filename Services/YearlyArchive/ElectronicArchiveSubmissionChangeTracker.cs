using DocMgr.Models.YearlyArchive;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 收集单次电子介质立档提交过程中的数据库写入明细。
    /// </summary>
    internal sealed class ElectronicArchiveSubmissionChangeTracker
    {
        private readonly List<string> _lines = new();
        private string? _currentSection;

        public void BeginSection(string sectionTitle)
        {
            if (string.IsNullOrWhiteSpace(sectionTitle))
            {
                return;
            }

            string title = sectionTitle.Trim();
            if (string.Equals(_currentSection, title, StringComparison.Ordinal))
            {
                return;
            }

            _currentSection = title;
            _lines.Add($"【{title}】");
        }

        public void AddLine(string detail)
        {
            if (string.IsNullOrWhiteSpace(detail))
            {
                return;
            }

            _lines.Add($"  • {detail.Trim()}");
        }

        public void AddLedgerChange(
            string diskCode,
            string? beforeStatus,
            string? afterStatus,
            string? beforeLocation,
            string? afterLocation,
            string? beforeNature = null,
            string? afterNature = null,
            string? extra = null)
        {
            var parts = new List<string> { $"硬盘 [{diskCode}]" };

            if (!string.Equals(beforeStatus, afterStatus, StringComparison.Ordinal))
            {
                parts.Add($"状态：{EmptyAsDash(beforeStatus)} → {EmptyAsDash(afterStatus)}");
            }

            if (!string.Equals(beforeNature, afterNature, StringComparison.Ordinal))
            {
                parts.Add($"介质属性：{EmptyAsDash(beforeNature)} → {EmptyAsDash(afterNature)}");
            }

            if (!string.Equals(beforeLocation, afterLocation, StringComparison.Ordinal))
            {
                parts.Add($"存放位置：{EmptyAsDash(beforeLocation)} → {EmptyAsDash(afterLocation)}");
            }

            if (!string.IsNullOrWhiteSpace(extra))
            {
                parts.Add(extra.Trim());
            }

            BeginSection("硬盘台账（HardDiskMedium / HardDiskLedger）");
            AddLine(string.Join("；", parts));
        }

        public void AddApplication(string applicationNo, string applicationType, string diskCode, string summary)
        {
            BeginSection("硬盘流转申请（HardDiskMediaApplication）");
            AddLine($"[{applicationNo}] {applicationType} / 硬盘 [{diskCode}]：{summary}");
        }

        public void AddTransaction(string diskCode, string description)
        {
            BeginSection("硬盘流转流水（HardDiskMediaTransaction）");
            AddLine($"硬盘 [{diskCode}]：{description}");
        }

        public void AddDeferred(string reason)
        {
            BeginSection("本次未同步（留待后续立档触发）");
            AddLine(reason);
        }

        public ElectronicArchiveDatabaseChangeReport BuildReport()
        {
            return new ElectronicArchiveDatabaseChangeReport(_lines.ToArray());
        }

        private static string EmptyAsDash(string? value)
            => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
    }
}
