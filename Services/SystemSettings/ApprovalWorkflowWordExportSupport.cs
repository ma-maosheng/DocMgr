using System.IO;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.Shared;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.XWPF.UserModel;

namespace DocMgr.Services.SystemSettings
{
    /// <summary>
    /// 把当前审核审批规则导出为领导可直接阅读的 Word 说明。
    /// 只使用业务中文名、适用情形和签字职务，不输出字段编码。
    /// </summary>
    public static class ApprovalWorkflowWordExportSupport
    {
        private const string FontFamily = "宋体";
        private const double TitlePoints = 18;
        private const double HeadingPoints = 14;
        private const double BodyPoints = 12;
        private const double TablePoints = 10.5;

        /// <summary>写入 docx。规则按业务目录顺序、同一业务内按优先顺序排列。</summary>
        public static void ExportToFile(
            string filePath,
            IReadOnlyList<ApprovalWorkflowRule> rules,
            IReadOnlyList<User> users,
            DateTime exportedAt)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentNullException.ThrowIfNull(rules);
            ArgumentNullException.ThrowIfNull(users);

            var document = new XWPFDocument();
            ConfigurePage(document);
            AddTitle(document, exportedAt);
            AddReadingGuide(document);

            var catalog = ApprovalWorkflowCatalog.BusinessTypes;
            var rulesByBusiness = rules
                .GroupBy(rule => rule.BusinessType?.Trim() ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

            for (int index = 0; index < catalog.Count; index++)
            {
                ApprovalBusinessTypeDefinition business = catalog[index];
                rulesByBusiness.TryGetValue(business.BusinessType, out List<ApprovalWorkflowRule>? businessRules);
                AddBusinessSection(document, index + 1, business, businessRules ?? [], users);
            }

            AddClosing(document);

            using var stream = File.Create(filePath);
            document.Write(stream);
        }

        private static void ConfigurePage(XWPFDocument document)
        {
            var body = document.Document.body;
            var sectPr = body.sectPr ?? body.AddNewSectPr();
            var pgSz = sectPr.pgSz ?? sectPr.AddNewPgSz();
            pgSz.w = (ulong)PrintPageLayoutSupport.PageWidthTwips;
            pgSz.h = (ulong)PrintPageLayoutSupport.PageHeightTwips;
            sectPr.pgMar ??= new CT_PageMar();
            sectPr.pgMar.top = (ulong)PrintPageLayoutSupport.MarginVerticalTwips;
            sectPr.pgMar.bottom = (ulong)PrintPageLayoutSupport.MarginVerticalTwips;
            sectPr.pgMar.left = (ulong)PrintPageLayoutSupport.MarginHorizontalTwips;
            sectPr.pgMar.right = (ulong)PrintPageLayoutSupport.MarginHorizontalTwips;
        }

        private static void AddTitle(XWPFDocument document, DateTime exportedAt)
        {
            AddParagraph(document, "审核审批规则说明", TitlePoints, bold: true, ParagraphAlignment.CENTER, spacingAfterLines: "80");
            AddParagraph(
                document,
                $"资料室管理系统　　导出时间：{exportedAt:yyyy年M月d日 HH:mm}",
                BodyPoints,
                bold: false,
                ParagraphAlignment.CENTER,
                spacingAfterLines: "120");
        }

        private static void AddReadingGuide(XWPFDocument document)
        {
            AddParagraph(document, "怎么看这份说明", HeadingPoints, bold: true, ParagraphAlignment.LEFT, spacingAfterLines: "40");
            AddParagraph(document, "这份说明按系统里当前保存的规则整理，说明每一类业务在什么情况下、需要哪些人签字。", BodyPoints, false, ParagraphAlignment.LEFT);
            AddParagraph(document, "同一类业务如果有多条规则，按“优先顺序”数字从小到大查找。先遇到的、正在启用并且情形相符的那一条生效。", BodyPoints, false, ParagraphAlignment.LEFT);
            AddParagraph(document, "适用情形写“其余情况都用这条”的，是这类业务没有被前面规则覆盖时的兜底，一般排在最后。", BodyPoints, false, ParagraphAlignment.LEFT);
            AddParagraph(document, "签字分两档。审核：部门审核、资料室负责人、生产科负责人。审批：分管资料院长签字、分管生产院长签字。表里写“不需要”的那一档，这笔业务不用签。", BodyPoints, false, ParagraphAlignment.LEFT);
            AddParagraph(document, "括号里是系统预先填上的默认签字人，办理时仍可以改。写“按申请人部门查找”或“按职务查找”的，表示没有指定到具体人。标成“已停用”的规则保留在表里，办理时不采用。", BodyPoints, false, ParagraphAlignment.LEFT, spacingAfterLines: "160");
        }

