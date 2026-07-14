using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Documents;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 资料归还工作台「查看借出详情」对话框：展示待归还出库明细并支持详单打印。
    /// </summary>
    public sealed class ArchiveReturnOutboundDetailViewModel : ViewModelBase
    {
        private readonly YearlyArchiveOutboundRecord _record;

        public ArchiveReturnOutboundDetailViewModel(YearlyArchiveOutboundRecord record)
        {
            _record = record ?? throw new ArgumentNullException(nameof(record));

            Items = new ObservableCollection<YearlyArchiveOutboundItem>(
                record.Items
                    .Where(ArchiveReturnItemDisplaySupport.IsReturnableOutboundItem)
                    .OrderBy(item => item.SortOrder)
                    .ThenBy(item => item.Id));

            ItemDetailsPanel = new ItemDetailsListPresenter<YearlyArchiveOutboundItem>(
                "待归还明细",
                summaryBuilder: items => ItemDetailsPanelSummarySupport.BuildTextColumnSummary(
                    items,
                    item => item.MaterialName,
                    "暂无待归还明细"));
            ItemDetailsPanel.RefreshItems(Items);

            PrintCommand = new RelayCommand(_ => PrintDetailList());
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());
        }

        public event Action? RequestClose;

        public ObservableCollection<YearlyArchiveOutboundItem> Items { get; }

        public ItemDetailsListPresenter<YearlyArchiveOutboundItem> ItemDetailsPanel { get; }

        public string WindowTitle => "借出详情";

        public string HeaderText => $"出库单 {_record.OutboundNo}";

        public string SubHeaderText =>
            $"借出人：{_record.ApplicantName}    部门：{_record.ApplicantDept}    状态：{_record.StatusStr}";

        public string SummaryText =>
            $"资料摘要：{(string.IsNullOrWhiteSpace(_record.MaterialSummary) ? "(无)" : _record.MaterialSummary.Trim())}";

        public string ExpectedReturnText =>
            $"应还日期：{(_record.ExpectedReturnDate?.ToString("yyyy-MM-dd") ?? "无")}";

        public string ProjectText
        {
            get
            {
                string year = _record.ArchiveYear?.ToString() ?? string.Empty;
                string project = _record.ProjectName?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(year) && string.IsNullOrWhiteSpace(project))
                {
                    return string.Empty;
                }

                if (string.IsNullOrWhiteSpace(year))
                {
                    return $"项目名称：{project}";
                }

                return string.IsNullOrWhiteSpace(project)
                    ? $"资料年度：{year}年"
                    : $"资料年度：{year}年    项目：{project}";
            }
        }

        public string ReasonText =>
            string.IsNullOrWhiteSpace(_record.Reason)
                ? string.Empty
                : $"借出原因：{_record.Reason.Trim()}";

        public RelayCommand PrintCommand { get; }

        public RelayCommand CloseCommand { get; }

        private void PrintDetailList()
        {
            var data = new ArchiveReturnOutboundDetailPrintData
            {
                OutboundNo = _record.OutboundNo,
                PrintDateText = DateTime.Now.ToString("yyyy-MM-dd"),
                BorrowerDept = _record.ApplicantDept,
                BorrowerName = _record.ApplicantName,
                ArchiveYearText = _record.ArchiveYear?.ToString() ?? string.Empty,
                ProjectName = _record.ProjectName?.Trim() ?? string.Empty,
                MaterialSummary = string.IsNullOrWhiteSpace(_record.MaterialSummary)
                    ? "(无)"
                    : _record.MaterialSummary.Trim(),
                ExpectedReturnDateText = _record.ExpectedReturnDate?.ToString("yyyy-MM-dd") ?? "无",
                Reason = _record.Reason?.Trim() ?? string.Empty,
                ItemLines = ArchiveOutboundItemDescription.BuildPrintDetailLines(Items).ToList()
            };

            FlowDocument document = ArchiveReturnOutboundDetailPrintDocumentFactory.Create(data);
            var previewWindow = new PrintPreviewWindow(document)
            {
                Owner = Application.Current.MainWindow
            };

            previewWindow.ShowDialog();
        }
    }
}
