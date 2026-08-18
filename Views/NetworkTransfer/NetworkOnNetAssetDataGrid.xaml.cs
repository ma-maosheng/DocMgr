using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace DocMgr.Views.NetworkTransfer;

/// <summary>
/// 在网台账资产表：NT-DSP、处置候选与已选明细共用列定义。
/// </summary>
public partial class NetworkOnNetAssetDataGrid : DataGrid
{
    public static readonly DependencyProperty ShowSelectionColumnProperty = DependencyProperty.Register(
        nameof(ShowSelectionColumn),
        typeof(bool),
        typeof(NetworkOnNetAssetDataGrid),
        new PropertyMetadata(false, OnShowSelectionColumnChanged));

    public static readonly DependencyProperty ShowDisposalColumnsProperty = DependencyProperty.Register(
        nameof(ShowDisposalColumns),
        typeof(bool),
        typeof(NetworkOnNetAssetDataGrid),
        new PropertyMetadata(false, OnShowDisposalColumnsChanged));

    public static readonly DependencyProperty ViewDetailCommandProperty = DependencyProperty.Register(
        nameof(ViewDetailCommand),
        typeof(ICommand),
        typeof(NetworkOnNetAssetDataGrid));

    public static readonly DependencyProperty RemoveItemCommandProperty = DependencyProperty.Register(
        nameof(RemoveItemCommand),
        typeof(ICommand),
        typeof(NetworkOnNetAssetDataGrid));

    private DataGridTemplateColumn? _selectionColumn;
    private readonly List<DataGridColumn> _disposalColumns = new();

    public NetworkOnNetAssetDataGrid()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplySelectionColumn();
            ApplyDisposalColumns();
        };
        MouseDoubleClick += OnRowMouseDoubleClick;
        ContextMenu = BuildContextMenu();
    }

    /// <summary>是否显示勾选列（处置候选用）。</summary>
    public bool ShowSelectionColumn
    {
        get => (bool)GetValue(ShowSelectionColumnProperty);
        set => SetValue(ShowSelectionColumnProperty, value);
    }

    /// <summary>是否显示处置原因/方式与操作列（已选明细用）。</summary>
    public bool ShowDisposalColumns
    {
        get => (bool)GetValue(ShowDisposalColumnsProperty);
        set => SetValue(ShowDisposalColumnsProperty, value);
    }

    /// <summary>查看当前行详情。</summary>
    public ICommand? ViewDetailCommand
    {
        get => (ICommand?)GetValue(ViewDetailCommandProperty);
        set => SetValue(ViewDetailCommandProperty, value);
    }

    /// <summary>从已选明细移除一行。</summary>
    public ICommand? RemoveItemCommand
    {
        get => (ICommand?)GetValue(RemoveItemCommandProperty);
        set => SetValue(RemoveItemCommandProperty, value);
    }

    private static void OnShowSelectionColumnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((NetworkOnNetAssetDataGrid)d).ApplySelectionColumn();

    private static void OnShowDisposalColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((NetworkOnNetAssetDataGrid)d).ApplyDisposalColumns();

    private void ApplySelectionColumn()
    {
        if (ShowSelectionColumn)
        {
            if (_selectionColumn != null)
            {
                return;
            }

            var checkBoxFactory = new FrameworkElementFactory(typeof(CheckBox));
            checkBoxFactory.SetBinding(
                CheckBox.IsCheckedProperty,
                new Binding("IsSelected")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });
            checkBoxFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            checkBoxFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            checkBoxFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0));
            _selectionColumn = new DataGridTemplateColumn
            {
                Header = "选",
                Width = new DataGridLength(40),
                CellTemplate = new DataTemplate { VisualTree = checkBoxFactory }
            };
            Columns.Insert(0, _selectionColumn);
            return;
        }

        if (_selectionColumn == null)
        {
            return;
        }

        Columns.Remove(_selectionColumn);
        _selectionColumn = null;
    }

    private void ApplyDisposalColumns()
    {
        if (ShowDisposalColumns)
        {
            if (_disposalColumns.Count > 0)
            {
                return;
            }

            _disposalColumns.Add(new DataGridTemplateColumn
            {
                Header = "原因",
                Width = new DataGridLength(120),
                CellTemplate = (DataTemplate)FindResource("DisposalReasonCellTemplate")
            });
            _disposalColumns.Add(new DataGridTemplateColumn
            {
                Header = "方式",
                Width = new DataGridLength(120),
                CellTemplate = (DataTemplate)FindResource("DisposalMethodCellTemplate")
            });
            _disposalColumns.Add(new DataGridTemplateColumn
            {
                Header = "操作",
                Width = new DataGridLength(130),
                CellTemplate = (DataTemplate)FindResource("DisposalActionsCellTemplate")
            });
            foreach (var column in _disposalColumns)
                Columns.Add(column);
            return;
        }

        if (_disposalColumns.Count == 0)
        {
            return;
        }

        foreach (var column in _disposalColumns)
            Columns.Remove(column);
        _disposalColumns.Clear();
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        var item = new MenuItem { Header = "查看详情" };
        item.Click += (_, _) => TryViewDetail();
        menu.Items.Add(item);
        return menu;
    }

    private void OnRowMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && ItemsControl.ContainerFromElement(this, source) is DataGridRow)
        {
            TryViewDetail();
        }
    }

    private void TryViewDetail()
    {
        if (SelectedItem == null || ViewDetailCommand == null)
        {
            return;
        }

        if (ViewDetailCommand.CanExecute(SelectedItem))
        {
            ViewDetailCommand.Execute(SelectedItem);
        }
    }
}
