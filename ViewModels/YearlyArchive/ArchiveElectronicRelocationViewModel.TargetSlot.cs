using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;
using System.Collections.ObjectModel;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 电子介质迁档：物理落位档口（单 ComboBox + 推荐 + 快照）。
    /// </summary>
    public sealed partial class ArchiveElectronicRelocationViewModel
    {
        private HardDiskMediaReturnTargetLocationOption? _selectedTargetSlotLocationOption;
        private string _targetFullLocation = string.Empty;
        private string _targetCellOccupancyText = "-";
        private bool _suppressTargetSlotResolve;

        /// <summary>可选目标档口（专用档口键，含空余盘位展示）。</summary>
        public ObservableCollection<HardDiskMediaReturnTargetLocationOption> TargetSlotLocationOptions { get; } = new();

        /// <summary>当前选中的目标档口选项。</summary>
        public HardDiskMediaReturnTargetLocationOption? SelectedTargetSlotLocationOption
        {
            get => _selectedTargetSlotLocationOption;
            set
            {
                if (!SetProperty(ref _selectedTargetSlotLocationOption, value))
                {
                    return;
                }

                OnPropertyChanged(nameof(CanShowTargetSlotSnapshot));
                if (_suppressTargetSlotResolve)
                {
                    return;
                }

                _ = ResolveTargetFullLocationFromSelectedOptionAsync();
            }
        }

        /// <summary>解析后的完整目标位置（含档内序号）。</summary>
        public string TargetFullLocation
        {
            get => _targetFullLocation;
            private set
            {
                if (SetProperty(ref _targetFullLocation, value))
                {
                    OnPropertyChanged(nameof(IsTargetSameSlotAsSource));
                    OnPropertyChanged(nameof(TargetSlotValidationMessage));
                    OnPropertyChanged(nameof(CanShowTargetSlotSnapshot));
                    RefreshDisplayItems();
                }
            }
        }

        /// <summary>档口占用说明。</summary>
        public string TargetCellOccupancyText
        {
            get => _targetCellOccupancyText;
            private set => SetProperty(ref _targetCellOccupancyText, value);
        }

        /// <summary>
        /// 迁入空白载体时，是否默认保持源档口（仅当源档口用途与目标类别一致）。
        /// </summary>
        private bool ShouldPreferSourceTargetSlot
        {
            get
            {
                if (!IsMoveToBlankCarrierMode || SourceSummary == null)
                {
                    return false;
                }

                if (!TryResolveTargetSlotCategoryName(out string targetCategory, out _))
                {
                    return false;
                }

                string? sourceCategory = ResolveSourceSlotCategoryName();
                return !string.IsNullOrWhiteSpace(sourceCategory)
                    && CabinetHardDiskSlotCategoryAssignment.MatchesCategory(sourceCategory, targetCategory);
            }
        }

        /// <summary>档口选择区标题。</summary>
        public string TargetLocationPanelTitle => ShouldPreferSourceTargetSlot
            ? "资料迁入后存放位置（默认保持原档口）："
            : "新存放位置：";

        /// <summary>推荐按钮文案旁的用途提示。</summary>
        public string TargetSlotRecommendHintText
        {
            get
            {
                if (!TryResolveTargetSlotCategoryName(
                        out string categoryName,
                        out _,
                        out var materialKind,
                        out var mediaKind))
                {
                    return "请先选择源电子介质袋后再推荐档口。";
                }

                string display = ArchiveElectronicStorageSlotCategorySupport.ResolveCategoryDisplayName(categoryName);
                string materialDisplay = ArchiveElectronicStorageSlotCategorySupport.ResolveMaterialKindDisplayName(materialKind);
                string mediaDisplay = ArchiveElectronicStorageSlotCategorySupport.ResolveMediaKindDisplayName(mediaKind);
                return $"按资料类型「{materialDisplay}」+ 介质类型「{mediaDisplay}」推荐（{display}）。";
            }
        }

        public bool IsTargetSameSlotAsSource =>
            !string.IsNullOrWhiteSpace(SourceCurrentLocation)
            && !string.IsNullOrWhiteSpace(TargetFullLocation)
            && ArchiveSlotLocationSupport.IsSameSlot(SourceCurrentLocation, TargetFullLocation);

        public string TargetSlotValidationMessage =>
            IsPhysicalMode && IsTargetSameSlotAsSource
                ? "新档口与当前档口相同，同一档口内的物理迁移没有必要。"
                : string.Empty;

        public bool CanShowTargetSlotSnapshot =>
            (IsPhysicalMode || IsMoveToBlankCarrierMode)
            && (!string.IsNullOrWhiteSpace(SelectedTargetSlotLocationOption?.Location)
                || !string.IsNullOrWhiteSpace(TargetFullLocation));

        public bool CanRecommendTargetSlot =>
            !IsBusy && HasSource && (IsPhysicalMode || IsMoveToBlankCarrierMode);

        private async Task ResetTargetSlotSelectionAsync(bool preferSourceLocation)
        {
            TargetFullLocation = string.Empty;
            TargetCellOccupancyText = "-";
            await LoadTargetSlotLocationOptionsAsync(preferSourceLocation);
            OnPropertyChanged(nameof(TargetLocationPanelTitle));
            OnPropertyChanged(nameof(TargetSlotRecommendHintText));
            OnPropertyChanged(nameof(CanRecommendTargetSlot));
        }

        private void ClearTargetSlotSelection()
        {
            TargetSlotLocationOptions.Clear();
            SyncSelectedTargetSlotLocationOption(null);
            TargetFullLocation = string.Empty;
            TargetCellOccupancyText = "-";
            OnPropertyChanged(nameof(TargetLocationPanelTitle));
            OnPropertyChanged(nameof(TargetSlotRecommendHintText));
            OnPropertyChanged(nameof(CanRecommendTargetSlot));
        }

        private async Task LoadTargetSlotLocationOptionsAsync(bool preferSourceLocation)
        {
            if (SourceSummary == null || (!IsPhysicalMode && !IsMoveToBlankCarrierMode))
            {
                ClearTargetSlotSelection();
                return;
            }

            if (!TryResolveTargetSlotCategoryName(out string categoryName, out string categoryError))
            {
                ClearTargetSlotSelection();
                TargetCellOccupancyText = categoryError;
                return;
            }

            IReadOnlyList<HardDiskMediaReturnTargetLocationOption> options;
            try
            {
                options = await _hardDiskMediaService.GetDedicatedTargetLocationOptionsAsync(categoryName);
            }
            catch (Exception ex)
            {
                ClearTargetSlotSelection();
                TargetCellOccupancyText = $"加载专用档口失败：{ex.Message}";
                _dialogService.ShowError(ex.Message, "加载目标档口失败");
                return;
            }

            var ordered = options
                .OrderBy(item => item.ExistingMediumCount)
                .ThenBy(item => item.Location, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string sourceLocation = SourceCurrentLocation;
            if (preferSourceLocation
                && ShouldPreferSourceTargetSlot
                && !string.IsNullOrWhiteSpace(sourceLocation)
                && !ordered.Any(item => ArchiveSlotLocationSupport.IsSameSlot(item.Location, sourceLocation)))
            {
                ordered.Insert(0, new HardDiskMediaReturnTargetLocationOption
                {
                    Location = ArchiveSlotLocationSupport.BuildSlotKey(sourceLocation),
                    ExistingMediumCount = 0,
                    SlotCapacity = 0
                });
            }

            string? previousLocation = SelectedTargetSlotLocationOption?.Location ?? TargetFullLocation;
            ReplaceTargetSlotLocationOptions(ordered);

            HardDiskMediaReturnTargetLocationOption? selected = null;
            if (preferSourceLocation && ShouldPreferSourceTargetSlot && !string.IsNullOrWhiteSpace(sourceLocation))
            {
                selected = FindTargetSlotLocationOption(TargetSlotLocationOptions, sourceLocation);
            }
            else if (!string.IsNullOrWhiteSpace(previousLocation))
            {
                selected = FindTargetSlotLocationOption(TargetSlotLocationOptions, previousLocation);
            }

            selected ??= TargetSlotLocationOptions.FirstOrDefault();
            if (selected == null)
            {
                SyncSelectedTargetSlotLocationOption(null);
                TargetFullLocation = string.Empty;
                string display = ArchiveElectronicStorageSlotCategorySupport.ResolveCategoryDisplayName(categoryName);
                TargetCellOccupancyText = $"未找到“{display}档口”，请先在磁盘柜开柜界面完成设置。";
                return;
            }

            if (preferSourceLocation
                && ShouldPreferSourceTargetSlot
                && !string.IsNullOrWhiteSpace(sourceLocation)
                && ArchiveSlotLocationSupport.IsSameSlot(selected.Location, sourceLocation))
            {
                SyncSelectedTargetSlotLocationOption(selected.Location);
                TargetFullLocation = sourceLocation.Trim();
                TargetCellOccupancyText = "默认保持原档口与原袋内序号；如需调整，请重新选择目标档口。";
                return;
            }

            SyncSelectedTargetSlotLocationOption(selected.Location);
            await ResolveTargetFullLocationFromSelectedOptionAsync();
        }

        private async Task RecommendTargetSlotAsync()
        {
            if (!CanRecommendTargetSlot || SourceSummary == null)
            {
                _dialogService.ShowMessage("请先选择源电子介质袋。", "推荐档口");
                return;
            }

            if (!TryResolveTargetSlotCategoryName(out string categoryName, out string categoryError))
            {
                _dialogService.ShowMessage(categoryError, "推荐档口");
                return;
            }

            string categoryDisplay = ArchiveElectronicStorageSlotCategorySupport.ResolveCategoryDisplayName(categoryName);

            try
            {
                IsBusy = true;
                await LoadTargetSlotLocationOptionsAsync(preferSourceLocation: false);

                string? recommendedFull = await _hardDiskMediaService.AllocateNextDedicatedFullLocationAsync(categoryName);
                if (IsPhysicalMode
                    && !string.IsNullOrWhiteSpace(recommendedFull)
                    && ArchiveSlotLocationSupport.IsSameSlot(SourceCurrentLocation, recommendedFull))
                {
                    recommendedFull = null;
                    foreach (var option in TargetSlotLocationOptions
                                 .OrderBy(item => item.ExistingMediumCount)
                                 .ThenBy(item => item.Location, StringComparer.OrdinalIgnoreCase))
                    {
                        if (ArchiveSlotLocationSupport.IsSameSlot(SourceCurrentLocation, option.Location)
                            || option.RemainingCapacity <= 0)
                        {
                            continue;
                        }

                        recommendedFull = await ResolveFullLocationInSlotAsync(option.Location, excludeSourceUnit: false);
                        if (!string.IsNullOrWhiteSpace(recommendedFull))
                        {
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(recommendedFull))
                {
                    _dialogService.ShowMessage(
                        $"未找到仍有容量的“{categoryDisplay}档口”，请先在磁盘柜开柜界面完成设置或腾出空位。",
                        "推荐档口");
                    return;
                }

                var matched = FindTargetSlotLocationOption(TargetSlotLocationOptions, recommendedFull);
                if (matched == null)
                {
                    matched = new HardDiskMediaReturnTargetLocationOption
                    {
                        Location = ArchiveSlotLocationSupport.BuildSlotKey(recommendedFull),
                        ExistingMediumCount = 0,
                        SlotCapacity = 0
                    };
                    TargetSlotLocationOptions.Insert(0, matched);
                }

                _suppressTargetSlotResolve = true;
                try
                {
                    SyncSelectedTargetSlotLocationOption(matched.Location);
                }
                finally
                {
                    _suppressTargetSlotResolve = false;
                }

                TargetFullLocation = recommendedFull.Trim();
                TargetCellOccupancyText = ShouldPreferSourceTargetSlot
                    && ArchiveSlotLocationSupport.IsSameSlot(SourceCurrentLocation, recommendedFull)
                    ? "保持原档口与原袋内序号。"
                    : $"已推荐 {recommendedFull.Trim()}。";

                _dialogService.ShowMessage($"已推荐{categoryDisplay}档口：{recommendedFull.Trim()}", "推荐档口");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message, "推荐档口失败");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ResolveTargetFullLocationFromSelectedOptionAsync()
        {
            if (SelectedTargetSlotLocationOption == null || SourceSummary == null)
            {
                TargetFullLocation = string.Empty;
                TargetCellOccupancyText = "-";
                return;
            }

            string slotLocation = SelectedTargetSlotLocationOption.Location?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(slotLocation))
            {
                TargetFullLocation = string.Empty;
                TargetCellOccupancyText = "-";
                return;
            }

            if (ShouldPreferSourceTargetSlot && ArchiveSlotLocationSupport.IsSameSlot(SourceCurrentLocation, slotLocation))
            {
                TargetFullLocation = SourceCurrentLocation.Trim();
                TargetCellOccupancyText = "保持原档口与原袋内序号。";
                return;
            }

            try
            {
                string? fullLocation = await ResolveFullLocationInSlotAsync(
                    slotLocation,
                    excludeSourceUnit: IsMoveToBlankCarrierMode);
                if (string.IsNullOrWhiteSpace(fullLocation))
                {
                    TargetFullLocation = string.Empty;
                    TargetCellOccupancyText = "目标档口已满或无法分配序号。";
                    return;
                }

                TargetFullLocation = fullLocation;
                if (!ArchiveSlotLocationSupport.TryParseSlotLocation(
                        fullLocation,
                        out string cabinetName,
                        out string side,
                        out int row,
                        out int column))
                {
                    TargetCellOccupancyText = string.Empty;
                    return;
                }

                int cellCount = await _filingService.GetElectronicUnitCountInCellAsync(cabinetName, side, row, column);
                ArchiveSlotLocationSupport.TryParseSequenceIndex(fullLocation, out int sequence);
                TargetCellOccupancyText = $"目标格内现有 {cellCount} 袋，迁入后将使用序号 {sequence:D2}";
            }
            catch (Exception ex)
            {
                TargetFullLocation = string.Empty;
                TargetCellOccupancyText = "位置计算失败";
                _dialogService.ShowError($"计算目标档口失败：{ex.Message}", "错误");
            }
        }

        private async Task<string?> ResolveFullLocationInSlotAsync(string slotLocation, bool excludeSourceUnit)
        {
            if (!ArchiveSlotLocationSupport.TryParseSlotLocation(
                    slotLocation,
                    out string cabinetName,
                    out string side,
                    out int row,
                    out int column))
            {
                return null;
            }

            int? excludeUnitId = excludeSourceUnit ? SourceSummary?.ContainerId : null;
            int sequence = await _filingService.GetMinimumAvailableElectronicSequenceInCellAsync(
                cabinetName,
                side,
                row,
                column,
                excludeUnitId);
            return ArchiveSlotLocationSupport.BuildFullElectronicLocation(cabinetName, side, row, column, sequence);
        }

        private void ShowTargetSlotSnapshot()
        {
            string? location = !string.IsNullOrWhiteSpace(TargetFullLocation)
                ? TargetFullLocation
                : SelectedTargetSlotLocationOption?.Location;
            if (string.IsNullOrWhiteSpace(location))
            {
                _dialogService.ShowMessage("请先选择或推荐目标档口后再查看快照。", "档口快照");
                return;
            }

            if (!TryShowSlotSnapshotByLocation(location))
            {
                _dialogService.ShowMessage("当前档口无法解析，请重新选择后再查看快照。", "档口快照");
            }
        }

        private bool TryShowSlotSnapshotByLocation(string? location)
        {
            if (string.IsNullOrWhiteSpace(location)
                || !ArchiveSlotLocationSupport.TryParseSlotLocation(
                    location,
                    out string cabinetName,
                    out string side,
                    out int row,
                    out int column))
            {
                return false;
            }

            var cabinet = _magneticCabinets.FirstOrDefault(item =>
                string.Equals(item.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
            if (cabinet == null)
            {
                return false;
            }

            CabinetFace face = string.Equals(side, "B", StringComparison.OrdinalIgnoreCase)
                ? CabinetFace.B
                : CabinetFace.A;

            _dialogService.ShowCabinetOpenDialog(new CabinetOpenRequest
            {
                CabinetId = cabinet.Id,
                CabinetName = cabinet.Name,
                CabinetType = cabinet.Type,
                Face = face,
                LayerCount = cabinet.LayerCount,
                ColumnCount = cabinet.ColumnCount,
                TargetSlotCode = $"{row}-{column}",
                WidthCm = cabinet.Width,
                HeightCm = cabinet.Height,
                DepthCm = cabinet.Depth
            });
            return true;
        }

        /// <summary>
        /// 按资料类型（年度/历史/损坏，取自源档口）与介质类型（硬盘/光盘，取自迁档模式或源载体）解析目标专用档口。
        /// </summary>
        private bool TryResolveTargetSlotCategoryName(out string categoryName, out string message)
            => TryResolveTargetSlotCategoryName(out categoryName, out message, out _, out _);

        private bool TryResolveTargetSlotCategoryName(
            out string categoryName,
            out string message,
            out ArchiveElectronicStorageSlotCategorySupport.SlotMaterialKind materialKind,
            out ArchiveElectronicStorageSlotCategorySupport.SlotMediaKind mediaKind)
        {
            categoryName = string.Empty;
            message = string.Empty;
            materialKind = ArchiveElectronicStorageSlotCategorySupport.SlotMaterialKind.YearlyData;
            mediaKind = ArchiveElectronicStorageSlotCategorySupport.SlotMediaKind.HardDisk;

            if (SourceSummary == null)
            {
                message = "请先选择源电子介质袋。";
                return false;
            }

            try
            {
                mediaKind = ResolveTargetSlotMediaKind();
                materialKind = ResolveSourceSlotMaterialKind(mediaKind);
                categoryName = ArchiveElectronicStorageSlotCategorySupport.ResolveDedicatedSlotCategory(
                    materialKind,
                    mediaKind);
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 目标介质类型：迁入空白硬盘/光盘取目标载体；物理迁移取源袋载体。
        /// </summary>
        private ArchiveElectronicStorageSlotCategorySupport.SlotMediaKind ResolveTargetSlotMediaKind()
        {
            if (IsMoveToBlankHardDiskMode)
            {
                return ArchiveElectronicStorageSlotCategorySupport.SlotMediaKind.HardDisk;
            }

            if (IsMoveToBlankOpticalDiscMode)
            {
                return ArchiveElectronicStorageSlotCategorySupport.SlotMediaKind.OpticalDisc;
            }

            return ArchiveElectronicStorageSlotCategorySupport.ResolveMediaKindFromCarrierType(
                SourceSummary?.StorageCarrierType);
        }

        /// <summary>
        /// 资料类型优先取源档口已配置类别；迁入空白载体时损坏档口按年度数据处理（新介质非损坏）。
        /// </summary>
        private ArchiveElectronicStorageSlotCategorySupport.SlotMaterialKind ResolveSourceSlotMaterialKind(
            ArchiveElectronicStorageSlotCategorySupport.SlotMediaKind targetMediaKind)
        {
            string? sourceCategory = ResolveSourceSlotCategoryName();
            if (ArchiveElectronicStorageSlotCategorySupport.TryResolveMaterialAndMediaKind(
                    sourceCategory,
                    out var materialKind,
                    out _))
            {
                if (materialKind == ArchiveElectronicStorageSlotCategorySupport.SlotMaterialKind.Damaged
                    && IsMoveToBlankCarrierMode)
                {
                    return ArchiveElectronicStorageSlotCategorySupport.SlotMaterialKind.YearlyData;
                }

                return materialKind;
            }

            // 源档口未配置时：物理迁移按载体默认年度；损坏仅在硬盘介质且无法从档口判定时无法在此获知台账状态。
            _ = targetMediaKind;
            return ArchiveElectronicStorageSlotCategorySupport.SlotMaterialKind.YearlyData;
        }

        private string? ResolveSourceSlotCategoryName()
        {
            string location = SourceCurrentLocation;
            if (string.IsNullOrWhiteSpace(location)
                || !ArchiveSlotLocationSupport.TryParseSlotLocation(
                    location,
                    out string cabinetName,
                    out string faceCode,
                    out int row,
                    out int column))
            {
                return null;
            }

            var cabinet = _magneticCabinets.FirstOrDefault(item =>
                string.Equals(item.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
            if (cabinet == null)
            {
                return null;
            }

            return _cabinetService.GetHardDiskDedicatedSlotCategoryName(
                cabinet.Id,
                faceCode,
                $"{row}-{column}");
        }

        private bool TryApplySelectedTargetLocation(ElectronicRelocationRequest request, out string message)
        {
            if (string.IsNullOrWhiteSpace(TargetFullLocation))
            {
                message = ShouldPreferSourceTargetSlot && !string.IsNullOrWhiteSpace(SourceCurrentLocation)
                    ? string.Empty
                    : "请完整选择新的存放档口。";
                if (string.IsNullOrWhiteSpace(message))
                {
                    request.NewStorageLocation = SourceCurrentLocation.Trim();
                    return true;
                }

                return false;
            }

            if (IsPhysicalMode && IsTargetSameSlotAsSource)
            {
                message = TargetSlotValidationMessage;
                return false;
            }

            request.NewStorageLocation = TargetFullLocation.Trim();
            message = string.Empty;
            return true;
        }

        private void ReplaceTargetSlotLocationOptions(IReadOnlyList<HardDiskMediaReturnTargetLocationOption> options)
        {
            TargetSlotLocationOptions.Clear();
            foreach (var option in options)
            {
                TargetSlotLocationOptions.Add(option);
            }
        }

        private void SyncSelectedTargetSlotLocationOption(string? location)
        {
            var matched = FindTargetSlotLocationOption(TargetSlotLocationOptions, location);
            if (ReferenceEquals(_selectedTargetSlotLocationOption, matched))
            {
                OnPropertyChanged(nameof(CanShowTargetSlotSnapshot));
                return;
            }

            _suppressTargetSlotResolve = true;
            try
            {
                _selectedTargetSlotLocationOption = matched;
                OnPropertyChanged(nameof(SelectedTargetSlotLocationOption));
                OnPropertyChanged(nameof(CanShowTargetSlotSnapshot));
            }
            finally
            {
                _suppressTargetSlotResolve = false;
            }
        }

        private static HardDiskMediaReturnTargetLocationOption? FindTargetSlotLocationOption(
            IEnumerable<HardDiskMediaReturnTargetLocationOption> options,
            string? location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            return options.FirstOrDefault(item => ArchiveSlotLocationSupport.IsSameSlot(item.Location, location));
        }
    }
}
