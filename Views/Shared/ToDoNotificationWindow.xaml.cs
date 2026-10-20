using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using DocMgr.ViewModels.Shared;

namespace DocMgr.Views.Shared
{
    public partial class ToDoNotificationWindow : Window
    {
        private bool _closingForBlockingPopup;

        public ToDoNotificationWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Closed += ToDoNotificationWindow_Closed;
            Loaded += ToDoNotificationWindow_Loaded;
            Activated += ToDoNotificationWindow_Activated;
            Deactivated += ToDoNotificationWindow_Deactivated;
        }

        /// <summary>
        /// 是否存在会拦截输入的其他业务弹窗（不含主窗口与本待办窗）。
        /// </summary>
        internal bool HasBlockingPopup() => HasBlockingPopupStatic(Owner);

        /// <summary>
        /// 静态判定：是否已有业务弹窗（供 Presenter 在打开待办前复用）。
        /// </summary>
        internal static bool HasBlockingPopupStatic(Window? owner)
        {
            var app = Application.Current;
            if (app == null)
            {
                return false;
            }

            return app.Windows
                .OfType<Window>()
                .Any(window => window.IsVisible
                               && window is not ToDoNotificationWindow
                               && window != owner
                               && window != app.MainWindow);
        }

        /// <summary>
        /// 有业务弹窗时自动关闭待办；否则保持置顶悬浮。
        /// </summary>
        internal void RefreshFloatingState()
        {
            if (HasBlockingPopup())
            {
                Topmost = false;
                RequestCloseForBlockingPopup();
                return;
            }

            Topmost = true;
        }

        private void RequestCloseForBlockingPopup()
        {
            if (_closingForBlockingPopup || !IsVisible)
            {
                return;
            }

            _closingForBlockingPopup = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (IsVisible)
                {
                    Close();
                }
            }), DispatcherPriority.Input);
        }

        private void ToDoNotificationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ToDoNotificationWindow_Loaded;
            RefreshFloatingState();

            if (Application.Current != null)
            {
                Application.Current.Activated += OnApplicationActivated;
            }
        }

        private void ToDoNotificationWindow_Activated(object? sender, EventArgs e)
        {
            RefreshFloatingState();
        }

        private void ToDoNotificationWindow_Deactivated(object? sender, EventArgs e)
        {
            RefreshFloatingState();
        }

        private void OnApplicationActivated(object? sender, EventArgs e)
        {
            RefreshFloatingState();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ToDoNotificationViewModel oldVm)
            {
                oldVm.RequestClose -= HandleRequestClose;
            }

            if (e.NewValue is ToDoNotificationViewModel newVm)
            {
                newVm.RequestClose += HandleRequestClose;
            }
        }

        private void ToDoNotificationWindow_Closed(object? sender, EventArgs e)
        {
            if (Application.Current != null)
            {
                Application.Current.Activated -= OnApplicationActivated;
            }

            DataContextChanged -= OnDataContextChanged;
            Closed -= ToDoNotificationWindow_Closed;
            Activated -= ToDoNotificationWindow_Activated;
            Deactivated -= ToDoNotificationWindow_Deactivated;

            if (DataContext is ToDoNotificationViewModel vm)
            {
                vm.RequestClose -= HandleRequestClose;
            }
        }

        private void HandleRequestClose()
        {
            Close();
        }
    }
}
