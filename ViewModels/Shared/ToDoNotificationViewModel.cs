using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Shared
{
    public class ToDoNotificationViewModel : ViewModelBase
    {
        private readonly Func<ToDoItem, Task>? _openAction;
        private readonly Func<IEnumerable<ToDoItem>, Task>? _ackAction;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ObservableCollection<ToDoItem> Items { get; }

        public string SummaryText => $"您有 {Items.Count} 条待办事项";

        public ICommand CloseCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand OpenItemCommand { get; }

        public event Action? RequestClose;

        public ToDoNotificationViewModel(
            IEnumerable<ToDoItem> items,
            Func<ToDoItem, Task>? openAction = null,
            Func<IEnumerable<ToDoItem>, Task>? ackAction = null)
        {
            _openAction = openAction;
            _ackAction = ackAction;

            Items = new ObservableCollection<ToDoItem>(items);

            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(), _ => !IsBusy);
            CloseCommand = new RelayCommand(async _ => await AcknowledgeAndCloseAsync(), _ => !IsBusy);
            OpenItemCommand = new RelayCommand<ToDoItem>(async item => await OpenItemAndCloseAsync(item), _ => !IsBusy);
        }

        /// <summary>
        /// 用最新待办列表刷新窗体内容（窗体打开期间由待办中心推送更新）。
        /// </summary>
        public void RefreshItems(IEnumerable<ToDoItem> items)
        {
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }

            OnPropertyChanged(nameof(SummaryText));
        }

        private async Task AcknowledgeAndCloseAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;

                if (_ackAction != null)
                {
                    await _ackAction(Items);
                }

                RequestClose?.Invoke();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task OpenItemAndCloseAsync(ToDoItem? item)
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;

                if (item != null && _openAction != null)
                {
                    await _openAction(item);
                }

                RequestClose?.Invoke();
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}