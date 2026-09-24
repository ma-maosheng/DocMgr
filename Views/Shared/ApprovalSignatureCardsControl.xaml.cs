using System.Windows;
using System.Windows.Controls;

namespace DocMgr.Views.Shared
{
    /// <summary>
    /// 统一审批签字卡片：审核（部门/资料室/生产科）与审批（分管资料/分管生产）双列紧凑卡，
    /// 字段值绑定宿主 ViewModel；提示文案与是否显示审批意见由本控件依赖属性控制。
    /// </summary>
    public partial class ApprovalSignatureCardsControl : UserControl
    {
        private const string DefaultSignatureHintText =
            "审批通过后按业务规则填入责任人姓名；签字日期可改，不能早于签批单首次打印日期。";

        public static readonly DependencyProperty SignatureHintTextProperty =
            DependencyProperty.Register(
                nameof(SignatureHintText),
                typeof(string),
                typeof(ApprovalSignatureCardsControl),
                new PropertyMetadata(DefaultSignatureHintText));

        public static readonly DependencyProperty ShowApprovalOpinionProperty =
            DependencyProperty.Register(
                nameof(ShowApprovalOpinion),
                typeof(bool),
                typeof(ApprovalSignatureCardsControl),
                new PropertyMetadata(true));

        public ApprovalSignatureCardsControl()
        {
            InitializeComponent();
        }

        /// <summary>签字区顶部提示文案。</summary>
        public string SignatureHintText
        {
            get => (string)GetValue(SignatureHintTextProperty);
            set => SetValue(SignatureHintTextProperty, value);
        }

        /// <summary>是否显示底部审批意见行（默认 true）。</summary>
        public bool ShowApprovalOpinion
        {
            get => (bool)GetValue(ShowApprovalOpinionProperty);
            set => SetValue(ShowApprovalOpinionProperty, value);
        }
    }
}
