#if DEBUG
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DocMgr.Infrastructure.DebugUi
{
    /// <summary>
    /// Debug 下为所有 Page / Window 自动叠加短码角标。
    /// </summary>
    public static class UiDebugIdBadgeSupport
    {
        private static readonly DependencyProperty IsAttachedProperty =
            DependencyProperty.RegisterAttached(
                "IsAttached",
                typeof(bool),
                typeof(UiDebugIdBadgeSupport),
                new PropertyMetadata(false));

        private static bool _isRegistered;

        /// <summary>
        /// 注册全局 Loaded 钩子（仅 Debug，启动时调用一次）。
        /// </summary>
        public static void Register()
        {
            if (_isRegistered)
            {
                return;
            }

            _isRegistered = true;

            EventManager.RegisterClassHandler(
                typeof(Page),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnHostLoaded),
                handledEventsToo: true);

            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnHostLoaded),
                handledEventsToo: true);
        }

        private static void OnHostLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement host)
            {
                return;
            }

            if (sender is not Page and not Window)
            {
                return;
            }

            if (Equals(host.GetValue(IsAttachedProperty), true))
            {
                return;
            }

            AttachBadge(host);
        }

        private static void AttachBadge(FrameworkElement host)
        {
            host.SetValue(IsAttachedProperty, true);

            UiDebugIdEntry entry = UiDebugIdCatalog.Resolve(host);
            Border badge = CreateBadge(entry);

            if (host is Page page)
            {
                // 必须先断开 Content，否则加入新 Grid 会抛“已是另一元素的逻辑子元素”
                object? existing = page.Content;
                page.Content = null;
                page.Content = WrapContent(existing, badge);
                return;
            }

            if (host is Window window)
            {
                object? existing = window.Content;
                window.Content = null;
                window.Content = WrapContent(existing, badge);
            }
        }

        private static Grid WrapContent(object? existingContent, FrameworkElement badge)
        {
            var overlay = new Grid();

            if (existingContent is UIElement uiElement)
            {
                overlay.Children.Add(uiElement);
            }
            else if (existingContent != null)
            {
                overlay.Children.Add(new ContentPresenter { Content = existingContent });
            }

            Panel.SetZIndex(badge, short.MaxValue);
            overlay.Children.Add(badge);
            return overlay;
        }

        private static Border CreateBadge(UiDebugIdEntry entry)
        {
            var textBlock = new TextBlock
            {
                Text = entry.Code,
                Foreground = entry.IsRegistered
                    ? new SolidColorBrush(Color.FromRgb(0xFE, 0xF0, 0x8C))
                    : new SolidColorBrush(Color.FromRgb(0xFD, 0xBA, 0x74)),
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(8, 3, 8, 3)
            };

            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x11, 0x18, 0x27)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xAA, 0xF5, 0x9E, 0x0B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Child = textBlock,
                Cursor = Cursors.Hand,
                ToolTip = entry.ToolTipText + "\n点击复制短码",
                Opacity = 0.92,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 6, 0)
            };

            badge.MouseLeftButtonDown += (_, args) =>
            {
                try
                {
                    Clipboard.SetText(entry.Code);
                    badge.ToolTip = $"{entry.ToolTipText}\n已复制：{entry.Code}";
                }
                catch
                {
                    // 剪贴板失败不影响业务
                }

                args.Handled = true;
            };

            return badge;
        }
    }
}
#endif
