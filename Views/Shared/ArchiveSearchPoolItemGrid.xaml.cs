using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DocMgr.Views.Shared;

/// <summary>
/// 立档检索池与入网检索集明细共用表格。列定义只保留这一处。
/// </summary>
public partial class ArchiveSearchPoolItemGrid : UserControl
{
    private const string SelectionPropertyName = "IsSelected";

    private DataGridCheckBoxColumn? _selectionColumn;

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(ArchiveSearchPoolItemGrid));

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem),
        typeof(object),
        typeof(ArchiveSearchPoolItemGrid),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty ShowSelectionColumnProperty = DependencyProperty.Register(
        nameof(ShowSelectionColumn),
        typeof(bool),
        typeof(ArchiveSearchPoolItemGrid),
        new PropertyMetadata(false, OnInteractionChanged));

    public static readonly DependencyProperty ShowElectronicColumnsProperty = DependencyProperty.Register(
        nameof(ShowElectronicColumns),
        typeof(bool),
        typeof(ArchiveSearchPoolItemGrid),
        new PropertyMetadata(true));

    public static readonly DependencyProperty AllowCopyCountEditProperty = DependencyProperty.Register(
        nameof(AllowCopyCountEdit),
        typeof(bool),
        typeof(ArchiveSearchPoolItemGrid),
        new PropertyMetadata(false, OnInteractionChanged));

    public static readonly DependencyProperty AllowConfidentialLevelEditProperty = DependencyProperty.Register(
        nameof(AllowConfidentialLevelEdit),
        typeof(bool),
        typeof(ArchiveSearchPoolItemGrid),
        new PropertyMetadata(false, OnInteractionChanged));

    public static readonly DependencyProperty ConfidentialLevelOptionsProperty = DependencyProperty.Register(
        nameof(ConfidentialLevelOptions),
        typeof(IEnumerable),
        typeof(ArchiveSearchPoolItemGrid));

    public static readonly DependencyProperty CanEditConfidentialLevelProperty = DependencyProperty.Register(
        nameof(CanEditConfidentialLevel),
        typeof(bool),
        typeof(ArchiveSearchPoolItemGrid),
        new PropertyMetadata(true));

    private static readonly DependencyPropertyKey IsGridReadOnlyPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsGridReadOnly),
        typeof(bool),
        typeof(ArchiveSearchPoolItemGrid),
        new PropertyMetadata(true));

    public static readonly DependencyProperty IsGridReadOnlyProperty = IsGridReadOnlyPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey CopyCountIsReadOnlyPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(CopyCountIsReadOnly),
        typeof(bool),
        typeof(ArchiveSearchPoolItemGrid),
        new PropertyMetadata(true));

    public static readonly DependencyProperty CopyCountIsReadOnlyProperty = CopyCountIsReadOnlyPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey ConfidentialLevelIsReadOnlyPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(ConfidentialLevelIsReadOnly),
        typeof(bool),
        typeof(ArchiveSearchPoolItemGrid),
        new PropertyMetadata(true));

    public static readonly DependencyProperty ConfidentialLevelIsReadOnlyProperty =
        ConfidentialLevelIsReadOnlyPropertyKey.DependencyProperty;

    public ArchiveSearchPoolItemGrid()
    {
        InitializeComponent();
        if (Resources["SelfProxy"] is BindingProxy proxy)
        {
            proxy.Data = this;
        }

        ApplyInteractionState();
    }

    /// <summary>行集合。</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>当前选中行。</summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>是否显示行首勾选列。勾选绑定行上的 IsSelected。</summary>
    public bool ShowSelectionColumn
    {
        get => (bool)GetValue(ShowSelectionColumnProperty);
        set => SetValue(ShowSelectionColumnProperty, value);
    }

    /// <summary>是否显示电子介质列：介质类型、所在目录、数据量。</summary>
    public bool ShowElectronicColumns
    {
        get => (bool)GetValue(ShowElectronicColumnsProperty);
        set => SetValue(ShowElectronicColumnsProperty, value);
    }

    /// <summary>份数是否可编辑。行上 IsCopyCountEditable 为 false 时仍显示 1 且不可改。</summary>
    public bool AllowCopyCountEdit
    {
        get => (bool)GetValue(AllowCopyCountEditProperty);
        set => SetValue(AllowCopyCountEditProperty, value);
    }

    /// <summary>密级是否进入编辑（下拉）。具体能否改由 <see cref="CanEditConfidentialLevel"/> 决定。</summary>
    public bool AllowConfidentialLevelEdit
    {
        get => (bool)GetValue(AllowConfidentialLevelEditProperty);
        set => SetValue(AllowConfidentialLevelEditProperty, value);
    }

    /// <summary>密级下拉选项。</summary>
    public IEnumerable? ConfidentialLevelOptions
    {
        get => (IEnumerable?)GetValue(ConfidentialLevelOptionsProperty);
        set => SetValue(ConfidentialLevelOptionsProperty, value);
    }

    /// <summary>当前是否允许修改密级。</summary>
    public bool CanEditConfidentialLevel
    {
        get => (bool)GetValue(CanEditConfidentialLevelProperty);
        set => SetValue(CanEditConfidentialLevelProperty, value);
    }

    /// <summary>整表只读。出现勾选、份数或密级编辑时为 false，其余列仍单独只读。</summary>
    public bool IsGridReadOnly => (bool)GetValue(IsGridReadOnlyProperty);

    /// <summary>份数列是否只读。</summary>
    public bool CopyCountIsReadOnly => (bool)GetValue(CopyCountIsReadOnlyProperty);

    /// <summary>密级列是否只读。</summary>
    public bool ConfidentialLevelIsReadOnly => (bool)GetValue(ConfidentialLevelIsReadOnlyProperty);

    private static void OnInteractionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ArchiveSearchPoolItemGrid)d).ApplyInteractionState();
    }

    private void ApplyInteractionState()
    {
        SetValue(IsGridReadOnlyPropertyKey, !ShowSelectionColumn && !AllowCopyCountEdit && !AllowConfidentialLevelEdit);
        SetValue(CopyCountIsReadOnlyPropertyKey, !AllowCopyCountEdit);
        SetValue(ConfidentialLevelIsReadOnlyPropertyKey, !AllowConfidentialLevelEdit);
        SyncSelectionColumn();
    }

    private void SyncSelectionColumn()
    {
        if (ItemGrid == null)
        {
            return;
        }

        if (!ShowSelectionColumn)
        {
            if (_selectionColumn != null)
            {
                ItemGrid.Columns.Remove(_selectionColumn);
            }

            return;
        }

        _selectionColumn ??= new DataGridCheckBoxColumn
        {
            Header = "选",
            Width = new DataGridLength(40),
            Binding = new Binding(SelectionPropertyName)
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            }
        };

        if (!ItemGrid.Columns.Contains(_selectionColumn))
        {
            ItemGrid.Columns.Insert(0, _selectionColumn);
        }
    }
}
