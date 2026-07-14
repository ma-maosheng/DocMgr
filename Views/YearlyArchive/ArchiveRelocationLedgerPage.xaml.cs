using DocMgr.ViewModels.YearlyArchive;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.YearlyArchive
{
    public partial class ArchiveRelocationLedgerPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly ArchiveRelocationLedgerViewModel _viewModel;

        public ArchiveRelocationLedgerPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            _viewModel = _pageScope.ServiceProvider.GetRequiredService<ArchiveRelocationLedgerViewModel>();
            DataContext = _viewModel;

            _viewModel.NavigateToFilingLedgerRequested += ViewModel_NavigateToFilingLedgerRequested;

            Loaded += ArchiveRelocationLedgerPage_Loaded;
            Unloaded += ArchiveRelocationLedgerPage_Unloaded;
        }

        private async void ArchiveRelocationLedgerPage_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        private void ArchiveRelocationLedgerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ArchiveRelocationLedgerPage_Loaded;
            Unloaded -= ArchiveRelocationLedgerPage_Unloaded;
            _viewModel.NavigateToFilingLedgerRequested -= ViewModel_NavigateToFilingLedgerRequested;
            _pageScope.Dispose();
        }

        private void ViewModel_NavigateToFilingLedgerRequested(int filingFactId)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.NavigateToArchiveFilingLedger(filingFactId);
                return;
            }

            MessageBox.Show("当前无法打开立档台账。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LedgerGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.NavigateToFilingLedgerCommand.CanExecute(null))
            {
                _viewModel.NavigateToFilingLedgerCommand.Execute(null);
            }
        }
    }
}
