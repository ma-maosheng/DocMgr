using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DocMgr.ViewModels.HistoryArchive;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Views.HistoryArchive;

public partial class HistoryArchiveBoxLedgerPage : Page
{
    private readonly IServiceScope _pageScope;
    private readonly HistoryArchiveBoxLedgerPageViewModel _viewModel;

    public HistoryArchiveBoxLedgerPage()
    {
        InitializeComponent();
        _pageScope = App.CurrentProvider.CreateScope();
        _viewModel = _pageScope.ServiceProvider.GetRequiredService<HistoryArchiveBoxLedgerPageViewModel>();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyLedgerColumns();
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
        Unloaded += (_, _) =>
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _pageScope.Dispose();
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HistoryArchiveBoxLedgerPageViewModel.ActiveColumns))
        {
            ApplyLedgerColumns();
        }
    }

    /// <summary>按当前资料类别重建表头。</summary>
    private void ApplyLedgerColumns()
    {
        LedgerGrid.Columns.Clear();
        Style? traceStyle = TryFindResource("DisposedTraceLocationTextStyle") as Style;
        foreach (HistoryArchiveBoxLedgerColumn column in _viewModel.ActiveColumns)
        {
            var gridColumn = new DataGridTextColumn
            {
                Header = column.Header,
                Binding = new Binding(column.PropertyName) { Mode = BindingMode.OneWay },
                Width = column.Star
                    ? new DataGridLength(1, DataGridLengthUnitType.Star)
                    : new DataGridLength(column.Width)
            };
            if (column.PropertyName == nameof(HistoryArchiveBoxLedgerRow.ContentSummary))
            {
                gridColumn.ElementStyle = TryFindResource("HistoryDetailSummaryTextStyle") as Style;
            }
            else if (column.TraceLocation && traceStyle != null)
            {
                gridColumn.ElementStyle = traceStyle;
            }

            LedgerGrid.Columns.Add(gridColumn);
        }
    }
}
