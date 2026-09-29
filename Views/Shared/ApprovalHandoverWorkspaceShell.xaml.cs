using System.Windows;
using System.Windows.Controls;
using DocMgr.Models.Shared;

namespace DocMgr.Views.Shared
{
    /// <summary>
    /// 线下签批办理统一壳：业务头 / 签字区 / 中间步骤附加区插槽 + 四区附件 + 底栏。
    /// DataContext 为业务 ViewModel，须暴露壳契约属性（见各业务 VM 的 shell 别名）。
    /// </summary>
    public partial class ApprovalHandoverWorkspaceShell : UserControl
    {
        public static readonly DependencyProperty ShellKindProperty =
            DependencyProperty.Register(
                nameof(ShellKind),
                typeof(ApprovalWorkflowShellKind),
                typeof(ApprovalHandoverWorkspaceShell),
                new PropertyMetadata(ApprovalWorkflowShellKind.ApplicationHandover, OnShellKindChanged));

        public static readonly DependencyProperty HeaderContentProperty =
            DependencyProperty.Register(
                nameof(HeaderContent),
                typeof(object),
                typeof(ApprovalHandoverWorkspaceShell),
                new PropertyMetadata(null));

        public static readonly DependencyProperty SignatureContentProperty =
            DependencyProperty.Register(
                nameof(SignatureContent),
                typeof(object),
                typeof(ApprovalHandoverWorkspaceShell),
                new PropertyMetadata(null));

        public static readonly DependencyProperty MidStepExtraContentProperty =
            DependencyProperty.Register(
                nameof(MidStepExtraContent),
                typeof(object),
                typeof(ApprovalHandoverWorkspaceShell),
                new PropertyMetadata(null));

        public static readonly DependencyProperty SignatureExtraContentProperty =
            DependencyProperty.Register(
                nameof(SignatureExtraContent),
                typeof(object),
                typeof(ApprovalHandoverWorkspaceShell),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ShowTitleChromeProperty =
            DependencyProperty.Register(
                nameof(ShowTitleChrome),
                typeof(bool),
                typeof(ApprovalHandoverWorkspaceShell),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ShowFooterChromeProperty =
            DependencyProperty.Register(
                nameof(ShowFooterChrome),
                typeof(bool),
                typeof(ApprovalHandoverWorkspaceShell),
                new PropertyMetadata(true));

        /// <summary>
        /// 是否绘制主体白边框卡片。嵌入双模窗且外层已有表单卡片时关，避免「审批与交接信息」再套一层空卡。
        /// </summary>
        public static readonly DependencyProperty ShowBodyChromeProperty =
            DependencyProperty.Register(
                nameof(ShowBodyChrome),
                typeof(bool),
                typeof(ApprovalHandoverWorkspaceShell),
                new PropertyMetadata(true));

        /// <summary>
        /// 业务头是否撑满滚动视口（盘库双表按剩余高度布局时启用）。
        /// 默认关闭，避免离库等固定高度内容与审批卡之间出现大块空白。
        /// </summary>
        public static readonly DependencyProperty StretchHeaderToViewportProperty =
            DependencyProperty.Register(
                nameof(StretchHeaderToViewport),
                typeof(bool),
                typeof(ApprovalHandoverWorkspaceShell),
                new PropertyMetadata(false, OnStretchHeaderToViewportChanged));

        /// <summary>底栏「提交」按钮文案；盘库登记可设为「提交盘库信息」。</summary>
        public static readonly DependencyProperty SubmitButtonTextProperty =
            DependencyProperty.Register(
                nameof(SubmitButtonText),
                typeof(string),
                typeof(ApprovalHandoverWorkspaceShell),
                new PropertyMetadata(ApprovalWorkflowShellCopySupport.SubmitButtonText));

        /// <summary>
        /// 中间步骤按钮文案覆盖；非空时优先于 <see cref="ShellKind"/> 默认文案。
        /// 盘库登记可设为「确认可上传附件信息」。
        /// </summary>
        public static readonly DependencyProperty ConfirmMidStepButtonTextOverrideProperty =
            DependencyProperty.Register(
                nameof(ConfirmMidStepButtonTextOverride),
                typeof(string),
                typeof(ApprovalHandoverWorkspaceShell),
                new PropertyMetadata(null, OnConfirmMidStepButtonTextOverrideChanged));

        private static readonly DependencyPropertyKey ConfirmMidStepButtonTextPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(ConfirmMidStepButtonText),
                typeof(string),
                typeof(ApprovalHandoverWorkspaceShell),
                new PropertyMetadata(ApprovalWorkflowShellCopySupport.ConfirmPhysicalHandoverButtonText));

        public static readonly DependencyProperty ConfirmMidStepButtonTextProperty =
            ConfirmMidStepButtonTextPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey PrintButtonTextPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(PrintButtonText),
                typeof(string),
                typeof(ApprovalHandoverWorkspaceShell),
                new PropertyMetadata(ApprovalWorkflowShellCopySupport.PrintHandoverSheetButtonText));

