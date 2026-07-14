using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DocMgr.Config
{
    /// <summary>
    /// 为所有 WPF 窗口统一应用标题栏图标与系统名称。
    /// 隐式 Window 样式在 Application.Resources 中不会可靠生效，因此在 Loaded 阶段集中处理。
    /// </summary>
    public static class DocMgrWindowBranding
    {
        private static ImageSource? _applicationIcon;
        private static bool _isRegistered;

        /// <summary>
        /// 注册全局窗口品牌样式（在应用启动时调用一次）。
        /// </summary>
        public static void Register()
        {
            if (_isRegistered)
            {
                return;
            }

            _isRegistered = true;
            EventManager.RegisterClassHandler(
                typeof(Window),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnWindowLoaded),
                handledEventsToo: true);
        }

        /// <summary>
        /// 将品牌标题与图标应用到指定窗口。
        /// </summary>
        public static void Apply(Window window)
        {
            ArgumentNullException.ThrowIfNull(window);

            window.Title = DocMgrBranding.ApplicationTitle;

            ImageSource? icon = GetApplicationIcon();
            if (icon != null)
            {
                window.Icon = icon;
            }
        }

        /// <summary>
        /// 获取应用图标（优先 ICO，回退 PNG）。
        /// </summary>
        public static ImageSource? GetApplicationIcon()
        {
            if (_applicationIcon != null)
            {
                return _applicationIcon;
            }

            _applicationIcon = TryLoadIcon(DocMgrBranding.ApplicationIconResource)
                ?? TryLoadIcon(DocMgrBranding.ApplicationIconPngResource);

            return _applicationIcon;
        }

        private static void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
            {
                Apply(window);
            }
        }

        private static ImageSource? TryLoadIcon(string resourcePath)
        {
            try
            {
                var iconUri = new Uri($"pack://application:,,,/{resourcePath}", UriKind.Absolute);
                return BitmapFrame.Create(iconUri);
            }
            catch
            {
                return null;
            }
        }
    }
}
