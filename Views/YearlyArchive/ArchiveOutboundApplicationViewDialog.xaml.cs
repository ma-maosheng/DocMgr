using System.Windows;

namespace DocMgr.Views.YearlyArchive
{
    /// <summary>
    /// 资料借出申请只读查看窗口：仅展示申请信息、审批信息、出库明细与附件，支持打印与关闭。
    /// </summary>
    public partial class ArchiveOutboundApplicationViewDialog : Window
    {
        public ArchiveOutboundApplicationViewDialog()
        {
            InitializeComponent();
        }
    }
}
