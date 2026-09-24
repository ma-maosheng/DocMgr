using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DocMgr.Views.Shared
{
    /// <summary>
    /// 审批办理壳附件分区。
    /// 可上传：标题 + 上传按钮 + 附件表（可选 ZoneHint 补充说明）。
    /// 无需上传：标题 +「无需上传」+ 一条说明（ZoneHint 优先，否则 PlaceholderText），不显示空表。
    /// </summary>
    public partial class ApprovalAttachmentZoneControl : UserControl
    {
        public static readonly DependencyProperty ZoneTitleProperty =
            DependencyProperty.Register(
                nameof(ZoneTitle),
                typeof(string),
                typeof(ApprovalAttachmentZoneControl),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ZoneHintProperty =
            DependencyProperty.Register(
                nameof(ZoneHint),
                typeof(string),
                typeof(ApprovalAttachmentZoneControl),
                new PropertyMetadata(string.Empty, OnSkipReasonSourcesChanged));

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(
                nameof(PlaceholderText),
                typeof(string),
                typeof(ApprovalAttachmentZoneControl),
                new PropertyMetadata(string.Empty, OnSkipReasonSourcesChanged));

        public static readonly DependencyProperty AttachmentsProperty =
            DependencyProperty.Register(
                nameof(Attachments),
                typeof(IEnumerable),
                typeof(ApprovalAttachmentZoneControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ShowUploadButtonsProperty =
            DependencyProperty.Register(
                nameof(ShowUploadButtons),
                typeof(bool),
                typeof(ApprovalAttachmentZoneControl),
                new PropertyMetadata(true, OnSkipReasonSourcesChanged));

        /// <summary>
        /// 兼容旧绑定；界面是否显示附件表已由 <see cref="ShowUploadButtons"/> 统一决定。
        /// </summary>
        public static readonly DependencyProperty ShowAttachmentListProperty =
            DependencyProperty.Register(
                nameof(ShowAttachmentList),
                typeof(bool),
                typeof(ApprovalAttachmentZoneControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty CanUploadProperty =
            DependencyProperty.Register(
                nameof(CanUpload),
                typeof(bool),
                typeof(ApprovalAttachmentZoneControl),
                new PropertyMetadata(false));

        public static readonly DependencyProperty UploadCommandProperty =
            DependencyProperty.Register(
                nameof(UploadCommand),
                typeof(ICommand),
                typeof(ApprovalAttachmentZoneControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty CaptureCommandProperty =
            DependencyProperty.Register(
                nameof(CaptureCommand),
                typeof(ICommand),
                typeof(ApprovalAttachmentZoneControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ViewCommandProperty =
            DependencyProperty.Register(
                nameof(ViewCommand),
                typeof(ICommand),
                typeof(ApprovalAttachmentZoneControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty DeleteCommandProperty =
            DependencyProperty.Register(
                nameof(DeleteCommand),
                typeof(ICommand),
                typeof(ApprovalAttachmentZoneControl),
                new PropertyMetadata(null));

        private static readonly DependencyPropertyKey SkipUploadReasonTextPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(SkipUploadReasonText),
                typeof(string),
                typeof(ApprovalAttachmentZoneControl),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SkipUploadReasonTextProperty =
            SkipUploadReasonTextPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey ShowActiveZoneHintPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(ShowActiveZoneHint),
                typeof(bool),
                typeof(ApprovalAttachmentZoneControl),
                new PropertyMetadata(false));

        public static readonly DependencyProperty ShowActiveZoneHintProperty =
            ShowActiveZoneHintPropertyKey.DependencyProperty;

        public ApprovalAttachmentZoneControl()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                RefreshDerivedState();
                SyncAttachmentGridWidth();
            };
        }

        private void OnZoneSizeChanged(object sender, SizeChangedEventArgs e)
        {
            SyncAttachmentGridWidth();
        }

        /// <summary>
        /// DataGrid 默认按列内容收缩；显式对齐所属卡片内容区实际宽度。
        /// </summary>
        private void SyncAttachmentGridWidth()
        {
            if (AttachmentGrid == null || ContentHost == null)
            {
                return;
            }

            double width = ContentHost.ActualWidth;
            if (width > 0 && !double.IsNaN(width) && !double.IsInfinity(width))
            {
                AttachmentGrid.Width = width;
            }
        }

        public string ZoneTitle
        {
            get => (string)GetValue(ZoneTitleProperty);
            set => SetValue(ZoneTitleProperty, value);
        }

        public string ZoneHint
        {
            get => (string)GetValue(ZoneHintProperty);
            set => SetValue(ZoneHintProperty, value);
        }

        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public IEnumerable? Attachments
        {
            get => (IEnumerable?)GetValue(AttachmentsProperty);
            set => SetValue(AttachmentsProperty, value);
        }

        public bool ShowUploadButtons
        {
            get => (bool)GetValue(ShowUploadButtonsProperty);
            set => SetValue(ShowUploadButtonsProperty, value);
        }

        public bool ShowAttachmentList
        {
            get => (bool)GetValue(ShowAttachmentListProperty);
            set => SetValue(ShowAttachmentListProperty, value);
        }

        public bool CanUpload
        {
            get => (bool)GetValue(CanUploadProperty);
            set => SetValue(CanUploadProperty, value);
        }

        public ICommand? UploadCommand
        {
            get => (ICommand?)GetValue(UploadCommandProperty);
            set => SetValue(UploadCommandProperty, value);
        }

        public ICommand? CaptureCommand
        {
            get => (ICommand?)GetValue(CaptureCommandProperty);
            set => SetValue(CaptureCommandProperty, value);
        }

        public ICommand? ViewCommand
        {
            get => (ICommand?)GetValue(ViewCommandProperty);
            set => SetValue(ViewCommandProperty, value);
        }

        public ICommand? DeleteCommand
        {
            get => (ICommand?)GetValue(DeleteCommandProperty);
            set => SetValue(DeleteCommandProperty, value);
        }

        /// <summary>无需上传时展示的唯一说明文案。</summary>
        public string SkipUploadReasonText => (string)GetValue(SkipUploadReasonTextProperty);

        /// <summary>可上传态下是否显示 ZoneHint（补充提示，避免与无需上传说明重复）。</summary>
        public bool ShowActiveZoneHint => (bool)GetValue(ShowActiveZoneHintProperty);

        private static void OnSkipReasonSourcesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ApprovalAttachmentZoneControl control)
            {
                control.RefreshDerivedState();
            }
        }

        private void RefreshDerivedState()
        {
            string hint = ZoneHint?.Trim() ?? string.Empty;
            string placeholder = PlaceholderText?.Trim() ?? string.Empty;
            string reason = !string.IsNullOrWhiteSpace(hint)
                ? hint
                : (!string.IsNullOrWhiteSpace(placeholder) ? placeholder : "本分区无需上传附件。");

            SetValue(SkipUploadReasonTextPropertyKey, reason);
            SetValue(
                ShowActiveZoneHintPropertyKey,
                ShowUploadButtons && !string.IsNullOrWhiteSpace(hint));
        }
    }
}
