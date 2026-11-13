using System.Windows;
using DocMgr.Views.Shared;

namespace DocMgr.Views.YearlyArchive;

/// <summary>电子目录/文件明细。资料详情里的普通展示与筛选勾选共用。</summary>
public partial class ElectronicContentEntryGrid : LedgerGridHost
{
    public static readonly DependencyProperty ShowFilterSelectionProperty = DependencyProperty.Register(
        nameof(ShowFilterSelection),
        typeof(bool),
        typeof(ElectronicContentEntryGrid),
        new PropertyMetadata(false));

    public ElectronicContentEntryGrid()
    {
        InitializeComponent();
        if (Resources["SelfProxy"] is BindingProxy proxy)
        {
            proxy.Data = this;
        }
    }

    /// <summary>是否显示筛选勾选列。</summary>
    public bool ShowFilterSelection
    {
        get => (bool)GetValue(ShowFilterSelectionProperty);
        set => SetValue(ShowFilterSelectionProperty, value);
    }
}
