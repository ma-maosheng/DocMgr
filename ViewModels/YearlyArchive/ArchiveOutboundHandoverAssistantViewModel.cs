using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Documents;
using DocMgr.Models.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.YearlyArchive
{
    public sealed class ArchiveOutboundHandoverAssistantViewModel : ViewModelBase
    {
        private readonly YearlyArchiveOutboundRecord _record;

        public ArchiveOutboundHandoverAssistantViewModel(
            YearlyArchiveOutboundRecord record,
            IEnumerable<ArchiveOutboundHandoverAssistantRowViewModel> rows)
        {
            _record = record ?? throw new ArgumentNullException(nameof(record));

            Items = new ObservableCollection<ArchiveOutboundHandoverAssistantRowViewModel>(rows);
            GroupedItems = CollectionViewSource.GetDefaultView(Items);
            GroupedItems.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ArchiveOutboundHandoverAssistantRowViewModel.Category)));

            PrintCommand = new RelayCommand(_ => PrintChecklist());
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());
        }

        public event Action? RequestClose;

        public ObservableCollection<ArchiveOutboundHandoverAssistantRowViewModel> Items { get; }

        public ICollectionView GroupedItems { get; }

        public string WindowTitle => "资料出库业务助手";

        public string HeaderText => $"申请单 { _record.OutboundNo }";

        public string SubHeaderText =>
            $"领用人：{_record.ApplicantName}    部门：{_record.ApplicantDept}    状态：{_record.StatusStr}";

        public string IntroText =>
            "请资料室资料管理员在办理实物出库时逐项核对以下事项，勾选确认后可打印留存。";

        public RelayCommand PrintCommand { get; }

        public RelayCommand CloseCommand { get; }

        private void PrintChecklist()
        {
            var data = new ArchiveOutboundHandoverAssistantPrintData
            {
                OutboundNo = _record.OutboundNo,
                ApplicantName = _record.ApplicantName,
                ApplicantDept = _record.ApplicantDept,
                MaterialSummary = string.IsNullOrWhiteSpace(_record.MaterialSummary)
                    ? "(无)"
                    : _record.MaterialSummary.Trim(),
                Lines = Items
                    .Select(item => new ArchiveOutboundHandoverAssistantPrintLine
                    {
                        Category = item.Category,
                        Text = item.Text,
                        IsChecked = item.IsChecked
                    })
                    .ToList()
            };

            FlowDocument document = ArchiveOutboundHandoverAssistantPrintDocumentFactory.Create(data);
            var previewWindow = new PrintPreviewWindow(document)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            previewWindow.ShowDialog();
        }
    }
}
