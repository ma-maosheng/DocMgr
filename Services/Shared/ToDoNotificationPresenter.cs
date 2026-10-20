using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Shared;
using DocMgr.Views.Shared;

namespace DocMgr.Services.Shared
{
    public class ToDoNotificationPresenter : IToDoNotificationPresenter
    {
        private readonly IToDoCenterService _toDoCenterService;

        private ToDoNotificationWindow? _window;
        private ToDoNotificationViewModel? _viewModel;

        public ToDoNotificationPresenter(IToDoCenterService toDoCenterService)
        {
            _toDoCenterService = toDoCenterService;
        }

        public void Show(
            Window owner,
            IEnumerable<ToDoItem> items,
            Func<ToDoItem, Task>? openAction = null,
            Func<IEnumerable<ToDoItem>, Task>? ackAction = null)
        {
            // 已有业务弹窗时不弹出待办，避免挡操作。
            if (ToDoNotificationWindow.HasBlockingPopupStatic(owner))
            {
                Close();
                return;
            }

            if (_window != null)
            {
                _viewModel?.RefreshItems(_toDoCenterService.Items);
                _window.RefreshFloatingState();
                if (!_window.HasBlockingPopup())
                {
                    _window.Activate();
                }

                return;
            }

            var vm = new ToDoNotificationViewModel(items, openAction, ackAction);

            var win = new ToDoNotificationWindow
            {
                DataContext = vm,
                Owner = owner,
                ShowActivated = false
            };

            win.Closed += OnWindowClosed;

            var area = SystemParameters.WorkArea;
            win.Left = area.Right - win.Width - 20;
            win.Top = area.Bottom - win.Height - 20;

            _window = win;
            _viewModel = vm;
            _toDoCenterService.PropertyChanged += ToDoCenter_PropertyChanged;
            win.Show();
            win.RefreshFloatingState();
        }

        /// <inheritdoc />
        public void Close()
        {
            var window = _window;
            if (window == null)
            {
                return;
            }

            if (!window.Dispatcher.CheckAccess())
            {
                window.Dispatcher.Invoke(Close);
                return;
            }

            if (window.IsVisible)
            {
                window.Close();
            }
        }

        private void ToDoCenter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(IToDoCenterService.Items))
            {
                return;
            }

            _viewModel?.RefreshItems(_toDoCenterService.Items);
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _toDoCenterService.PropertyChanged -= ToDoCenter_PropertyChanged;

            if (sender is ToDoNotificationWindow win)
            {
                win.Closed -= OnWindowClosed;
            }

            _window = null;
            _viewModel = null;
        }
    }
}