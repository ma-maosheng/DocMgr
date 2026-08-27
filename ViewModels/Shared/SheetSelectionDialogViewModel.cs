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
        /// 勾选时按内容单元格内的文本行拆分记录；否则按 Excel 表格行导入。
        /// </summary>
        public bool ExpandItemsByTextLine { get; }
    }

    public class SheetSelectionDialogViewModel : ViewModelBase
    {
        private const string DefaultExpandOptionContent = "以文本行为单位展开资料子项";
        private const string DefaultExpandOptionToolTip =
            "勾选后，「子项名称」单元格内每一非空文本行导入为一条资料子项；不勾选则以 Excel 表格行作为一条资料子项。";

        private readonly IDialogService _dialogService;
        private string _selectedSheet = string.Empty;
        private bool _expandItemsByTextLine;

        public SheetSelectionDialogViewModel(
            IEnumerable<string> sheetNames,
            IDialogService dialogService,
            bool showExpandItemsByTextLineOption = false,
            string? expandItemsByTextLineContent = null,
            string? expandItemsByTextLineToolTip = null)
        {
            _dialogService = dialogService;
            SheetNames = (sheetNames ?? Enumerable.Empty<string>()).ToList();
            _selectedSheet = SheetNames.FirstOrDefault() ?? string.Empty;
            ShowExpandItemsByTextLineOption = showExpandItemsByTextLineOption;
            ExpandItemsByTextLineContent = string.IsNullOrWhiteSpace(expandItemsByTextLineContent)
                ? DefaultExpandOptionContent
                : expandItemsByTextLineContent.Trim();
            ExpandItemsByTextLineToolTip = string.IsNullOrWhiteSpace(expandItemsByTextLineToolTip)
                ? DefaultExpandOptionToolTip
                : expandItemsByTextLineToolTip.Trim();

            ConfirmCommand = new RelayCommand(_ => Confirm(), _ => CanConfirm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public List<string> SheetNames { get; }

        /// <summary>
        /// 是否显示「以文本行为单位拆分」勾选（存档文本 / 其他资料导入等场景）。
        /// </summary>
        public bool ShowExpandItemsByTextLineOption { get; }

        /// <summary>
        /// 勾选框显示文案。
        /// </summary>
        public string ExpandItemsByTextLineContent { get; }

        /// <summary>
        /// 勾选框提示。
        /// </summary>
        public string ExpandItemsByTextLineToolTip { get; }

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
        /// 以文本行为单位拆分内容字段。
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
