using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.XWPF.UserModel;

namespace DocMgr.Services.Shared
{
    /// <summary>
    /// Word 导出字体：必须同时设置 ascii/eastAsia/hAnsi 与 sz/szCs，
    /// 否则中文会回落文档默认样式，常表现为字号偏大。
    /// 相对 FlowDocument 预览：先缩小一号，再缩小半号
    /// （二号→约17pt、小四→9.5pt、五号→8pt）。
    /// </summary>
    public static class WordExportFontSupport
    {
        /// <summary>正文/标签：9.5pt（五号与小五之间，相对预览小四共缩小约 1.5 号）。</summary>
        public const double BodyFontPoints = 9.5;

        /// <summary>标题：17pt（小二与三号之间，相对预览二号共缩小约 1.5 号）。</summary>
        public const double TitleFontPoints = 17;

        /// <summary>表后说明：8pt（小五与六号之间，相对预览五号共缩小约 1.5 号）。</summary>
        public const double FooterFontPoints = 8;


        /// <summary>段前/段后间距：0.5 行（OOXML 单位为百分之一行）。</summary>
        public const string ParagraphSpacingHalfLineHundredths = "50";

        public const string FontFamily = "宋体";

        /// <summary>兼容旧引用，等同 <see cref="FontFamily"/>。</summary>
        public const string BodyFontFamily = FontFamily;
        public const string TitleFontFamily = FontFamily;
        public const string LabelFontFamily = FontFamily;


        /// <summary>
        /// 将段落段前、段后间距统一设为 0.5 行（清除 twips 绝对值，避免叠加）。
        /// </summary>
        public static void ApplyHalfLineParagraphSpacing(XWPFParagraph paragraph)
        {
            ArgumentNullException.ThrowIfNull(paragraph);

            paragraph.SpacingBefore = 0;
            paragraph.SpacingAfter = 0;

            var pPr = paragraph.GetCTP().pPr ?? paragraph.GetCTP().AddNewPPr();
            var spacing = pPr.spacing ?? pPr.AddNewSpacing();
            spacing.before = 0;
            spacing.after = 0;
            spacing.beforeLines = ParagraphSpacingHalfLineHundredths;
            spacing.afterLines = ParagraphSpacingHalfLineHundredths;
        }

        /// <summary>
        /// 为 Run 设置字体与字号（含东亚与复杂文种）。
        /// </summary>
        public static void Apply(
            XWPFRun run,
            string fontFamily,
            double fontPoints,
            bool bold = false)
        {
            ArgumentNullException.ThrowIfNull(run);
            ArgumentException.ThrowIfNullOrWhiteSpace(fontFamily);
            if (fontPoints < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(fontPoints));
            }

            string family = fontFamily.Trim();
            run.IsBold = bold;
            run.FontSize = fontPoints;
            run.FontFamily = family;

            // FontFamily 仅写 ascii；中文必须显式写 eastAsia，并同步 szCs。
            CT_R ctr = run.GetCTR();
            CT_RPr rPr = ctr.rPr ?? ctr.AddNewRPr();
            CT_Fonts fonts = rPr.rFonts ?? rPr.AddNewRFonts();
            fonts.ascii = family;
            fonts.hAnsi = family;
            fonts.eastAsia = family;
            fonts.cs = family;

            ulong halfPoints = (ulong)Math.Round(fontPoints * 2);
            CT_HpsMeasure sz = rPr.sz ?? rPr.AddNewSz();
            sz.val = halfPoints;
            CT_HpsMeasure szCs = rPr.szCs ?? rPr.AddNewSzCs();
            szCs.val = halfPoints;

            if (bold)
            {
                rPr.b ??= rPr.AddNewB();
                rPr.bCs ??= rPr.AddNewBCs();
            }
        }

        /// <summary>正文（宋体）。</summary>
        public static void ApplyBody(XWPFRun run, bool bold = false) =>
            Apply(run, FontFamily, BodyFontPoints, bold);

        /// <summary>表格标签（宋体加粗）。</summary>
        public static void ApplyLabel(XWPFRun run) =>
            Apply(run, FontFamily, BodyFontPoints, bold: true);

        /// <summary>标题（宋体加粗）。</summary>
        public static void ApplyTitle(XWPFRun run) =>
            Apply(run, FontFamily, TitleFontPoints, bold: true);

        /// <summary>表后说明（宋体）。</summary>
        public static void ApplyFooter(XWPFRun run, bool bold = false) =>
            Apply(run, FontFamily, FooterFontPoints, bold);
    }
}