        private static void AddBusinessSection(
            XWPFDocument document,
            int sectionNo,
            ApprovalBusinessTypeDefinition business,
            IReadOnlyList<ApprovalWorkflowRule> rules,
            IReadOnlyList<User> users)
        {
            AddParagraph(document, $"{sectionNo}. {business.DisplayName}", HeadingPoints, bold: true, ParagraphAlignment.LEFT, spacingBeforeLines: "80", spacingAfterLines: "60");

            if (rules.Count == 0)
            {
                AddParagraph(document, "当前没有配置规则。办理时不会自动带出签字人。", BodyPoints, false, ParagraphAlignment.LEFT, spacingAfterLines: "80");
                return;
            }

            var ordered = rules
                .OrderBy(rule => rule.Priority)
                .ThenBy(rule => rule.Id)
                .ToList();

            int[] widths = WordExportTableLayoutSupport.DistributeWidths(
                PrintPageLayoutSupport.ContentWidthTwips,
                [1.0, 1.7, 2.5, 2.3, 2.3, 1.0]);

            var table = document.CreateTable(ordered.Count + 1, widths.Length);
            WordExportTableLayoutSupport.ApplyFixedTableLayout(table, widths);
            ApplyTableBorders(table);

            FillCell(table.GetRow(0).GetCell(0), ["优先顺序"], TablePoints, bold: true, center: true, header: true);
            FillCell(table.GetRow(0).GetCell(1), ["规则名称"], TablePoints, bold: true, center: true, header: true);
            FillCell(table.GetRow(0).GetCell(2), ["什么情况下用这条"], TablePoints, bold: true, center: true, header: true);
            FillCell(table.GetRow(0).GetCell(3), ["审核要签"], TablePoints, bold: true, center: true, header: true);
            FillCell(table.GetRow(0).GetCell(4), ["审批要签"], TablePoints, bold: true, center: true, header: true);
            FillCell(table.GetRow(0).GetCell(5), ["状态"], TablePoints, bold: true, center: true, header: true);
            WordExportTableLayoutSupport.ApplyRowCellWidths(table.GetRow(0), widths);

            for (int i = 0; i < ordered.Count; i++)
            {
                ApprovalWorkflowRule rule = ordered[i];
                XWPFTableRow row = table.GetRow(i + 1);
                FillCell(row.GetCell(0), [rule.Priority.ToString()], TablePoints, bold: false, center: true, header: false);
                FillCell(row.GetCell(1), [string.IsNullOrWhiteSpace(rule.RuleName) ? "（未命名）" : rule.RuleName.Trim()], TablePoints, bold: false, center: false, header: false);
                FillCell(row.GetCell(2), [DescribeSituation(rule, business)], TablePoints, bold: false, center: false, header: false);
                FillCell(row.GetCell(3), DescribeSigners(rule, users, reviewLevel: true), TablePoints, bold: false, center: false, header: false);
                FillCell(row.GetCell(4), DescribeSigners(rule, users, reviewLevel: false), TablePoints, bold: false, center: false, header: false);
                FillCell(row.GetCell(5), [rule.IsEnabled ? "启用" : "已停用"], TablePoints, bold: false, center: true, header: false);
                WordExportTableLayoutSupport.ApplyRowCellWidths(row, widths);
            }
        }

        private static void AddClosing(XWPFDocument document)
        {
            AddParagraph(
                document,
                "本文只说明当前的签字要求，具体怎么办理仍以各项业务页面为准。",
                BodyPoints,
                bold: false,
                ParagraphAlignment.LEFT,
                spacingBeforeLines: "160");
        }

        private static string DescribeSituation(ApprovalWorkflowRule rule, ApprovalBusinessTypeDefinition business)
        {
            var parts = new List<string>();
            AppendSituation(parts, business, rule.Condition1FieldKey, rule.Condition1Value);
            AppendSituation(parts, business, rule.Condition2FieldKey, rule.Condition2Value);
            if (parts.Count == 0)
            {
                return "其余情况都用这条";
            }

            if (parts.Count == 1)
            {
                return parts[0];
            }

            bool or = string.Equals(rule.ConditionLogic?.Trim(), ApprovalWorkflowDomainValues.ConditionLogicOr, StringComparison.Ordinal);
            return or
                ? $"满足以下任一情形：{parts[0]}；或者{parts[1]}"
                : $"同时满足：{parts[0]}，并且{parts[1]}";
        }

        private static void AppendSituation(
            List<string> target,
            ApprovalBusinessTypeDefinition business,
            string? fieldKey,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(fieldKey) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string key = fieldKey.Trim();
            string rawValue = value.Trim();
            ApprovalConditionFieldDefinition? field = business.ConditionFields.FirstOrDefault(item =>
                string.Equals(item.FieldKey, key, StringComparison.Ordinal));
            string fieldName = field?.DisplayName ?? key;
            string shownValue = rawValue;
            if (field != null)
            {
                (string Value, string Label) option = field.Options.FirstOrDefault(item =>
                    string.Equals(item.Value, rawValue, StringComparison.Ordinal));
                if (!string.IsNullOrWhiteSpace(option.Label))
                {
                    shownValue = option.Label.Trim();
                }
            }

            target.Add($"{fieldName}为「{shownValue}」");
        }