        public static readonly DependencyProperty PrintButtonTextProperty =
            PrintButtonTextPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey SignedZoneTitlePropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(SignedZoneTitle),
                typeof(string),
                typeof(ApprovalHandoverWorkspaceShell),
                new PropertyMetadata(ApprovalWorkflowShellCopySupport.SignedHandoverZoneTitle));

        public static readonly DependencyProperty SignedZoneTitleProperty =
            SignedZoneTitlePropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey DefaultPhotoZoneTitlePropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(DefaultPhotoZoneTitle),
                typeof(string),
                typeof(ApprovalHandoverWorkspaceShell),
                new PropertyMetadata(ApprovalWorkflowShellCopySupport.PhysicalPhotoZoneTitle));

        public static readonly DependencyProperty DefaultPhotoZoneTitleProperty =
            DefaultPhotoZoneTitlePropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey ShowsDraftSubmitWithdrawPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(ShowsDraftSubmitWithdraw),
                typeof(bool),
                typeof(ApprovalHandoverWorkspaceShell),
                new PropertyMetadata(false));

        public static readonly DependencyProperty ShowsDraftSubmitWithdrawProperty =
            ShowsDraftSubmitWithdrawPropertyKey.DependencyProperty;

        public ApprovalHandoverWorkspaceShell()
        {
            InitializeComponent();
            ApplyShellKindTexts(ShellKind);
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            ApplyStretchHeaderToViewport(StretchHeaderToViewport);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            BodyScrollViewer.SizeChanged += OnBodyScrollViewerSizeChanged;
            ApplyStretchHeaderToViewport(StretchHeaderToViewport);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            BodyScrollViewer.SizeChanged -= OnBodyScrollViewerSizeChanged;
        }

        private void OnBodyScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (StretchHeaderToViewport)
            {
                SyncBodyContentMinHeight();
            }
        }

        public ApprovalWorkflowShellKind ShellKind
        {
            get => (ApprovalWorkflowShellKind)GetValue(ShellKindProperty);
            set => SetValue(ShellKindProperty, value);
        }

        public object? HeaderContent
        {
            get => GetValue(HeaderContentProperty);
            set => SetValue(HeaderContentProperty, value);
        }

        public object? SignatureContent
        {
            get => GetValue(SignatureContentProperty);
            set => SetValue(SignatureContentProperty, value);
        }

        public object? MidStepExtraContent
        {
            get => GetValue(MidStepExtraContentProperty);
            set => SetValue(MidStepExtraContentProperty, value);
        }

        public object? SignatureExtraContent
        {
            get => GetValue(SignatureExtraContentProperty);
            set => SetValue(SignatureExtraContentProperty, value);
        }

        /// <summary>是否显示顶部标题与流程说明（嵌入双模窗时可关，避免与外层标题重复）。</summary>
        public bool ShowTitleChrome
        {
            get => (bool)GetValue(ShowTitleChromeProperty);
            set => SetValue(ShowTitleChromeProperty, value);
        }

        /// <summary>是否显示办结提示条与底栏（嵌入页已有底栏时可关）。</summary>
        public bool ShowFooterChrome
        {
            get => (bool)GetValue(ShowFooterChromeProperty);
            set => SetValue(ShowFooterChromeProperty, value);
        }

        /// <summary>是否绘制主体白边框卡片（嵌入双模窗外层已有表单卡时可关）。</summary>
        public bool ShowBodyChrome
        {
            get => (bool)GetValue(ShowBodyChromeProperty);
            set => SetValue(ShowBodyChromeProperty, value);
        }

        /// <summary>业务头是否撑满滚动视口（盘库登记启用）。</summary>
        public bool StretchHeaderToViewport
        {
            get => (bool)GetValue(StretchHeaderToViewportProperty);
            set => SetValue(StretchHeaderToViewportProperty, value);
        }

        /// <summary>底栏提交按钮文案。</summary>
        public string SubmitButtonText
        {
            get => (string)GetValue(SubmitButtonTextProperty);
            set => SetValue(SubmitButtonTextProperty, value);
        }

        /// <summary>中间步骤按钮文案覆盖（非空优先）。</summary>
        public string? ConfirmMidStepButtonTextOverride
        {
            get => (string?)GetValue(ConfirmMidStepButtonTextOverrideProperty);
            set => SetValue(ConfirmMidStepButtonTextOverrideProperty, value);
        }

        public string ConfirmMidStepButtonText => (string)GetValue(ConfirmMidStepButtonTextProperty);
        public string PrintButtonText => (string)GetValue(PrintButtonTextProperty);
        public string SignedZoneTitle => (string)GetValue(SignedZoneTitleProperty);
        public string DefaultPhotoZoneTitle => (string)GetValue(DefaultPhotoZoneTitleProperty);
        public bool ShowsDraftSubmitWithdraw => (bool)GetValue(ShowsDraftSubmitWithdrawProperty);

        private static void OnShellKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ApprovalHandoverWorkspaceShell shell)
            {
                shell.ApplyShellKindTexts((ApprovalWorkflowShellKind)e.NewValue);
            }
        }

        private static void OnConfirmMidStepButtonTextOverrideChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is ApprovalHandoverWorkspaceShell shell)
            {
                shell.RefreshConfirmMidStepButtonText(shell.ShellKind);
            }
        }

        private static void OnStretchHeaderToViewportChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is ApprovalHandoverWorkspaceShell shell)
            {
                shell.ApplyStretchHeaderToViewport((bool)e.NewValue);
            }
        }

        private void ApplyShellKindTexts(ApprovalWorkflowShellKind kind)
        {
            RefreshConfirmMidStepButtonText(kind);
            SetValue(PrintButtonTextPropertyKey, ApprovalWorkflowShellCopySupport.GetPrintButtonText(kind));
            SetValue(SignedZoneTitlePropertyKey, ApprovalWorkflowShellCopySupport.GetSignedZoneTitle(kind));
            SetValue(
                DefaultPhotoZoneTitlePropertyKey,
                kind == ApprovalWorkflowShellKind.DisposalUnlockUpload
                    ? ApprovalWorkflowShellCopySupport.DiskPhotoZoneTitle
                    : ApprovalWorkflowShellCopySupport.PhysicalPhotoZoneTitle);
            SetValue(ShowsDraftSubmitWithdrawPropertyKey, ApprovalWorkflowShellCopySupport.ShowsDraftSubmitWithdraw(kind));
        }

        private void RefreshConfirmMidStepButtonText(ApprovalWorkflowShellKind kind)
        {
            string? overrideText = ConfirmMidStepButtonTextOverride;
            string text = string.IsNullOrWhiteSpace(overrideText)
                ? ApprovalWorkflowShellCopySupport.GetConfirmMidStepButtonText(kind)
                : overrideText.Trim();
            SetValue(ConfirmMidStepButtonTextPropertyKey, text);
        }

        private void ApplyStretchHeaderToViewport(bool stretch)
        {
            if (HeaderRow == null || BodyContentGrid == null)
            {
                return;
            }

            if (stretch)
            {
                HeaderRow.Height = new GridLength(1, GridUnitType.Star);
                SyncBodyContentMinHeight();
            }
            else
            {
                HeaderRow.Height = GridLength.Auto;
                BodyContentGrid.ClearValue(MinHeightProperty);
            }
        }

        private void SyncBodyContentMinHeight()
        {
            if (BodyScrollViewer == null || BodyContentGrid == null)
            {
                return;
            }

            double viewportHeight = BodyScrollViewer.ViewportHeight;
            if (viewportHeight > 0)
            {
                BodyContentGrid.MinHeight = viewportHeight;
            }
        }
    }
}
