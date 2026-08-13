using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows.Input;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Services.HardDiskMedia;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.NetworkTransfer
{
    /// <summary>
    /// 入网审批环节，单块借出硬盘的空白归位档口维护行。
    /// </summary>
    public sealed class NetworkInboundReturnHardDiskApprovalRowViewModel : ViewModelBase
    {
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly ICabinetService _cabinetService;
        private readonly IDialogService _dialogService;
        private readonly Func<IReadOnlyCollection<string>> _reservedSlotProvider;
        private HardDiskMediaReturnTargetLocationOption? _selectedSlotLocationOption;
        private bool _isLoadingOptions;

        public NetworkInboundReturnHardDiskApprovalRowViewModel(
            NetworkInboundReturnHardDiskItem item,
            IHardDiskMediaService hardDiskMediaService,
            ICabinetService cabinetService,
            IDialogService dialogService,
            bool canEditSlots,
            Func<IReadOnlyCollection<string>> reservedSlotProvider)
        {
            Item = item;
            _hardDiskMediaService = hardDiskMediaService;
            _cabinetService = cabinetService;
            _dialogService = dialogService;
            CanEditSlots = canEditSlots;
            _reservedSlotProvider = reservedSlotProvider;
            RecommendSlotLocationCommand = new RelayCommand(
                async _ => await RecommendSlotLocationAsync(),
                _ => CanEditSlots && !IsLoadingOptions);
            ShowSlotLocationSnapshotCommand = new RelayCommand(
                async _ => await ShowSlotLocationSnapshotAsync(),
                _ => CanShowSlotLocationSnapshot);
        }

        public NetworkInboundReturnHardDiskItem Item { get; }

        public bool CanEditSlots { get; }

        public bool IsLoadingOptions
        {
            get => _isLoadingOptions;
            private set
            {
                if (SetProperty(ref _isLoadingOptions, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string DiskCode => Item.DiskCode;

        public ObservableCollection<HardDiskMediaReturnTargetLocationOption> SlotLocationOptions { get; } = new();

        public HardDiskMediaReturnTargetLocationOption? SelectedSlotLocationOption
        {
            get => _selectedSlotLocationOption;
            set
            {
                if (SetProperty(ref _selectedSlotLocationOption, value))
                {
                    Item.TargetBlankSlotLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(value?.Location);
                    OnPropertyChanged(nameof(TargetBlankSlotLocationDisplay));
                    OnPropertyChanged(nameof(CanShowSlotLocationSnapshot));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string TargetBlankSlotLocationDisplay =>
            string.IsNullOrWhiteSpace(Item.TargetBlankSlotLocation)
                ? "（待指定）"
                : Item.TargetBlankSlotLocation.Trim();

        public RelayCommand RecommendSlotLocationCommand { get; }

        public RelayCommand ShowSlotLocationSnapshotCommand { get; }

        /// <summary>当前所选档口可解析时，允许查看档口快照。</summary>
        public bool CanShowSlotLocationSnapshot =>
            TryParseCabinetLocation(Item.TargetBlankSlotLocation, out _, out _, out _);

        /// <summary>
        /// 加载空白硬盘专用档口选项，并在审批态自动推荐首个可用档口。
        /// </summary>
        public async Task LoadSlotLocationOptionsAsync(bool autoSelectRecommended)
        {
            IsLoadingOptions = true;
            try
            {
                SlotLocationOptions.Clear();
                var options = await _hardDiskMediaService.GetReturnTargetLocationOptionsAsync(
                    HardDiskMediaApplication.TypeReturnBlankRegistration,
                    Item.MediumId,
                    Item.SourceApplicationId,
                    Item.SourceOutboundRecordId);
                foreach (HardDiskMediaReturnTargetLocationOption option in options)
                {
                    SlotLocationOptions.Add(option);
                }

                string persisted = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(Item.TargetBlankSlotLocation);
                if (!string.IsNullOrWhiteSpace(persisted)
                    && SlotLocationOptions.All(item => !string.Equals(item.Location, persisted, StringComparison.OrdinalIgnoreCase)))
                {
                    SlotLocationOptions.Insert(0, new HardDiskMediaReturnTargetLocationOption
                    {
                        Location = persisted,
                        ExistingMediumCount = 0
                    });
                }

                string? recommended = await RecommendNextAvailableSlotAsync();
                if (autoSelectRecommended && CanEditSlots)
                {
                    SelectedSlotLocationOption =
                        SlotLocationOptions.FirstOrDefault(option => string.Equals(option.Location, recommended, StringComparison.OrdinalIgnoreCase))
                        ?? SlotLocationOptions.FirstOrDefault(option => string.Equals(option.Location, persisted, StringComparison.OrdinalIgnoreCase))
                        ?? SlotLocationOptions.FirstOrDefault();
                }
                else
                {
                    SelectedSlotLocationOption =
                        SlotLocationOptions.FirstOrDefault(option => string.Equals(option.Location, persisted, StringComparison.OrdinalIgnoreCase))
                        ?? SlotLocationOptions.FirstOrDefault();
                }

                OnPropertyChanged(nameof(CanShowSlotLocationSnapshot));
                CommandManager.InvalidateRequerySuggested();
            }
            finally
            {
                IsLoadingOptions = false;
            }
        }

        private async Task RecommendSlotLocationAsync()
        {
            string? recommended = await RecommendNextAvailableSlotAsync();
            if (string.IsNullOrWhiteSpace(recommended))
            {
                _dialogService.ShowMessage("未找到可用的空白硬盘专用档口，请先在磁盘柜开柜界面设置。", "推荐档口");
                return;
            }

            SelectedSlotLocationOption =
                SlotLocationOptions.FirstOrDefault(option => string.Equals(option.Location, recommended, StringComparison.OrdinalIgnoreCase))
                ?? new HardDiskMediaReturnTargetLocationOption
                {
                    Location = recommended,
                    ExistingMediumCount = 0
                };

            if (!SlotLocationOptions.Contains(SelectedSlotLocationOption))
            {
                SlotLocationOptions.Insert(0, SelectedSlotLocationOption);
            }

            _dialogService.ShowMessage($"已推荐 {SelectedSlotLocationOption.DisplayText}", "推荐档口");
            OnPropertyChanged(nameof(CanShowSlotLocationSnapshot));
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task<string?> RecommendNextAvailableSlotAsync()
        {
            IReadOnlyCollection<string> reserved = _reservedSlotProvider();
            var options = await _hardDiskMediaService.GetReturnTargetLocationOptionsAsync(
                HardDiskMediaApplication.TypeReturnBlankRegistration,
                Item.MediumId,
                Item.SourceApplicationId,
                Item.SourceOutboundRecordId);

            foreach (HardDiskMediaReturnTargetLocationOption option in options)
            {
                string normalized = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(option.Location);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (reserved.Any(slot => string.Equals(slot, normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                return normalized;
            }

            return await _hardDiskMediaService.RecommendBlankDedicatedSlotLocationAsync();
        }

        private async Task ShowSlotLocationSnapshotAsync()
        {
            if (!CanShowSlotLocationSnapshot)
            {
                _dialogService.ShowMessage("请先选择或推荐空白硬盘归位档口后再查看快照。", "档口快照");
                return;
            }

            string location = Item.TargetBlankSlotLocation;
            if (!TryParseCabinetLocation(location, out string cabinetName, out CabinetFace face, out string slotCode))
            {
                _dialogService.ShowMessage("当前档口无法解析，请重新选择后再查看快照。", "档口快照");
                return;
            }

            var cabinet = (await _cabinetService.GetAllCabinetsAsync())
                .FirstOrDefault(item => item.Type == CabinetType.MagneticDisk
                                        && string.Equals(item.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
            if (cabinet == null)
            {
                _dialogService.ShowMessage($"未找到柜号 [{cabinetName}] 对应的防磁磁盘柜。", "档口快照");
                return;
            }

            _dialogService.ShowCabinetOpenDialog(new CabinetOpenRequest
            {
                CabinetId = cabinet.Id,
                CabinetName = cabinet.Name,
                CabinetType = cabinet.Type,
                Face = face,
                LayerCount = cabinet.LayerCount,
                ColumnCount = cabinet.ColumnCount,
                TargetSlotCode = slotCode,
                WidthCm = cabinet.Width,
                HeightCm = cabinet.Height,
                DepthCm = cabinet.Depth
            });
        }

        private static bool TryParseCabinetLocation(string? location, out string cabinetName, out CabinetFace face, out string slotCode)
        {
            cabinetName = string.Empty;
            face = CabinetFace.A;
            slotCode = string.Empty;

            if (string.IsNullOrWhiteSpace(location))
            {
                return false;
            }

            var match = Regex.Match(location.Trim(), "^(?<cabinet>.+?)(?<face>[AB])-(?<row>\\d+)-(?<col>\\d+)$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return false;
            }

            cabinetName = match.Groups["cabinet"].Value;
            face = string.Equals(match.Groups["face"].Value, "B", StringComparison.OrdinalIgnoreCase)
                ? CabinetFace.B
                : CabinetFace.A;
            slotCode = $"{match.Groups["row"].Value}-{match.Groups["col"].Value}";
            return !string.IsNullOrWhiteSpace(cabinetName) && !string.IsNullOrWhiteSpace(slotCode);
        }
    }
}
