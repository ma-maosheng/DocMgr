using System.Windows;
using System.Windows.Controls;

namespace DocMgr.Views.Shared;

/// <summary>
/// 建档申请与入网申请共用的「1. 申请信息」表头。
/// </summary>
public partial class ApplicationRequestHeaderSectionView : UserControl
{
    /// <summary>建档申请在表头中追加的身份字段（资料名称、库管模式等）。</summary>
    public static readonly DependencyProperty IdentityContentProperty = DependencyProperty.Register(
        nameof(IdentityContent),
        typeof(object),
        typeof(ApplicationRequestHeaderSectionView),
        new PropertyMetadata(null));

    public ApplicationRequestHeaderSectionView()
    {
        InitializeComponent();
    }

    /// <summary>宿主追加的表头内容。入网申请不使用。</summary>
    public object? IdentityContent
    {
        get => GetValue(IdentityContentProperty);
        set => SetValue(IdentityContentProperty, value);
    }
}
