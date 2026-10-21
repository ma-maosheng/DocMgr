using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using DocMgr.Models.Inventory;
using DocMgr.ViewModels.Inventory;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.Inventory
{
    public partial class StockContainerOverviewPage : Page
    {
        private readonly IServiceScope _pageScope;
        private readonly StockContainerOverviewViewModel _viewModel;

        public StockContainerOverviewPage()
        {
            InitializeComponent();

            _pageScope = App.CurrentProvider.CreateScope();
            _viewModel = _pageScope.ServiceProvider.GetRequiredService<StockContainerOverviewViewModel>();
            DataContext = _viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyOverviewColumns();

            _viewModel.OpenFilingLedgerRequested += ViewModel_OpenFilingLedgerRequested;
            Loaded += StockContainerOverviewPage_Loaded;
            Unloaded += StockContainerOverviewPage_Unloaded;
        }

        private async void StockContainerOverviewPage_Loaded(object sender, RoutedEventArgs e)
        {
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await _viewModel.InitializeAsync();
        }

        private void StockContainerOverviewPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Loaded -= StockContainerOverviewPage_Loaded;
            Unloaded -= StockContainerOverviewPage_Unloaded;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.OpenFilingLedgerRequested -= ViewModel_OpenFilingLedgerRequested;
            _pageScope.Dispose();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StockContainerOverviewViewModel.SelectedBrowseKind))
            {
                ApplyOverviewColumns();
            }
        }

        /// <summary>按当前资料类型重建上下两表的列。</summary>
        private void ApplyOverviewColumns()
        {
            Style? traceStyle = TryFindResource("DisposedTraceLocationTextStyle") as Style;
            FillGrid(
                ContainerGrid,
                StockContainerOverviewColumns.ContainerColumns(_viewModel.SelectedBrowseKind),
                traceStyle);
            FillGrid(
                ContentGrid,
                StockContainerOverviewColumns.ContentColumns(_viewModel.SelectedBrowseKind),
                traceStyle);
        }

        private static void FillGrid(
            DataGrid grid,
            IReadOnlyList<StockContainerOverviewGridColumn> columns,
            Style? traceStyle)
        {
            grid.Columns.Clear();
            foreach (StockContainerOverviewGridColumn column in columns)
            {
                var gridColumn = new DataGridTextColumn
                {
                    Header = column.Header,
                    Binding = new Binding(column.PropertyName) { Mode = BindingMode.OneWay },
                    Width = column.Star
                        ? new DataGridLength(1, DataGridLengthUnitType.Star)
                        : new DataGridLength(column.Width)
                };
                if (column.TraceLocation && traceStyle != null)
                {
                    gridColumn.ElementStyle = traceStyle;
                }

                grid.Columns.Add(gridColumn);
            }
        }

        private void ViewModel_OpenFilingLedgerRequested(int filingFactId)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.NavigateToArchiveFilingLedger(filingFactId);
                return;
            }

            MessageBox.Show("当前无法打开立档台账。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
