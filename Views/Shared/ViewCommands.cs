using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;

namespace DocMgr.Views.Shared
{
    public static class ViewCommands
    {
        public static RoutedUICommand CloseCurrentView { get; } = new("关闭当前页面", nameof(CloseCurrentView), typeof(ViewCommands));

        static ViewCommands()
        {
            CommandManager.RegisterClassCommandBinding(typeof(Page), new CommandBinding(CloseCurrentView, ExecuteCloseCurrentView, CanExecuteCloseCurrentView));
            CommandManager.RegisterClassCommandBinding(typeof(Window), new CommandBinding(CloseCurrentView, ExecuteCloseCurrentView, CanExecuteCloseCurrentView));
        }

        public static void EnsureInitialized()
        {
        }

        private static void CanExecuteCloseCurrentView(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = sender is Page || sender is Window;
            e.Handled = true;
        }

        private static void ExecuteCloseCurrentView(object sender, ExecutedRoutedEventArgs e)
        {
            switch (sender)
            {
                case Window window:
                    if (TryExecuteViewModelCloseWithoutSave(window.DataContext))
                    {
                        e.Handled = true;
                        return;
                    }

                    window.Close();
                    e.Handled = true;
                    return;
                case Page page:
                    ClosePage(page);
                    e.Handled = true;
                    return;
            }
        }

        /// <summary>
        /// 优先执行 ViewModel 的取消/关闭命令，避免窗体直接 Close 时绕过业务层关闭逻辑。
        /// </summary>
        private static bool TryExecuteViewModelCloseWithoutSave(object? dataContext)
        {
            if (dataContext == null)
            {
                return false;
            }

            foreach (string commandPropertyName in new[] { "CancelCommand", "CloseCommand" })
            {
                PropertyInfo? property = dataContext.GetType().GetProperty(commandPropertyName);
                if (property?.GetValue(dataContext) is not ICommand command || !command.CanExecute(null))
                {
                    continue;
                }

                command.Execute(null);
                return true;
            }

            return false;
        }

        private static void ClosePage(Page page)
        {
            ArgumentNullException.ThrowIfNull(page);

            if (Window.GetWindow(page) is MainWindow mainWindow)
            {
                mainWindow.CloseCurrentPage();
                return;
            }

            if (FindAncestor<Frame>(page) is Frame frame)
            {
                frame.Content = null;
                return;
            }

            NavigationService? navigationService = page.NavigationService;
            if (navigationService?.CanGoBack == true)
            {
                navigationService.GoBack();
            }
        }

        private static T? FindAncestor<T>(DependencyObject? current)
            where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = current switch
                {
                    Visual visual => VisualTreeHelper.GetParent(visual),
                    Visual3D visual3D => VisualTreeHelper.GetParent(visual3D),
                    _ => LogicalTreeHelper.GetParent(current)
                };
            }

            return null;
        }
    }
}
