using DocMgr.Models.Shared;
using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.XWPF.UserModel;

namespace DocMgr.Services.Shared
{
    /// <summary>
    /// Word 导出表格列宽：与 FlowDocument 打印预览对齐，并固定布局，避免 Word 自动调宽。
    /// </summary>
    public static class WordExportTableLayoutSupport
    {
        /// <summary>
        /// 申请审批单主表列宽（标签|内容|标签|内容）：
        /// 与 FlowDocument Star 权重一致，标签列约占 8 个汉字宽。
        /// </summary>
        public static readonly int[] ApprovalFormMainColumnWidthsTwips =
            DistributeWidths(
                PrintPageLayoutSupport.ContentWidthTwips,
                PrintPageLayoutSupport.ApprovalFormMainColumnStars);

        /// <summary>
        /// 按权重把总宽分配为各列 twips，余数摊到最后一列，保证合计精确。
        /// </summary>
        public static int[] DistributeWidths(int totalTwips, IReadOnlyList<double> stars)
        {
            ArgumentNullException.ThrowIfNull(stars);
            if (stars.Count == 0)
            {
                throw new ArgumentException("列宽权重不能为空。", nameof(stars));
            }

            if (totalTwips <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalTwips));
            }

            double sum = 0;
            for (int i = 0; i < stars.Count; i++)
            {
                double weight = stars[i] > 0 ? stars[i] : 1;
                sum += weight;
            }

            var widths = new int[stars.Count];
            int allocated = 0;
            for (int i = 0; i < stars.Count; i++)
            {
                double weight = stars[i] > 0 ? stars[i] : 1;
                if (i == stars.Count - 1)
                {
                    widths[i] = totalTwips - allocated;
                }
                else
                {
                    widths[i] = (int)Math.Round(totalTwips * weight / sum);
                    allocated += widths[i];
                }
            }

            return widths;
        }

        /// <summary>等宽列。</summary>
        public static int[] DistributeEqualWidths(int totalTwips, int columnCount)
        {
            if (columnCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(columnCount));
            }

            var stars = new double[columnCount];
            Array.Fill(stars, 1d);
            return DistributeWidths(totalTwips, stars);
        }

        /// <summary>
        /// 设置表宽、固定布局与 tblGrid 列宽（不设置百分比 Width，避免与 dxa 冲突）。
        /// </summary>
        public static void ApplyFixedTableLayout(XWPFTable table, IReadOnlyList<int> columnWidthsTwips)
        {
            ArgumentNullException.ThrowIfNull(table);
            ArgumentNullException.ThrowIfNull(columnWidthsTwips);
            if (columnWidthsTwips.Count == 0)
            {
                throw new ArgumentException("列宽不能为空。", nameof(columnWidthsTwips));
            }

            int total = 0;
            for (int i = 0; i < columnWidthsTwips.Count; i++)
            {
                total += columnWidthsTwips[i];
            }

            var tbl = table.GetCTTbl();
            var tblPr = tbl.tblPr ?? tbl.AddNewTblPr();

            var tblW = tblPr.tblW ?? tblPr.AddNewTblW();
            tblW.type = ST_TblWidth.dxa;
            tblW.w = total.ToString();

            CT_TblLayoutType layout = tblPr.tblLayout ?? tblPr.AddNewTblLayout();
            layout.type = ST_TblLayoutType.@fixed;

            var grid = tbl.tblGrid ?? tbl.AddNewTblGrid();
            grid.gridCol.Clear();
            foreach (int width in columnWidthsTwips)
            {
                grid.AddNewGridCol().w = (ulong)Math.Max(0, width);
            }
        }

        /// <summary>按列宽数组为整行各单元格写入 tcW（支持 gridSpan）。</summary>
        public static void ApplyRowCellWidths(XWPFTableRow row, IReadOnlyList<int> columnWidthsTwips)
        {
            ArgumentNullException.ThrowIfNull(row);
            ArgumentNullException.ThrowIfNull(columnWidthsTwips);

            int columnIndex = 0;
            foreach (XWPFTableCell cell in row.GetTableCells())
            {
                if (columnIndex >= columnWidthsTwips.Count)
                {
                    break;
                }

                int span = Math.Max(1, GetGridSpan(cell));
                span = Math.Min(span, columnWidthsTwips.Count - columnIndex);

                int width = 0;
                for (int i = 0; i < span; i++)
                {
                    width += columnWidthsTwips[columnIndex + i];
                }

                SetCellWidth(cell, width);
                columnIndex += span;
            }
        }

        /// <summary>设置单个单元格宽度（dxa）。</summary>
        public static void SetCellWidth(XWPFTableCell cell, int widthTwips)
        {
            ArgumentNullException.ThrowIfNull(cell);
            var tcPr = cell.GetCTTc().tcPr ?? cell.GetCTTc().AddNewTcPr();
            var tcW = tcPr.tcW ?? tcPr.AddNewTcW();
            tcW.type = ST_TblWidth.dxa;
            tcW.w = Math.Max(0, widthTwips).ToString();
        }

        private static int GetGridSpan(XWPFTableCell cell)
        {
            string? raw = cell.GetCTTc().tcPr?.gridSpan?.val;
            if (!string.IsNullOrWhiteSpace(raw)
                && int.TryParse(raw, out int span)
                && span > 0)
            {
                return span;
            }

            return 1;
        }
    }
}
