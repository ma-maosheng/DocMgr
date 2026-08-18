using System.Windows;

namespace DocMgr.Views.NetworkTransfer;

/// <summary>
/// NT-DSP-ED 弹窗布局：按屏幕工作区高度计算窗口与已选明细表格高度。
/// </summary>
internal static class NetworkOnNetDisposalLayoutSupport
{
    private const double DataGridHeaderHeight = 28;
    private const double DataGridRowHeight = 36;
    private const int MinVisibleRows = 6;
    private const int MaxVisibleRows = 16;

    /// <summary>弹窗建议高度（随屏幕工作区放大，保留上下边距）。</summary>
    public static double ResolveWindowHeight()
    {
        double workAreaHeight = SystemParameters.WorkArea.Height;
        return Math.Clamp(workAreaHeight * 0.88, 780, 980);
    }

    /// <summary>
    /// 已选明细表格高度（含表头）：屏幕越高可见行数越多。
    /// </summary>
    public static double ResolveSelectedItemsGridHeight(double windowHeight, double scrollViewportHeight)
    {
        int screenRows = ResolveVisibleRowCount(SystemParameters.WorkArea.Height);
        double screenBasedHeight = ToGridHeight(screenRows);

        if (scrollViewportHeight <= 0)
        {
            return screenBasedHeight;
        }

        // 扣除基本信息、候选区、审核审批、附件及间距，将滚动区剩余高度优先给已选明细。
        const double reservedAboveSelected = 430;
        const double reservedBelowSelected = 250;
        double viewportSpare = scrollViewportHeight - reservedAboveSelected - reservedBelowSelected;
        int viewportRows = ResolveRowCountFromHeight(viewportSpare);
        double viewportBasedHeight = ToGridHeight(viewportRows);

        double height = Math.Max(screenBasedHeight, viewportBasedHeight);
        return Math.Clamp(height, ToGridHeight(MinVisibleRows), ToGridHeight(MaxVisibleRows));
    }

    private static int ResolveVisibleRowCount(double workAreaHeight)
    {
        // 768 高约 6 行，每增加 72px 工作区高度约多 1 行。
        int rows = MinVisibleRows + (int)Math.Floor((workAreaHeight - 768) / 72);
        return Math.Clamp(rows, MinVisibleRows, MaxVisibleRows);
    }

    private static int ResolveRowCountFromHeight(double height)
    {
        if (height <= DataGridHeaderHeight)
        {
            return MinVisibleRows;
        }

        int rows = (int)Math.Floor((height - DataGridHeaderHeight) / DataGridRowHeight);
        return Math.Clamp(rows, MinVisibleRows, MaxVisibleRows);
    }

    private static double ToGridHeight(int rows) =>
        DataGridHeaderHeight + rows * DataGridRowHeight;
}
