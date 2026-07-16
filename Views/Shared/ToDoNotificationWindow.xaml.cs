using System;
using System.Linq;
using System.Windows;
using DocMgr.ViewModels.Shared;

namespace DocMgr.Views.Shared
{
    public partial class ToDoNotificationWindow : Window
    {
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
        internal bool HasBlockingPopup()
        {
            var app = Application.Current;
            if (app == null)
            {
                return false;
            }

            return app.Windows
                .OfType<Window>()
                .Any(window => window.IsVisible
                               && window != this
                               && window is not ToDoNotificationWindow
                               && window != Owner);
        }

        /// <summary>
        /// 根据当前弹窗栈刷新置顶与激活状态，避免“窗体在上、按钮不可点”。
        /// </summary>
        internal void RefreshFloatingState()
        {
            var canFloat = !HasBlockingPopup();
            Topmost = canFloat;

            if (!canFloat && IsActive)
            {
                var blocker = Application.Current?.Windows
                    .OfType<Window>()
                    .LastOrDefault(window => window.IsVisible
                                             && window != this
                                             && window is not ToDoNotificationWindow
                                             && window != Owner);

                blocker?.Activate();
            }
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
