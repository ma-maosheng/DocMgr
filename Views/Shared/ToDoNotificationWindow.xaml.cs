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

        private void ToDoNotificationWindow_Closed(object? sender, System.EventArgs e)
        {
            DataContextChanged -= OnDataContextChanged;
            Closed -= ToDoNotificationWindow_Closed;

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