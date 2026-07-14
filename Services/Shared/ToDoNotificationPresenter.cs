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
            if (_window != null)
            {
                _viewModel?.RefreshItems(_toDoCenterService.Items);
                _window.Activate();
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