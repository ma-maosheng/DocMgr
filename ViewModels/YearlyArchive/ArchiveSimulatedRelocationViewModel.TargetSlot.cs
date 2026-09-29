using DocMgr.Models.Cabinets;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;
using System.Collections.ObjectModel;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 模拟介质迁档：物理落位档口（单 ComboBox + 推荐 + 快照）。
    /// </summary>
    public sealed partial class ArchiveSimulatedRelocationViewModel
    {
        private ArchiveBoxTargetLocationOption? _selectedTargetSlotLocationOption;
        private string _targetFullLocation = string.Empty;
        private string _targetCellOccupancyText = "-";
        private int _resolvedTargetBoxIndex;
        private bool _suppressTargetSlotResolve;

        /// <summary>可选目标档口（年度资料专用/混用档口，含已有盒数）。</summary>
        public ObservableCollection<ArchiveBoxTargetLocationOption> TargetSlotLocationOptions { get; } = new();

        /// <summary>当前选中的目标档口选项。</summary>
        public ArchiveBoxTargetLocationOption? SelectedTargetSlotLocationOption
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

        /// <summary>解析后的完整目标位置（含盒内序号）。</summary>
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

        public string TargetLocationPanelTitle => "新存放档口：";

        public string TargetSlotRecommendHintText
        {
            get
            {
                string specification = ResolveBoxSpecificationForTargetSlot();
                return string.IsNullOrWhiteSpace(specification)
                    ? "请先选择源档案盒（迁入空盒时请先选择新盒规格）后再推荐档口。"
                    : $"按盒规格「{specification}」推荐年度资料可用档口（含混用档口）。";
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
            IsPhysicalMode
            && (!string.IsNullOrWhiteSpace(SelectedTargetSlotLocationOption?.Location)
                || !string.IsNullOrWhiteSpace(TargetFullLocation));

        public bool CanRecommendTargetSlot =>
            !IsBusy && HasSource && IsPhysicalMode && !string.IsNullOrWhiteSpace(ResolveBoxSpecificationForTargetSlot());

        private async Task ResetTargetSlotSelectionAsync()
        {
            TargetFullLocation = string.Empty;
            TargetCellOccupancyText = "-";
            _resolvedTargetBoxIndex = 0;
            await LoadTargetSlotLocationOptionsAsync();
            OnPropertyChanged(nameof(TargetSlotRecommendHintText));
            OnPropertyChanged(nameof(CanRecommendTargetSlot));
        }

        private void ClearTargetSlotSelection()
        {
            TargetSlotLocationOptions.Clear();
            SyncSelectedTargetSlotLocationOption(null);
            TargetFullLocation = string.Empty;
            TargetCellOccupancyText = "-";
            _resolvedTargetBoxIndex = 0;
            OnPropertyChanged(nameof(TargetSlotRecommendHintText));
            OnPropertyChanged(nameof(CanRecommendTargetSlot));
        }

        private async Task LoadTargetSlotLocationOptionsAsync(bool preferSuggestedSelection = false)
        {
            if (SourceSummary == null || !IsPhysicalMode)
            {
                ClearTargetSlotSelection();
                return;
            }

            string specification = ResolveBoxSpecificationForTargetSlot();
            if (string.IsNullOrWhiteSpace(specification))
            {
                ClearTargetSlotSelection();
                TargetCellOccupancyText = "无法确定盒规格，请检查源档案盒或新盒规格。";
                return;
            }

            IReadOnlyList<ArchiveBoxTargetLocationOption> options;
            try
            {
                options = await _filingService.GetArchiveBoxTargetLocationOptionsAsync(
                    SourceSummary.ProjectName,
                    SourceSummary.Year,
                    specification);
            }
            catch (Exception ex)
            {
                ClearTargetSlotSelection();
                TargetCellOccupancyText = $"加载档口失败：{ex.Message}";
                _dialogService.ShowError(ex.Message, "加载目标档口失败");
                return;
            }

            var ordered = options
                .OrderBy(item => item.FitsCapacity ? 0 : 1)
                .ThenBy(item => item.Priority)
                .ThenBy(item => item.ExistingBoxCount)
                .ThenBy(item => item.Location, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string? previousLocation = SelectedTargetSlotLocationOption?.Location ?? TargetFullLocation;
            ReplaceTargetSlotLocationOptions(ordered);

            ArchiveBoxTargetLocationOption? selected = null;
            if (preferSuggestedSelection)
            {
                selected = ordered.FirstOrDefault();
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
                TargetCellOccupancyText = $"未找到符合规格「{specification}」的可用档口，请先在开柜界面完成档口用途设置。";
                return;
            }

            SyncSelectedTargetSlotLocationOption(selected.Location);
            await ResolveTargetFullLocationFromSelectedOptionAsync();
        }

        private async Task RecommendTargetSlotAsync()
        {
            if (!CanRecommendTargetSlot || SourceSummary == null)
            {
                _dialogService.ShowMessage("请先选择源档案盒，并确认盒规格后再推荐。", "推荐档口");
                return;
            }

            string specification = ResolveBoxSpecificationForTargetSlot();
            try
            {
                IsBusy = true;
                await LoadTargetSlotLocationOptionsAsync(preferSuggestedSelection: false);

                var suggestion = await _filingService.SuggestArchiveBoxLocationAsync(
                    SourceSummary.ProjectName,
                    SourceSummary.Year,
                    specification);

                string? recommendedFull = suggestion?.SuggestedBoxLocationCode?.Trim();
                ArchiveBoxTargetLocationOption? matched = null;

                if (!string.IsNullOrWhiteSpace(recommendedFull)
                    && ArchiveSlotLocationSupport.IsSameSlot(SourceCurrentLocation, recommendedFull))
                {
                    recommendedFull = null;
                }

                if (!string.IsNullOrWhiteSpace(recommendedFull))
                {
                    matched = FindTargetSlotLocationOption(TargetSlotLocationOptions, recommendedFull);
                }

                if (matched == null || string.IsNullOrWhiteSpace(recommendedFull))
                {
                    matched = TargetSlotLocationOptions
                        .Where(item => !ArchiveSlotLocationSupport.IsSameSlot(SourceCurrentLocation, item.Location))
                        .OrderBy(item => item.FitsCapacity ? 0 : 1)
                        .ThenBy(item => item.Priority)
                        .ThenBy(item => item.ExistingBoxCount)
                        .ThenBy(item => item.Location, StringComparer.OrdinalIgnoreCase)
                        .FirstOrDefault();

                    if (matched != null)
                    {
                        recommendedFull = await ResolveFullLocationInSlotAsync(matched);
                    }
                }

                if (matched == null || string.IsNullOrWhiteSpace(recommendedFull))
                {
                    _dialogService.ShowMessage(
                        $"未找到符合规格「{specification}」且不同于源档口的可用档口。",
                        "推荐档口");
                    return;
                }

                if (!TargetSlotLocationOptions.Contains(matched))
                {
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

                await ApplyResolvedTargetLocationAsync(matched, recommendedFull);

                string summary = matched.FitsCapacity
                    ? $"已推荐档口：{recommendedFull}"
                    : $"已推荐档口：{recommendedFull}（当前未找到严格满足容量规则的档口，已回退为占用较少的可用档口，请人工确认）";
                if (!string.IsNullOrWhiteSpace(suggestion?.SuggestionSummary)
                    && ArchiveSlotLocationSupport.IsSameSlot(suggestion.SuggestedBoxLocationCode, recommendedFull))
                {
                    summary = suggestion.SuggestionSummary;
                }

                _dialogService.ShowMessage(summary, "推荐档口");
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
                _resolvedTargetBoxIndex = 0;
                return;
            }

            try
            {
                string? fullLocation = await ResolveFullLocationInSlotAsync(SelectedTargetSlotLocationOption);
                if (string.IsNullOrWhiteSpace(fullLocation))
                {
                    TargetFullLocation = string.Empty;
                    TargetCellOccupancyText = "目标档口无法分配盒序号。";
                    _resolvedTargetBoxIndex = 0;
                    return;
                }

                await ApplyResolvedTargetLocationAsync(SelectedTargetSlotLocationOption, fullLocation);
            }
            catch (Exception ex)
            {
                TargetFullLocation = string.Empty;
                TargetCellOccupancyText = "位置计算失败";
                _resolvedTargetBoxIndex = 0;
                _dialogService.ShowError($"计算目标档口失败：{ex.Message}", "错误");
            }
        }

        private async Task ApplyResolvedTargetLocationAsync(ArchiveBoxTargetLocationOption option, string fullLocation)
        {
            TargetFullLocation = fullLocation.Trim();
            ArchiveSlotLocationSupport.TryParseSequenceIndex(fullLocation, out _resolvedTargetBoxIndex);
            int cellCount = await _filingService.GetBoxCountInCellAsync(
                option.CabinetName,
                option.Side,
                option.Row,
                option.Column);
            TargetCellOccupancyText = $"目标格内现有 {cellCount} 盒，迁入后将使用序号 {_resolvedTargetBoxIndex:D2}";
        }

        private async Task<string?> ResolveFullLocationInSlotAsync(ArchiveBoxTargetLocationOption option)
        {
            int sequence = await _filingService.GetMinimumAvailableBoxSequenceInCellAsync(
                option.CabinetName,
                option.Side,
                option.Row,
                option.Column);
            if (sequence <= 0)
            {
                return null;
            }

            return $"{option.CabinetName.Trim()}{option.Side.Trim().ToUpperInvariant()}-{option.Row}-{option.Column}-{sequence:D2}";
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

            var cabinet = _archiveCabinets.FirstOrDefault(item =>
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
        /// 整盒物理迁移用源盒规格；迁入空盒用新建盒规格。
        /// </summary>
        private string ResolveBoxSpecificationForTargetSlot()
        {
            if (MoveContentsToNewEmptyBox)
            {
                return SelectedNewBoxSpecification?.Trim() ?? string.Empty;
            }

            return SourceSummary?.BoxSpecification?.Trim() ?? string.Empty;
        }

        private bool TryApplySelectedTargetLocation(SimulatedRelocationRequest request, out string message)
        {
            if (SelectedTargetSlotLocationOption == null
                || string.IsNullOrWhiteSpace(TargetFullLocation)
                || _resolvedTargetBoxIndex <= 0)
            {
                message = "请完整选择新的存放档口。";
                return false;
            }

            if (IsTargetSameSlotAsSource)
            {
                message = TargetSlotValidationMessage;
                return false;
            }

            var option = SelectedTargetSlotLocationOption;
            request.NewStorageLocation = TargetFullLocation.Trim();
            request.NewCabinetName = option.CabinetName;
            request.NewSide = option.Side;
            request.NewRow = option.Row;
            request.NewColumn = option.Column;
            request.NewBoxIndex = _resolvedTargetBoxIndex;
            message = string.Empty;
            return true;
        }

        private void ReplaceTargetSlotLocationOptions(IReadOnlyList<ArchiveBoxTargetLocationOption> options)
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

        private static ArchiveBoxTargetLocationOption? FindTargetSlotLocationOption(
            IEnumerable<ArchiveBoxTargetLocationOption> options,
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
