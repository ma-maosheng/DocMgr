using System.Windows;
using DocMgr.Views.Shared;

namespace DocMgr.Views.NetworkTransfer;

/// <summary>出网申请列表。申请页显示明细数，审批页不显示。</summary>
public partial class NetworkOutboundRecordGrid : LedgerGridHost
{
    public static readonly DependencyProperty ShowItemCountProperty = DependencyProperty.Register(
        nameof(ShowItemCount),
        typeof(bool),
        typeof(NetworkOutboundRecordGrid),
        new PropertyMetadata(false));

    public NetworkOutboundRecordGrid()
    {
        InitializeComponent();
        if (Resources["SelfProxy"] is BindingProxy proxy)
        {
            proxy.Data = this;
        }
    }

    /// <summary>是否显示明细数列。</summary>
    public bool ShowItemCount
    {
        get => (bool)GetValue(ShowItemCountProperty);
        set => SetValue(ShowItemCountProperty, value);
    }
}
