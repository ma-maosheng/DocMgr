using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 电子介质立档提交后数据库变更明细，供界面展示核对。
    /// </summary>
    public sealed class ElectronicArchiveDatabaseChangeReport
    {
        public ElectronicArchiveDatabaseChangeReport(IReadOnlyList<string> lines)
        {
            Lines = lines ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> Lines { get; }

        public bool HasChanges => Lines.Count > 0;

        public string ToDisplayText()
        {
            if (Lines.Count == 0)
            {
                return "本次提交未记录到可展示的明细变更。";
            }

            return string.Join(Environment.NewLine, Lines);
        }

        public string ToDialogText(string summaryHeader)
            => ToDialogText(summaryHeader, "—— 数据库变更明细 ——");

        public string ToPreviewDialogText(string summaryHeader)
            => ToDialogText(summaryHeader, "—— 拟执行数据库变更明细 ——");

        private string ToDialogText(string summaryHeader, string detailSectionTitle)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(summaryHeader))
            {
                builder.AppendLine(summaryHeader.Trim());
                builder.AppendLine();
            }

            builder.AppendLine(detailSectionTitle);
            builder.AppendLine(ToDisplayText());
            return builder.ToString().TrimEnd();
        }
    }
}
