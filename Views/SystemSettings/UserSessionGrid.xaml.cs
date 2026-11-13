using System.Windows;
using DocMgr.Views.Shared;

namespace DocMgr.Views.SystemSettings;

/// <summary>在线用户与登录记录共用表。登录记录多一列结束时间。</summary>
public partial class UserSessionGrid : LedgerGridHost
{
    public static readonly DependencyProperty ShowEndTimeProperty = DependencyProperty.Register(
        nameof(ShowEndTime),
        typeof(bool),
        typeof(UserSessionGrid),
        new PropertyMetadata(false));

    public UserSessionGrid()
    {
        InitializeComponent();
        if (Resources["SelfProxy"] is BindingProxy proxy)
        {
            proxy.Data = this;
        }
    }

    /// <summary>是否显示结束时间。在线用户表不显示。</summary>
    public bool ShowEndTime
    {
        get => (bool)GetValue(ShowEndTimeProperty);
        set => SetValue(ShowEndTimeProperty, value);
    }
}
