using System.Windows;
using System.Windows.Controls;

namespace DocMgr.Views.Shared
{
    /// <summary>
    /// 办结后查看窗附件区：列表 + 增补提示（绑定 DataContext 的 Attachments / CanSupplementOtherAttachments / SupplementAttachmentHint / ViewAttachmentCommand）。
    /// </summary>
    public partial class ApprovalCompletedAttachmentPanel : UserControl
    {
        public ApprovalCompletedAttachmentPanel()
        {
            InitializeComponent();
        }
    }
}
