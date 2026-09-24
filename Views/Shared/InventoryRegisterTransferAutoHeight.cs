using System.Windows;
using System.Windows.Controls;

namespace DocMgr.Views.Shared;

/// <summary>
/// 盘库登记双列表宿主：按剩余高度为同行左右 DataGrid 对齐整行高度。
/// 挂在共享行布局的 Transfer Grid 上（标题/筛选·登记类型 Auto，表体同行）。
/// </summary>
public static class InventoryRegisterTransferAutoHeight
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(InventoryRegisterTransferAutoHeight),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) =>
        (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) =>
        obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Grid host)
        {
            return;
        }

        host.SizeChanged -= OnHostSizeChanged;
        host.Loaded -= OnHostLoaded;
        if ((bool)e.NewValue)
        {
            host.SizeChanged += OnHostSizeChanged;
            host.Loaded += OnHostLoaded;
        }
    }

    private static void OnHostLoaded(object sender, RoutedEventArgs e) =>
        Apply(sender as Grid);

    private static void OnHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.HeightChanged)
        {
            Apply(sender as Grid);
        }
    }

    private static void Apply(Grid? host)
    {
        if (host is null || host.ActualHeight <= 0)
        {
            return;
        }

        FrameworkElement? title = null;
        FrameworkElement? filter = null;
        FrameworkElement? rightFilter = null;
        FrameworkElement? footer = null;
        DataGrid? leftGrid = null;
        DataGrid? rightGrid = null;

        foreach (UIElement child in host.Children)
        {
            int row = Grid.GetRow(child);
            int column = Grid.GetColumn(child);
            switch (row)
            {
                case 0 when column == 0 && child is FrameworkElement titleElement:
                    title = titleElement;
                    break;
                case 1 when column == 0 && child is FrameworkElement filterElement:
                    filter = filterElement;
                    break;
                case 1 when column == 2 && child is FrameworkElement rightFilterElement:
                    rightFilter = rightFilterElement;
                    break;
                case 2 when column == 0 && child is DataGrid left:
                    leftGrid = left;
                    break;
                case 2 when column == 2 && child is DataGrid right:
                    rightGrid = right;
                    break;
                case 3 when column == 2 && child is FrameworkElement footerElement:
                    footer = footerElement;
                    break;
            }
        }

        if (leftGrid is null || rightGrid is null)
        {
            return;
        }

        double filterChrome = Math.Max(MeasureChrome(filter), MeasureChrome(rightFilter));
        double chrome = MeasureChrome(title) + filterChrome + MeasureChrome(footer);
        double available = host.ActualHeight - chrome;
        InventoryRegisterTransferLayoutSupport.ApplyDataGridHeights(available, leftGrid, rightGrid);
    }

    private static double MeasureChrome(FrameworkElement? element)
    {
        if (element is null || element.Visibility == Visibility.Collapsed)
        {
            return 0;
        }

        return element.ActualHeight + element.Margin.Top + element.Margin.Bottom;
    }
}