        private static IReadOnlyList<string> DescribeSigners(ApprovalWorkflowRule rule, IReadOnlyList<User> users, bool reviewLevel)
        {
            var lines = new List<string>();
            if (reviewLevel)
            {
                AddSigner(lines, rule.EnableDeptHead, ApprovalWorkflowDomainValues.DisplayDeptHead, rule.DefaultDeptHeadUserId, users, "按申请人部门查找");
                AddSigner(lines, rule.EnableArchiveRoomHead, ApprovalWorkflowDomainValues.DisplayArchiveRoomHead, rule.DefaultArchiveRoomHeadUserId, users, "按职务查找");
                AddSigner(lines, rule.EnableProductionHead, ApprovalWorkflowDomainValues.DisplayProductionHead, rule.DefaultProductionHeadUserId, users, "按职务查找");
            }
            else
            {
                AddSigner(lines, rule.EnableArchiveDeputyPresident, ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident, rule.DefaultArchiveDeputyPresidentUserId, users, "按职务查找");
                AddSigner(lines, rule.EnableProductionVicePresident, ApprovalWorkflowDomainValues.DisplayProductionVicePresident, rule.DefaultProductionVicePresidentUserId, users, "按职务查找");
            }

            if (lines.Count == 0)
            {
                lines.Add("不需要");
            }

            return lines;
        }

        private static void AddSigner(
            List<string> lines,
            bool enabled,
            string title,
            int? userId,
            IReadOnlyList<User> users,
            string fallback)
        {
            if (!enabled)
            {
                return;
            }

            string person = fallback;
            if (userId is > 0)
            {
                string? realName = users.FirstOrDefault(user => user.Id == userId.Value)?.RealName?.Trim();
                person = string.IsNullOrWhiteSpace(realName) ? "按职务查找" : realName;
            }

            lines.Add($"{title}（{person}）");
        }

        private static void AddParagraph(
            XWPFDocument document,
            string text,
            double fontPoints,
            bool bold,
            ParagraphAlignment alignment,
            string spacingBeforeLines = "0",
            string spacingAfterLines = "40")
        {
            var paragraph = document.CreateParagraph();
            paragraph.Alignment = alignment;
            SetLineSpacing(paragraph, spacingBeforeLines, spacingAfterLines);
            var run = paragraph.CreateRun();
            run.SetText(text);
            WordExportFontSupport.Apply(run, FontFamily, fontPoints, bold);
        }

        private static void FillCell(XWPFTableCell cell, IReadOnlyList<string> lines, double fontPoints, bool bold, bool center, bool header)
        {
            cell.SetVerticalAlignment(XWPFTableCell.XWPFVertAlign.CENTER);
            if (header)
            {
                cell.SetColor("E8EEF4");
            }

            if (cell.Paragraphs.Count == 0)
            {
                cell.AddParagraph();
            }

            while (cell.Paragraphs.Count > 1)
            {
                cell.RemoveParagraph(cell.Paragraphs.Count - 1);
            }

            for (int i = 0; i < lines.Count; i++)
            {
                XWPFParagraph paragraph = i == 0 ? cell.Paragraphs[0] : cell.AddParagraph();
                paragraph.Alignment = center ? ParagraphAlignment.CENTER : ParagraphAlignment.LEFT;
                SetLineSpacing(paragraph, "0", "0");
                foreach (XWPFRun existing in paragraph.Runs.ToList())
                {
                    paragraph.RemoveRun(paragraph.Runs.IndexOf(existing));
                }

                var run = paragraph.CreateRun();
                run.SetText(lines[i]);
                WordExportFontSupport.Apply(run, FontFamily, fontPoints, bold);
            }
        }

        private static void SetLineSpacing(XWPFParagraph paragraph, string beforeLines, string afterLines)
        {
            paragraph.SpacingBefore = 0;
            paragraph.SpacingAfter = 0;
            var pPr = paragraph.GetCTP().pPr ?? paragraph.GetCTP().AddNewPPr();
            var spacing = pPr.spacing ?? pPr.AddNewSpacing();
            spacing.before = 0;
            spacing.after = 0;
            spacing.beforeLines = beforeLines;
            spacing.afterLines = afterLines;
        }

        private static void ApplyTableBorders(XWPFTable table)
        {
            var tblPr = table.GetCTTbl().tblPr ?? table.GetCTTbl().AddNewTblPr();
            var borders = tblPr.tblBorders ?? tblPr.AddNewTblBorders();
            SetBorder(borders.top ??= new CT_Border());
            SetBorder(borders.bottom ??= new CT_Border());
            SetBorder(borders.left ??= new CT_Border());
            SetBorder(borders.right ??= new CT_Border());
            SetBorder(borders.insideH ??= new CT_Border());
            SetBorder(borders.insideV ??= new CT_Border());
        }

        private static void SetBorder(CT_Border border)
        {
            border.val = ST_Border.single;
            border.sz = 8;
            border.space = 0;
            border.color = "666666";
        }
    }
}
