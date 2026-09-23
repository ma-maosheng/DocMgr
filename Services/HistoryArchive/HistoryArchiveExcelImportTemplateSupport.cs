using System;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DocMgr.Services.HistoryArchive
{
    /// <summary>
    /// 历史存档 Excel 导入模板导出（地形图 / 航摄影像 / 其他资料）。
    /// </summary>
    public static class HistoryArchiveExcelImportTemplateSupport
    {
        public const string SheetNameImport = "导入录入";
        public const string SheetNameInstructions = "字段说明";

        /// <summary>导出地形图导入模板。</summary>
        public static void ExportTopoMapTemplate(string filePath)
        {
            string[] headers =
            {
                "档案盒编号", "档案盒规格", "比例尺", "图号", "图名", "幅数",
                "成图日期", "调绘日期", "坐标系统", "高程基准", "涉及省市县", "备注"
            };

            string[] example =
            {
                "丙A-3-2-01", "标准(5cm)", "1:10000", "J50E001001", "示例图名", "1",
                "2020-06-01", "2020-05-01", "2000国家大地坐标系", "1985国家高程基准", "河北省某某市", string.Empty
            };

            var instructions = new (string Field, string Hint)[]
            {
                ("填写说明", "一行一条图幅记录；同一档案盒的「档案盒编号/规格」可只在首行填写，后续行留空自动下填。导入前请删除或改写示例行。登记人/登记日期由系统写入。"),
                ("档案盒编号", "必填；物理位置，格式如 丙A-3-2-01（柜面-层-列-盒序号）。"),
                ("档案盒规格", "必填；标准(10cm)、标准(5cm)、标准(3cm)、标准(2cm)、非标(10cm)。"),
                ("比例尺", "必填；空白行将被跳过。"),
                ("图号", "填写原图号；当前图号由系统按比例尺规则自动计算。"),
                ("图名", "可选。"),
                ("幅数", "可选；整数。"),
                ("成图日期", "可选；兼容列名「成图时间」；建议 yyyy-MM-dd。"),
                ("调绘日期", "可选；兼容列名「调绘时间」；建议 yyyy-MM-dd。"),
                ("坐标系统", "可选。"),
                ("高程基准", "可选。"),
                ("涉及省市县", "可选。"),
                ("备注", "可选。")
            };

            WriteTemplate(filePath, headers, example, instructions);
        }

        /// <summary>导出航摄影像导入模板。</summary>
        public static void ExportAerialPhotoTemplate(string filePath)
        {
            string[] headers =
            {
                "档案盒编号", "档案盒规格", "测区名称", "比例尺", "航摄日期",
                "档案盒内物品", "相片张数", "备注"
            };

            string[] example =
            {
                "丙A-3-2-01", "标准(5cm)", "示例测区", "1:8000", "2019-08-15",
                "底片及相片", "120", string.Empty
            };

            var instructions = new (string Field, string Hint)[]
            {
                ("填写说明", "一行一条测区/盒记录；「档案盒编号/规格/比例尺」可下填。导入前请删除或改写示例行。登记人/登记日期由系统写入。"),
                ("档案盒编号", "必填（可与测区名称二选一有值）；格式如 丙A-3-2-01。"),
                ("档案盒规格", "必填；标准(10cm)、标准(5cm)、标准(3cm)、标准(2cm)、非标(10cm)。"),
                ("测区名称", "建议必填；与档案盒编号同时为空时该行跳过。"),
                ("比例尺", "可选；兼容列名「航摄比例尺」。"),
                ("航摄日期", "可选；兼容列名「航摄时间」；建议 yyyy-MM-dd。"),
                ("档案盒内物品", "可选。"),
                ("相片张数", "可选；整数；兼容列名「像片张数」。"),
                ("备注", "可选。")
            };

            WriteTemplate(filePath, headers, example, instructions);
        }

        /// <summary>导出其他历史资料导入模板。</summary>
        public static void ExportOtherMapTemplate(string filePath)
        {
            string[] headers =
            {
                "序号", "资料分类", "起始年度", "截止年度", "资料内容",
                "档案盒编号", "档案盒规格", "备注"
            };

            string[] example =
            {
                "1", "专题图", "2018", "2020", "示例资料内容",
                "丙A-3-2-01", "标准(5cm)", string.Empty
            };

            var instructions = new (string Field, string Hint)[]
            {
                ("填写说明", "一行一条资料记录；「档案盒编号/规格」可下填。导入前请删除或改写示例行。登记人/登记日期由系统写入。「资料内容」兼容列名「图名」；「档案盒编号」兼容「盒号」。"),
                ("序号", "可选。"),
                ("资料分类", "建议填写。"),
                ("起始年度", "可选；四位年份。"),
                ("截止年度", "可选；四位年份。"),
                ("资料内容", "建议填写；兼容列名「图名」。"),
                ("档案盒编号", "必填；格式如 丙A-3-2-01；兼容列名「盒号」。"),
                ("档案盒规格", "必填；标准(10cm)、标准(5cm)、标准(3cm)、标准(2cm)、非标(10cm)。"),
                ("备注", "可选。")
            };

            WriteTemplate(filePath, headers, example, instructions);
        }

        private static void WriteTemplate(
            string filePath,
            string[] headers,
            string[] exampleRow,
            (string Field, string Hint)[] instructions)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("导出文件路径不能为空。", nameof(filePath));
            }

            string? directoryPath = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("导出文件目录无效。", nameof(filePath));
            }

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            using var workbook = new XSSFWorkbook();
            var importSheet = workbook.CreateSheet(SheetNameImport);
            var headerRow = importSheet.CreateRow(0);
            for (int i = 0; i < headers.Length; i++)
            {
                headerRow.CreateCell(i).SetCellValue(headers[i]);
                importSheet.SetColumnWidth(i, Math.Max(12, headers[i].Length + 4) * 256);
            }

            var sample = importSheet.CreateRow(1);
            for (int i = 0; i < exampleRow.Length && i < headers.Length; i++)
            {
                sample.CreateCell(i).SetCellValue(exampleRow[i]);
            }

            var instructionSheet = workbook.CreateSheet(SheetNameInstructions);
            instructionSheet.SetColumnWidth(0, 18 * 256);
            instructionSheet.SetColumnWidth(1, 72 * 256);
            for (int i = 0; i < instructions.Length; i++)
            {
                var row = instructionSheet.CreateRow(i);
                row.CreateCell(0).SetCellValue(instructions[i].Field);
                row.CreateCell(1).SetCellValue(instructions[i].Hint);
            }

            using var output = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            workbook.Write(output, leaveOpen: true);
        }
    }
}
