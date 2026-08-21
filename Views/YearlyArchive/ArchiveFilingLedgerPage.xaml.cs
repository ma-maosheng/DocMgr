using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveFilingLedgerPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly ArchiveFilingLedgerViewModel _viewModel;
        private bool _preserveStateOnUnload;

        public ArchiveFilingLedgerPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            _viewModel = _pageScope.ServiceProvider.GetRequiredService<ArchiveFilingLedgerViewModel>();
            DataContext = _viewModel;

            _viewModel.ViewRegisterDetailRequested += ViewModel_ViewRegisterDetailRequested;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            Loaded += ArchiveFilingLedgerPage_Loaded;
            Unloaded += ArchiveFilingLedgerPage_Unloaded;
        }

        private async void ArchiveFilingLedgerPage_Loaded(object sender, RoutedEventArgs e)
        {
            RestartBusyProgress();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await _viewModel.InitializeAsync();
            await _viewModel.ApplyPendingNavigationFocusAsync();
        }

        private void ArchiveFilingLedgerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_preserveStateOnUnload)
            {
                _preserveStateOnUnload = false;
                return;
            }

            Loaded -= ArchiveFilingLedgerPage_Loaded;
            Unloaded -= ArchiveFilingLedgerPage_Unloaded;
            _viewModel.ViewRegisterDetailRequested -= ViewModel_ViewRegisterDetailRequested;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _pageScope.Dispose();
        }

        private void ViewModel_ViewRegisterDetailRequested(ArchiveDetailOpenRequest request)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                _preserveStateOnUnload = true;
                mainWindow.NavigateToArchiveDetailPage(
                    request.RegisterRecordId,
                    request.SearchHighlight,
                    request.FilterPoolMediaKind,
                    request.FilingFactId);
                return;
            }

            MessageBox.Show("当前无法打开资料查看页。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ArchiveFilingLedgerViewModel.IsBusy) && _viewModel.IsBusy)
            {
                RestartBusyProgress();
            }
        }

        private void LedgerBusyProgress_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                RestartBusyProgress();
            }
        }

        private void RestartBusyProgress()
        {
            Dispatcher.BeginInvoke(() =>
            {
                LedgerBusyProgress.IsIndeterminate = false;
                LedgerBusyProgress.UpdateLayout();
                LedgerBusyProgress.IsIndeterminate = true;
            }, DispatcherPriority.Loaded);
        }

        private void FoldButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            if (button.Command != null && button.Command.CanExecute(button.CommandParameter))
            {
                button.Command.Execute(button.CommandParameter);
            }

            e.Handled = true;
        }
    }
}
