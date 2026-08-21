using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 存档文本直办 Excel 导入预览。
    /// </summary>
    public sealed class StockTextArchiveExcelImportDialogViewModel : ViewModelBase
    {
        private readonly IStockTextArchiveDirectFilingService _filingService;
        private readonly IUserContextService _userContextService;
        private readonly IDialogService _dialogService;
        private readonly IReadOnlyList<StockTextArchiveExcelBoxDraft> _boxes;

        private bool _isBusy;
        private string _summaryText = "正在校验…";
        private bool _imported;

        public StockTextArchiveExcelImportDialogViewModel(
            IReadOnlyList<StockTextArchiveExcelBoxDraft> boxes,
            IStockTextArchiveDirectFilingService filingService,
            IUserContextService userContextService,
            IDialogService dialogService)
        {
            _boxes = boxes ?? Array.Empty<StockTextArchiveExcelBoxDraft>();
            _filingService = filingService;
            _userContextService = userContextService;
            _dialogService = dialogService;
            Rows = new ObservableCollection<StockTextArchiveExcelImportRowViewModel>();
            ConfirmCommand = new RelayCommand(async _ => await ConfirmAsync(), _ => !IsBusy && ImportableCount > 0);
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(_imported), _ => !IsBusy);
        }

        public ObservableCollection<StockTextArchiveExcelImportRowViewModel> Rows { get; }

        public ICommand ConfirmCommand { get; }

        public ICommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        public bool Imported => _imported;

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string SummaryText
        {
            get => _summaryText;
            private set => SetProperty(ref _summaryText, value);
        }

        public int ImportableCount => Rows.Count(item => item.CanImport);

        public async Task InitializeAsync()
        {
            IsBusy = true;
            using var progress = _dialogService.ShowOperationProgress("存档文本 Excel 导入", "正在校验档案盒…");
            try
            {
                var validations = await _filingService.ValidateExcelImportAsync(
                    _boxes,
                    _userContextService.CurrentUser,
                    new Progress<(int Current, int Total, string Status)>(item =>
                        progress.Report(item.Current, item.Total, item.Status)));
                Rows.Clear();
                foreach (var item in validations)
                {
                    Rows.Add(new StockTextArchiveExcelImportRowViewModel(item));
                }

                RefreshSummary();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ConfirmAsync()
        {
            var importable = Rows.Where(item => item.CanImport).Select(item => item.Box).ToList();
            if (importable.Count == 0)
            {
                _dialogService.ShowMessage("没有可通过校验的档案盒。");
                return;
            }

            if (!_dialogService.ShowConfirm(
                    $"将按表内物理位置导入 {importable.Count} 盒（跳过审批）。失败盒不会阻断后续盒。是否继续？",
                    "确认 Excel 导入立档"))
            {
                return;
            }

            IsBusy = true;
            StockTextArchiveExcelImportCommitResult result;
            try
            {
                using (var progress = _dialogService.ShowOperationProgress("存档文本 Excel 导入", "正在按盒立档…"))
                {
                    result = await _filingService.CommitExcelImportAsync(
                        importable,
                        _userContextService.CurrentUser,
                        new Progress<(int Current, int Total, string Status)>(item =>
                            progress.Report(item.Current, item.Total, item.Status)));
                }

                _imported = result.SucceededCount > 0;
                _dialogService.ShowMessage(result.Summary, "导入结果");
                RequestClose?.Invoke(_imported);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RefreshSummary()
        {
            int totalItems = Rows.Sum(item => item.ItemCount);
            SummaryText =
                $"共 {Rows.Count} 盒、{totalItems} 条子项；可通过校验 {ImportableCount} 盒。"
                + " 资料名称沿用表中值；子类按名称映射；档口使用表内档案盒编号。";
        }
    }

    /// <summary>
    /// Excel 导入预览行。
    /// </summary>
    public sealed class StockTextArchiveExcelImportRowViewModel
    {
        public StockTextArchiveExcelImportRowViewModel(StockTextArchiveExcelBoxValidation validation)
        {
            Box = validation.Box;
            SequenceNo = validation.Box.SequenceNo;
            Year = validation.Box.Year;
            ProjectName = validation.Box.ProjectName;
            MaterialName = validation.Box.MaterialName;
            BoxSpecification = validation.Box.BoxSpecification;
            Location = validation.Box.NormalizedBoxLocationCode;
            ItemCount = validation.Box.Items.Count;
            CanImport = validation.CanImport;
            StatusText = validation.CanImport ? "可导入" : "不可导入";
            ErrorText = validation.Errors.Count == 0
                ? string.Empty
                : string.Join("；", validation.Errors);
        }

        public StockTextArchiveExcelBoxDraft Box { get; }

        public int SequenceNo { get; }

        public string Year { get; }

        public string ProjectName { get; }

        public string MaterialName { get; }

        public string BoxSpecification { get; }

        public string Location { get; }

        public int ItemCount { get; }

        public bool CanImport { get; }

        public string StatusText { get; }

        public string ErrorText { get; }
    }
}
