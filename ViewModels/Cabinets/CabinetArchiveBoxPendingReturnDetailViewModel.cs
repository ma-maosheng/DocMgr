using DocMgr.Models.Cabinets;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;

namespace DocMgr.ViewModels.Cabinets
{
    /// <summary>
    /// 模拟档案盒「待还资料详情」对话框：追溯盒内已办结出库、尚未归还的提档明细。
    /// </summary>
    public sealed class CabinetArchiveBoxPendingReturnDetailViewModel : ViewModelBase
    {
        public CabinetArchiveBoxPendingReturnDetailViewModel(
            string boxCode,
            string boxLabel,
            int pendingReturnCopyCount,
            IReadOnlyList<SimulatedArchiveBoxPendingReturnDetailRow> details)
        {
            if (string.IsNullOrWhiteSpace(boxCode))
            {
                throw new ArgumentException("档案盒位置编号不能为空。", nameof(boxCode));
            }

            ArgumentNullException.ThrowIfNull(details);

            BoxCode = boxCode.Trim();
            BoxLabel = boxLabel?.Trim() ?? string.Empty;
            PendingReturnCopyCount = Math.Max(0, pendingReturnCopyCount);

            ItemDetailsPanel = new ItemDetailsListPresenter<SimulatedArchiveBoxPendingReturnDetailRow>(
                "待还资料明细",
                summaryBuilder: items => ItemDetailsPanelSummarySupport.BuildTextColumnSummary(
                    items,
                    item => item.OutboundNo,
                    "暂无待还资料明细"));
            ItemDetailsPanel.RefreshItems(details);

            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());
        }

        public event Action? RequestClose;

        public string BoxCode { get; }

        public string BoxLabel { get; }

        public int PendingReturnCopyCount { get; }

        public ItemDetailsListPresenter<SimulatedArchiveBoxPendingReturnDetailRow> ItemDetailsPanel { get; }

        public string WindowTitle => "待还资料详情";

        public string HeaderText => string.IsNullOrWhiteSpace(BoxLabel)
            ? $"档案盒 {BoxCode}"
            : $"档案盒 {BoxCode}（{BoxLabel}）";

        public string SummaryText =>
            $"待还份数合计 {PendingReturnCopyCount} 份，追溯明细 {ItemDetailsPanel.ItemCount} 条（已办结出库、尚未归还的提档记录）。";

        public string ListHintText =>
            "以下为本盒内资料子项出库待还明细，便于资料室管理员追溯借出单号、借用人、应还日期等信息。";

        public RelayCommand CloseCommand { get; }
    }
}
