using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DocMgr.Views.Shared
{
    /// <summary>
    /// 将嵌套 <see cref="ScrollViewer"/> 在无法继续滚动时的滚轮事件传递给外层滚动容器，
    /// 避免复杂页面出现滚轮无响应或卡顿感。
    /// </summary>
    public static class ScrollViewerWheelRoutingSupport
    {
        private const double ScrollBoundaryTolerance = 0.5;

        /// <summary>
        /// 注册全局滚轮路由处理，应在应用启动时调用一次。
        /// </summary>
        public static void Register()
        {
            EventManager.RegisterClassHandler(
                typeof(ScrollViewer),
                UIElement.PreviewMouseWheelEvent,
                new MouseWheelEventHandler(OnPreviewMouseWheel),
                handledEventsToo: true);
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled || sender is not ScrollViewer scrollViewer)
            {
                return;
            }

            if (!ShouldBubbleToParent(scrollViewer, e.Delta))
            {
                return;
            }

            ScrollViewer? parentScrollViewer = FindParentScrollViewer(scrollViewer);
            if (parentScrollViewer == null)
            {
                return;
            }

            e.Handled = true;

            var routedEventArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.PreviewMouseWheelEvent,
                Source = parentScrollViewer
            };
            parentScrollViewer.RaiseEvent(routedEventArgs);
        }

        private static bool ShouldBubbleToParent(ScrollViewer scrollViewer, int delta)
        {
            if (delta == 0)
            {
                return false;
            }

            if (scrollViewer.ScrollableHeight <= ScrollBoundaryTolerance)
            {
                return true;
            }

            if (delta > 0)
            {
                return scrollViewer.VerticalOffset <= ScrollBoundaryTolerance;
            }

            return scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - ScrollBoundaryTolerance;
        }

        private static ScrollViewer? FindParentScrollViewer(DependencyObject element)
        {
            DependencyObject? current = VisualTreeHelper.GetParent(element);
            while (current != null)
            {
                if (current is ScrollViewer parentScrollViewer && !ReferenceEquals(parentScrollViewer, element))
                {
                    return parentScrollViewer;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
