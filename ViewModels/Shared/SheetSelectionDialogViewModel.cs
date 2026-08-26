using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Shared
{
    /// <summary>
    /// 工作表选择结果。
    /// </summary>
    public sealed class SheetSelectionResult
    {
        public SheetSelectionResult(string sheetName, bool expandItemsByTextLine)
        {
            SheetName = sheetName ?? string.Empty;
            ExpandItemsByTextLine = expandItemsByTextLine;
        }

        public string SheetName { get; }

        /// <summary>
        /// 勾选时按「子项名称」单元格内的文本行拆分资料子项；否则按 Excel 表格行导入。
        /// </summary>
        public bool ExpandItemsByTextLine { get; }
    }

    public class SheetSelectionDialogViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogService;
        private string _selectedSheet = string.Empty;
        private bool _expandItemsByTextLine;

        public SheetSelectionDialogViewModel(
            IEnumerable<string> sheetNames,
            IDialogService dialogService,
            bool showExpandItemsByTextLineOption = false)
        {
            _dialogService = dialogService;
            SheetNames = (sheetNames ?? Enumerable.Empty<string>()).ToList();
            _selectedSheet = SheetNames.FirstOrDefault() ?? string.Empty;
            ShowExpandItemsByTextLineOption = showExpandItemsByTextLineOption;

            ConfirmCommand = new RelayCommand(_ => Confirm(), _ => CanConfirm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public List<string> SheetNames { get; }

        /// <summary>
        /// 是否显示「以文本行为单位展开资料子项」（仅存档文本 Excel 导入需要）。
        /// </summary>
        public bool ShowExpandItemsByTextLineOption { get; }

        public string SelectedSheet
        {
            get => _selectedSheet;
            set
            {
                if (SetProperty(ref _selectedSheet, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// 以文本行为单位展开资料子项。
        /// </summary>
        public bool ExpandItemsByTextLine
        {
            get => _expandItemsByTextLine;
            set => SetProperty(ref _expandItemsByTextLine, value);
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        private bool CanConfirm()
            => !string.IsNullOrWhiteSpace(SelectedSheet);

        private void Confirm()
        {
            if (!CanConfirm())
            {
                _dialogService.ShowMessage("请选择一个有效的工作表！");
                return;
            }

            RequestClose?.Invoke(true);
        }
    }
}
