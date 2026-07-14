using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DocMgr.Services.Interfaces;

namespace DocMgr.Infrastructure.DbOperationLog
{
    /// <summary>
    /// 全局捕获按钮/菜单项点击，供操作日志记录来源按钮。
    /// </summary>
    public static class DbOperationLogUiCapture
    {
        public static readonly DependencyProperty ActionNameProperty = DependencyProperty.RegisterAttached(
            "ActionName",
            typeof(string),
            typeof(DbOperationLogUiCapture),
            new PropertyMetadata(null));

        private static IDbOperationLogContextService? _contextService;

        public static void Register(IDbOperationLogContextService contextService)
        {
            ArgumentNullException.ThrowIfNull(contextService);

            if (_contextService != null)
            {
                return;
            }

            _contextService = contextService;
            EventManager.RegisterClassHandler(
                typeof(Button),
                UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(OnButtonPreviewMouseLeftButtonDown),
                true);

            EventManager.RegisterClassHandler(
                typeof(MenuItem),
                UIElement.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(OnMenuItemPreviewMouseLeftButtonDown),
                true);
        }

        public static void SetActionName(DependencyObject element, string? value)
        {
            element.SetValue(ActionNameProperty, value);
        }

        public static string? GetActionName(DependencyObject element)
        {
            return element.GetValue(ActionNameProperty) as string;
        }

        private static void OnButtonPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_contextService == null || sender is not Button button)
            {
                return;
            }

            if (!button.IsEnabled)
            {
                return;
            }

            string? actionName = ResolveActionName(button);
            if (actionName != null)
            {
                _contextService.CaptureButtonAction(actionName);
            }
        }

        private static void OnMenuItemPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_contextService == null || sender is not MenuItem menuItem)
            {
                return;
            }

            if (!menuItem.IsEnabled)
            {
                return;
            }

            string? actionName = ResolveActionName(menuItem);
            if (actionName != null)
            {
                _contextService.CaptureButtonAction(actionName);
            }
        }

        private static string? ResolveActionName(FrameworkElement element)
        {
            string? attached = GetActionName(element);
            if (!string.IsNullOrWhiteSpace(attached))
            {
                return attached.Trim();
            }

            if (element is Button { Content: string text } && !string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }

            if (element is MenuItem { Header: string header } && !string.IsNullOrWhiteSpace(header))
            {
                return header.Trim();
            }

            if (!string.IsNullOrWhiteSpace(element.Name))
            {
                return element.Name.Trim();
            }

            return element.GetType().Name;
        }
    }
}
