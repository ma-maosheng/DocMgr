using System.Windows;
using DocMgr.Views.Shared;

namespace DocMgr.Views.YearlyArchive;

/// <summary>
/// 流转台账一级容器表。模拟页与电子页共用，仅编号列和位置列标题不同。
/// </summary>
public partial class CirculationLedgerContainerGrid : LedgerGridHost
{
    public static readonly DependencyProperty CodeColumnHeaderProperty = DependencyProperty.Register(
        nameof(CodeColumnHeader),
        typeof(string),
        typeof(CirculationLedgerContainerGrid),
        new PropertyMetadata("档案盒编号"));

    public static readonly DependencyProperty LocationColumnHeaderProperty = DependencyProperty.Register(
        nameof(LocationColumnHeader),
        typeof(string),
        typeof(CirculationLedgerContainerGrid),
        new PropertyMetadata("当前位置"));

    public CirculationLedgerContainerGrid()
    {
        InitializeComponent();
        if (Resources["SelfProxy"] is BindingProxy proxy)
        {
            proxy.Data = this;
        }
    }

    /// <summary>容器编号列标题，例如档案盒编号、介质袋编号。</summary>
    public string CodeColumnHeader
    {
        get => (string)GetValue(CodeColumnHeaderProperty);
        set => SetValue(CodeColumnHeaderProperty, value);
    }

    /// <summary>位置列标题，例如当前位置、存放位置。</summary>
    public string LocationColumnHeader
    {
        get => (string)GetValue(LocationColumnHeaderProperty);
        set => SetValue(LocationColumnHeaderProperty, value);
    }
}
