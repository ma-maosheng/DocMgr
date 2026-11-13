using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 选择一块可并入的非空硬盘。同一项目名称、同一实施年度只影响本弹窗列表，确认后才写回立档页。
    /// </summary>
    public sealed class AppendHardDiskSelectionDialogViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogService;
        private readonly IReadOnlyList<AppendHardDiskPickerRow> _rows;
        private bool _sameProjectOnly;
        private bool _sameYearOnly;
        private string _keyword = string.Empty;
        private AppendHardDiskPickerRow? _selectedRow;

        public AppendHardDiskSelectionDialogViewModel(
            IDialogService dialogService,
            IReadOnlyList<AppendHardDiskPickerRow> rows,
            bool sameProjectOnly,
            bool sameYearOnly,
            int? selectedUnitId)
        {
            _dialogService = dialogService;
            _rows = rows ?? Array.Empty<AppendHardDiskPickerRow>();
            _sameProjectOnly = sameProjectOnly;
            _sameYearOnly = sameYearOnly;
            VisibleRows = new ObservableCollection<AppendHardDiskPickerRow>();
            ConfirmCommand = new RelayCommand(_ => Confirm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
            ApplyFilter(selectedUnitId);
        }

        public ObservableCollection<AppendHardDiskPickerRow> VisibleRows { get; }

        public bool SameProjectOnly
        {
            get => _sameProjectOnly;
            set
            {
                if (SetProperty(ref _sameProjectOnly, value))
                {
                    ApplyFilter(SelectedRow?.UnitId);
                }
            }
        }

        public bool SameYearOnly
        {
            get => _sameYearOnly;
            set
            {
                if (SetProperty(ref _sameYearOnly, value))
                {
                    ApplyFilter(SelectedRow?.UnitId);
                }
            }
        }

        public string Keyword
        {
            get => _keyword;
            set
            {
                if (SetProperty(ref _keyword, value))
                {
                    ApplyFilter(SelectedRow?.UnitId);
                }
            }
        }

        public AppendHardDiskPickerRow? SelectedRow
        {
            get => _selectedRow;
            set => SetProperty(ref _selectedRow, value);
        }

        public AppendHardDiskSelectionResult? Result { get; private set; }

        public ICommand ConfirmCommand { get; }

        public ICommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        private void Confirm()
        {
            if (SelectedRow is not { CanSelect: true })
            {
                _dialogService.ShowMessage("请选择一块可并入的非空硬盘。");
                return;
            }

            Result = new AppendHardDiskSelectionResult
            {
                UnitId = SelectedRow.UnitId,
                SameProjectOnly = SameProjectOnly,
                SameYearOnly = SameYearOnly
            };
            RequestClose?.Invoke(true);
        }

        private void ApplyFilter(int? preferredUnitId)
        {
            string keyword = Keyword?.Trim() ?? string.Empty;
            List<AppendHardDiskPickerRow> visible = _rows
                .Where(Include)
                .Where(row => MatchesKeyword(row, keyword))
                .OrderByDescending(row => row.CanHoldPendingBatch)
                .ThenByDescending(row => row.BelongsToCurrentProject)
                .ThenByDescending(row => row.BelongsToCurrentYear)
                .ThenByDescending(row => row.AvailableCapacityMb)
                .ThenBy(row => row.ElectronicArchiveNo, StringComparer.OrdinalIgnoreCase)
                .ToList();

            VisibleRows.Clear();
            foreach (AppendHardDiskPickerRow row in visible)
            {
                VisibleRows.Add(row);
            }

            AppendHardDiskPickerRow? preferred = preferredUnitId is int unitId
                ? visible.FirstOrDefault(row => row.UnitId == unitId && row.CanSelect)
                : null;
            SelectedRow = preferred
                ?? visible.FirstOrDefault(row => row.CanSelect && row.CanHoldPendingBatch);
        }

        private bool Include(AppendHardDiskPickerRow row)
        {
            if (row.IsPredetermined)
            {
                return true;
            }

            if (SameProjectOnly && !row.MatchesProjectFilter)
            {
                return false;
            }

            if (SameYearOnly && !row.MatchesYearFilter)
            {
                return false;
            }

            return true;
        }

        private static bool MatchesKeyword(AppendHardDiskPickerRow row, string keyword)
        {
            if (keyword.Length == 0)
            {
                return true;
            }

            return Contains(row.DiskCode, keyword)
                || Contains(row.ElectronicArchiveNo, keyword)
                || Contains(row.YearProjectText, keyword)
                || Contains(row.StorageLocation, keyword)
                || Contains(row.Remarks, keyword)
                || Contains(row.GroupLabel, keyword);
        }

        private static bool Contains(string? value, string keyword) =>
            value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;
    }
}
