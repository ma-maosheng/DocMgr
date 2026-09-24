using System.Windows.Controls;

namespace DocMgr.Views.Shared;

/// <summary>
/// 盘库登记双列表：按可用高度对齐整行计算 DataGrid 显示高度（含列头）。
/// </summary>
internal static class InventoryRegisterTransferLayoutSupport
{
    public const double DataGridHeaderHeight = 28;
    public const double DataGridRowHeight = 36;
    public const int MinVisibleRows = 4;
    public const int MaxVisibleRows = 18;

    /// <summary>
    /// 将可用高度对齐到「列头 + N 行」，避免最后一行半截显示。
    /// </summary>
    public static double ResolveDataGridHeight(double availableHeight)
    {
        int rows = ResolveVisibleRowCount(availableHeight);
        return ToGridHeight(rows);
    }

    /// <summary>把同一高度应用到左右两个 DataGrid。</summary>
    public static void ApplyDataGridHeights(double availableHeight, params DataGrid[] grids)
    {
        if (grids.Length == 0 || availableHeight <= 0)
        {
            return;
        }

        double height = ResolveDataGridHeight(availableHeight);
        foreach (DataGrid grid in grids)
        {
            if (double.IsNaN(grid.Height) || Math.Abs(grid.Height - height) > 0.5)
            {
                grid.Height = height;
            }
        }
    }

    private static int ResolveVisibleRowCount(double availableHeight)
    {
        if (availableHeight <= DataGridHeaderHeight)
        {
            return MinVisibleRows;
        }

        int rows = (int)Math.Floor((availableHeight - DataGridHeaderHeight) / DataGridRowHeight);
        return Math.Clamp(rows, MinVisibleRows, MaxVisibleRows);
    }

    private static double ToGridHeight(int rows) =>
        DataGridHeaderHeight + rows * DataGridRowHeight;
}
