using System.Windows;
using DocMgr.Views.Shared;

namespace DocMgr.Views.YearlyArchive;

/// <summary>
/// 电子立档明细。已有明细只读；待立档明细可编辑立档存储路径。
/// </summary>
public partial class ElectronicFilingDetailGrid : LedgerGridHost
{
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded),
        typeof(bool),
        typeof(ElectronicFilingDetailGrid),
        new PropertyMetadata(true));

    public static readonly DependencyProperty AllowEditStoragePathProperty = DependencyProperty.Register(
        nameof(AllowEditStoragePath),
        typeof(bool),
        typeof(ElectronicFilingDetailGrid),
        new PropertyMetadata(false, OnAllowEditStoragePathChanged));

    private static readonly DependencyPropertyKey IsPathReadOnlyPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsPathReadOnly),
        typeof(bool),
        typeof(ElectronicFilingDetailGrid),
        new PropertyMetadata(true));

    public static readonly DependencyProperty IsPathReadOnlyProperty = IsPathReadOnlyPropertyKey.DependencyProperty;

    public ElectronicFilingDetailGrid()
    {
        InitializeComponent();
        if (Resources["SelfProxy"] is BindingProxy proxy)
        {
            proxy.Data = this;
        }
    }

    /// <summary>明细分页头展开时显示表格。</summary>
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>立档存储路径是否按可编辑列展示。</summary>
    public bool AllowEditStoragePath
    {
        get => (bool)GetValue(AllowEditStoragePathProperty);
        set => SetValue(AllowEditStoragePathProperty, value);
    }

    /// <summary>整表只读。可编辑路径时关闭，其余列仍单独只读。</summary>
    public bool IsPathReadOnly => (bool)GetValue(IsPathReadOnlyProperty);

    private static void OnAllowEditStoragePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var grid = (ElectronicFilingDetailGrid)d;
        grid.SetValue(IsPathReadOnlyPropertyKey, !(bool)e.NewValue);
    }
}
