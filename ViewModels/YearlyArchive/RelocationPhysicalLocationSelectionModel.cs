using DocMgr.Models.Cabinets;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace DocMgr.ViewModels.YearlyArchive
{
    public enum RelocationPhysicalLocationKind
    {
        SimulatedArchiveBox,
        ElectronicArchiveUnit
    }

    public sealed class RelocationPhysicalLocationSelectionModel : ViewModelBase
    {
        private readonly RelocationPhysicalLocationKind _kind;
        private readonly ICabinetService _cabinetService;
        private readonly IArchiveFilingService _filingService;
        private readonly IDialogService _dialogService;

        private Cabinet? _selectedCabinet;
        private string _selectedSide = string.Empty;
        private string _selectedRow = string.Empty;
        private string _selectedColumn = string.Empty;
        private string _currentSourceLocation = string.Empty;
        private string _newLocationPreview = string.Empty;
        private string _cellOccupancyText = "-";
        private bool _isLocationReady;
        private int _resolvedCellCount;
        private int _resolvedSequenceIndex;
        private bool _useMoveToEmptyRules;
        private int? _moveToEmptySourceUnitId;

        public RelocationPhysicalLocationSelectionModel(
            RelocationPhysicalLocationKind kind,
            ICabinetService cabinetService,
            IArchiveFilingService filingService,
            IDialogService dialogService)
        {
            _kind = kind;
            _cabinetService = cabinetService;
            _filingService = filingService;
            _dialogService = dialogService;

            ShowSlotSnapshotCommand = new RelayCommand(_ => ShowSlotSnapshot(), _ => CanShowSlotSnapshot);
        }

        public ObservableCollection<Cabinet> Cabinets { get; } = new();

        public ObservableCollection<string> Sides { get; } = new();

        public ObservableCollection<string> Rows { get; } = new();

        public ObservableCollection<string> Columns { get; } = new();

        public RelayCommand ShowSlotSnapshotCommand { get; }

        public string LocationPanelTitle => _useMoveToEmptyRules
            ? "资料迁入后存放位置（默认保持原档口）"
            : _kind == RelocationPhysicalLocationKind.SimulatedArchiveBox
                ? "新存放档口"
                : "新存放位置";

        public string CurrentSourceLocationDisplay => string.IsNullOrWhiteSpace(CurrentSourceLocation)
            ? "当前档口：尚未选择源容器"
            : $"当前档口：{CurrentSourceLocation}";

        public bool HasCurrentSourceLocation => !string.IsNullOrWhiteSpace(CurrentSourceLocation);

        public string CurrentSourceLocation
        {
            get => _currentSourceLocation;
            set
            {
                if (SetProperty(ref _currentSourceLocation, value))
                {
                    OnPropertyChanged(nameof(CurrentSourceLocationDisplay));
                    OnPropertyChanged(nameof(HasCurrentSourceLocation));
                    OnPropertyChanged(nameof(IsSameSlotAsSource));
                }
            }
        }

        public string NewLocationPreview
        {
            get => _newLocationPreview;
            private set
            {
                if (SetProperty(ref _newLocationPreview, value))
                {
                    OnPropertyChanged(nameof(IsSameSlotAsSource));
                    OnPropertyChanged(nameof(SlotValidationMessage));
                }
            }
        }

        public string CellOccupancyText
        {
            get => _cellOccupancyText;
            private set => SetProperty(ref _cellOccupancyText, value);
        }

        public bool IsLocationReady
        {
            get => _isLocationReady;
            private set => SetProperty(ref _isLocationReady, value);
        }

        public bool IsSameSlotAsSource =>
            ArchiveSlotLocationSupport.IsSameSlot(CurrentSourceLocation, NewLocationPreview);

        public string SlotValidationMessage => !_useMoveToEmptyRules && IsSameSlotAsSource
            ? "新档口与当前档口相同，同一档口内的物理迁移没有必要。"
            : string.Empty;

        public bool CanShowSlotSnapshot =>
            SelectedCabinet != null
            && !string.IsNullOrWhiteSpace(SelectedSide)
            && !string.IsNullOrWhiteSpace(SelectedRow)
            && !string.IsNullOrWhiteSpace(SelectedColumn);

        public Cabinet? SelectedCabinet
        {
            get => _selectedCabinet;
            set
            {
                if (SetProperty(ref _selectedCabinet, value))
                {
                    UpdateSides();
                    UpdateRowsAndColumns();
                    _ = RefreshLocationPreviewAsync();
                    OnPropertyChanged(nameof(CanShowSlotSnapshot));
                }
            }
        }

        public string SelectedSide
        {
            get => _selectedSide;
            set
            {
                if (SetProperty(ref _selectedSide, value))
                {
                    _ = RefreshLocationPreviewAsync();
                    OnPropertyChanged(nameof(CanShowSlotSnapshot));
                }
            }
        }

        public string SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (SetProperty(ref _selectedRow, value))
                {
                    _ = RefreshLocationPreviewAsync();
                    OnPropertyChanged(nameof(CanShowSlotSnapshot));
                }
            }
        }

        public string SelectedColumn
        {
            get => _selectedColumn;
            set
            {
                if (SetProperty(ref _selectedColumn, value))
                {
                    _ = RefreshLocationPreviewAsync();
                    OnPropertyChanged(nameof(CanShowSlotSnapshot));
                }
            }
        }

        public async Task LoadCabinetsAsync()
        {
            var allCabinets = await _cabinetService.GetAllCabinetsAsync();
            var cabinetItems = _kind == RelocationPhysicalLocationKind.ElectronicArchiveUnit
                ? CabinetSelectionSupport.BuildElectronicMagneticCabinetItems(allCabinets)
                : CabinetSelectionSupport.BuildSimulatedArchiveCabinetItems(allCabinets);

            Cabinets.Clear();
            foreach (var cabinet in cabinetItems)
            {
                Cabinets.Add(cabinet);
            }

            ApplyDefaultCabinetSelectionIfNeeded();
        }

        public void ApplyDefaultCabinetSelectionIfNeeded()
        {
            if (_useMoveToEmptyRules || SelectedCabinet != null || Cabinets.Count == 0)
            {
                return;
            }

            SelectedCabinet = Cabinets[0];
        }

        public void ResetTargetSelection()
        {
            SelectedCabinet = null;
            ReplaceItems(Sides, Array.Empty<string>());
            ReplaceItems(Rows, Array.Empty<string>());
            ReplaceItems(Columns, Array.Empty<string>());
            SelectedSide = string.Empty;
            SelectedRow = string.Empty;
            SelectedColumn = string.Empty;
            NewLocationPreview = string.Empty;
            CellOccupancyText = "-";
            IsLocationReady = false;
            _resolvedCellCount = 0;
            _resolvedSequenceIndex = 0;
            ApplyDefaultCabinetSelectionIfNeeded();
        }

        public void ConfigureForMoveToEmpty(bool enabled, int? sourceUnitId = null)
        {
            _useMoveToEmptyRules = enabled;
            _moveToEmptySourceUnitId = enabled ? sourceUnitId : null;
            OnPropertyChanged(nameof(SlotValidationMessage));
            OnPropertyChanged(nameof(LocationPanelTitle));
        }

        public void InitializeFromSourceLocation(string sourceLocation, int? sourceUnitId = null)
        {
            CurrentSourceLocation = sourceLocation;
            _moveToEmptySourceUnitId = sourceUnitId;
            ResetTargetSelection();

            if (!_useMoveToEmptyRules || string.IsNullOrWhiteSpace(sourceLocation))
            {
                return;
            }

            NewLocationPreview = sourceLocation.Trim();
            CellOccupancyText = "默认保持原档口与原袋内序号；如需调整，请重新选择目标档口。";
            IsLocationReady = true;
            HydrateSelectionFromLocation(sourceLocation);
        }

        public bool TryApplyToMoveToEmptyRequest(ElectronicRelocationRequest request, out string message)
        {
            if (string.IsNullOrWhiteSpace(CurrentSourceLocation))
            {
                message = "请先选择源电子介质袋。";
                return false;
            }

            if (!IsLocationReady || string.IsNullOrWhiteSpace(NewLocationPreview))
            {
                request.NewStorageLocation = CurrentSourceLocation.Trim();
                message = string.Empty;
                return true;
            }

            request.NewStorageLocation = NewLocationPreview.Trim();
            message = string.Empty;
            return true;
        }

        public bool TryValidateForPhysicalMove(out string message)
        {
            if (!IsLocationReady || string.IsNullOrWhiteSpace(NewLocationPreview))
            {
                message = "请完整选择新的存放档口。";
                return false;
            }

            if (IsSameSlotAsSource)
            {
                message = SlotValidationMessage;
                return false;
            }

            message = string.Empty;
            return true;
        }

        public bool TryApplyToSimulatedRequest(SimulatedRelocationRequest request, out string message)
        {
            if (!TryValidateForPhysicalMove(out message))
            {
                return false;
            }

            if (!TryParseSelectedSlot(out int row, out int column))
            {
                message = "请选择有效的柜位信息。";
                return false;
            }

            request.NewStorageLocation = NewLocationPreview;
            request.NewCabinetName = SelectedCabinet?.Name ?? string.Empty;
            request.NewSide = SelectedSide;
            request.NewRow = row;
            request.NewColumn = column;
            request.NewBoxIndex = _resolvedSequenceIndex;
            return true;
        }

        public bool TryApplyToElectronicRequest(ElectronicRelocationRequest request, out string message)
        {
            if (!TryValidateForPhysicalMove(out message))
            {
                return false;
            }

            request.NewStorageLocation = NewLocationPreview;
            return true;
        }

        private async Task RefreshLocationPreviewAsync()
        {
            if (SelectedCabinet == null
                || string.IsNullOrWhiteSpace(SelectedSide)
                || !TryParseSelectedSlot(out int row, out int column))
            {
                NewLocationPreview = string.Empty;
                CellOccupancyText = "-";
                IsLocationReady = false;
                return;
            }

            try
            {
                if (_useMoveToEmptyRules && _kind == RelocationPhysicalLocationKind.ElectronicArchiveUnit)
                {
                    await RefreshMoveToEmptyLocationPreviewAsync(row, column);
                    return;
                }

                if (_kind == RelocationPhysicalLocationKind.SimulatedArchiveBox)
                {
                    _resolvedCellCount = await _filingService.GetBoxCountInCellAsync(
                        SelectedCabinet.Name,
                        SelectedSide,
                        row,
                        column);
                    _resolvedSequenceIndex = await _filingService.GetMinimumAvailableBoxSequenceInCellAsync(
                        SelectedCabinet.Name,
                        SelectedSide,
                        row,
                        column);
                    NewLocationPreview = $"{SelectedCabinet.Name}{SelectedSide}-{row}-{column}-{_resolvedSequenceIndex:D2}";
                    CellOccupancyText = $"目标格内现有 {_resolvedCellCount} 盒，迁入后将使用序号 {_resolvedSequenceIndex:D2}";
                }
                else
                {
                    _resolvedCellCount = await _filingService.GetElectronicUnitCountInCellAsync(
                        SelectedCabinet.Name,
                        SelectedSide,
                        row,
                        column);
                    _resolvedSequenceIndex = await _filingService.GetMinimumAvailableElectronicSequenceInCellAsync(
                        SelectedCabinet.Name,
                        SelectedSide,
                        row,
                        column);
                    NewLocationPreview = $"{SelectedCabinet.Name}{SelectedSide}-{row}-{column}-{_resolvedSequenceIndex:D2}";
                    CellOccupancyText = $"目标格内现有 {_resolvedCellCount} 袋，迁入后将使用序号 {_resolvedSequenceIndex:D2}";
                }

                IsLocationReady = true;
            }
            catch (Exception ex)
            {
                NewLocationPreview = string.Empty;
                CellOccupancyText = "位置计算失败";
                IsLocationReady = false;
                _dialogService.ShowError($"计算目标档口失败：{ex.Message}", "错误");
            }
        }

        private bool TryParseSelectedSlot(out int row, out int column)
        {
            row = 0;
            column = 0;
            return int.TryParse(SelectedRow, out row) && int.TryParse(SelectedColumn, out column);
        }

        private void UpdateSides()
        {
            ReplaceItems(Sides, Array.Empty<string>());
            if (SelectedCabinet == null)
            {
                return;
            }

            Sides.Add("A");
            if (SelectedCabinet.FaceCount > 1)
            {
                Sides.Add("B");
            }

            SelectedSide = Sides.FirstOrDefault() ?? string.Empty;
        }

        private void UpdateRowsAndColumns()
        {
            ReplaceItems(Rows, Array.Empty<string>());
            ReplaceItems(Columns, Array.Empty<string>());
            if (SelectedCabinet == null)
            {
                return;
            }

            for (int i = 1; i <= SelectedCabinet.LayerCount; i++)
            {
                Rows.Add(i.ToString());
            }

            for (int i = 1; i <= SelectedCabinet.ColumnCount; i++)
            {
                Columns.Add(i.ToString());
            }

            SelectedRow = Rows.FirstOrDefault() ?? string.Empty;
            SelectedColumn = Columns.FirstOrDefault() ?? string.Empty;
        }

        private async Task RefreshMoveToEmptyLocationPreviewAsync(int row, int column)
        {
            if (SelectedCabinet == null || string.IsNullOrWhiteSpace(SelectedSide))
            {
                NewLocationPreview = string.Empty;
                CellOccupancyText = "-";
                IsLocationReady = false;
                return;
            }

            string slotKey = ArchiveSlotLocationSupport.BuildSlotKey(SelectedCabinet.Name, SelectedSide, row, column);
            if (ArchiveSlotLocationSupport.IsSameSlot(CurrentSourceLocation, slotKey))
            {
                NewLocationPreview = CurrentSourceLocation.Trim();
                CellOccupancyText = "保持原档口与原袋内序号。";
                IsLocationReady = true;
                return;
            }

            int minSequence = await _filingService.GetMinimumAvailableElectronicSequenceInCellAsync(
                SelectedCabinet.Name,
                SelectedSide,
                row,
                column,
                _moveToEmptySourceUnitId);
            NewLocationPreview = ArchiveSlotLocationSupport.BuildFullElectronicLocation(
                SelectedCabinet.Name,
                SelectedSide,
                row,
                column,
                minSequence);
            CellOccupancyText = $"目标档口将使用最小编号 [{minSequence:D2}]。";
            IsLocationReady = true;
        }

        private void HydrateSelectionFromLocation(string sourceLocation)
        {
            if (!ArchiveSlotLocationSupport.TryParseSlotLocation(
                    sourceLocation,
                    out string cabinetName,
                    out string side,
                    out int row,
                    out int column))
            {
                return;
            }

            SelectedCabinet = Cabinets.FirstOrDefault(item =>
                string.Equals(item.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
            if (SelectedCabinet == null)
            {
                return;
            }

            if (Sides.Contains(side))
            {
                SelectedSide = side;
            }

            string rowText = row.ToString();
            if (Rows.Contains(rowText))
            {
                SelectedRow = rowText;
            }

            string columnText = column.ToString();
            if (Columns.Contains(columnText))
            {
                SelectedColumn = columnText;
            }
        }

        private void ShowSlotSnapshot()
        {
            if (!CanShowSlotSnapshot || !TryParseSelectedSlot(out int row, out int column) || SelectedCabinet == null)
            {
                return;
            }

            CabinetFace face = string.Equals(SelectedSide, "B", StringComparison.OrdinalIgnoreCase)
                ? CabinetFace.B
                : CabinetFace.A;

            _dialogService.ShowCabinetOpenDialog(new CabinetOpenRequest
            {
                CabinetId = SelectedCabinet.Id,
                CabinetName = SelectedCabinet.Name,
                CabinetType = SelectedCabinet.Type,
                Face = face,
                LayerCount = SelectedCabinet.LayerCount,
                ColumnCount = SelectedCabinet.ColumnCount,
                TargetSlotCode = $"{row}-{column}",
                WidthCm = SelectedCabinet.Width,
                HeightCm = SelectedCabinet.Height,
                DepthCm = SelectedCabinet.Depth
            });
        }

        private static void ReplaceItems(ObservableCollection<string> target, IReadOnlyList<string> source)
        {
            target.Clear();
            foreach (var item in source)
            {
                target.Add(item);
            }
        }
    }
}
