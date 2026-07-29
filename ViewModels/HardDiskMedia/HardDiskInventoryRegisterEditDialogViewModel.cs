using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.HardDiskMedia;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘盘库登记办理弹窗 ViewModel。
    /// </summary>
    public sealed class HardDiskInventoryRegisterEditDialogViewModel : ViewModelBase
    {
        private readonly IHardDiskInventoryRegisterService _registerService;
        private readonly ICabinetService _cabinetService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly List<HardDiskInventoryRegisterCandidateViewModel> _mediaPool = new();
        private HardDiskInventoryRegisterRecord _record;
        private bool _hasCommittedChanges;
        private string _registerNo = string.Empty;
        private string _registerKind = HardDiskInventoryRegisterDomainValues.KindDamage;
        private string _reason = string.Empty;
        private string _remark = string.Empty;
        private string _filterKeyword = string.Empty;
        private HardDiskInventoryRegisterItemViewModel? _selectedItem;

        public HardDiskInventoryRegisterEditDialogViewModel(
            IHardDiskInventoryRegisterService registerService,
            ICabinetService cabinetService,
            IDialogService dialogService,
            IUserContextService userContextService,
            HardDiskInventoryRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            _registerService = registerService;
            _cabinetService = cabinetService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _record = record;

            MoveToRegisterCommand = new RelayCommand(_ => MoveToRegister(), _ => CanEditHeader && AvailableDisks.Any(item => item.IsSelected));
            MoveToAvailableCommand = new RelayCommand(_ => MoveToAvailable(), _ => CanEditHeader && Items.Any(item => item.IsSelected));
            RecommendTargetLocationCommand = new RelayCommand(async _ => await RecommendTargetLocationAsync(), _ => CanEditHeader && RequiresTargetLocation && SelectedItem != null);
            ShowTargetLocationPreviewCommand = new RelayCommand(async _ => await ShowTargetLocationPreviewAsync(), _ => CanShowTargetLocationPreview);
            SaveDraftCommand = new RelayCommand(async _ => await SaveDraftAsync(), _ => CanEditHeader);
            CompleteCommand = new RelayCommand(async _ => await CompleteAsync(), _ => CanComplete);
            WithdrawCommand = new RelayCommand(async _ => await WithdrawAsync(), _ => CanWithdraw);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            _ = InitializeAsync();
        }

        public event Action<bool?>? RequestClose;

        public bool HasCommittedChanges => _hasCommittedChanges;

        public string WindowTitle =>
            $"硬盘盘库登记 · {(string.IsNullOrWhiteSpace(RegisterNo) ? "待编单" : RegisterNo)} · {StatusDisplay}";

        public string StatusDisplay => HardDiskInventoryRegisterDomainValues.ToStatusDisplay(_record.Status);

        public string BannerText =>
            "仅「在库(空盘)」「在库(损坏)」可盘库登记。损坏登记/档口调整须指定损坏硬盘专用档口；盘失登记清空档口并写「在库(盘失)」。确认登记办结即时写台账，无需审批签批。正式离库请走「离库处置」。";

        public ObservableCollection<string> RegisterKindOptions { get; } = new(HardDiskInventoryRegisterDomainValues.RegisterKindOptions);

        public ObservableCollection<HardDiskInventoryRegisterCandidateViewModel> AvailableDisks { get; } = new();

        public ObservableCollection<HardDiskInventoryRegisterItemViewModel> Items { get; } = new();

        public ObservableCollection<HardDiskMediaReturnTargetLocationOption> DamagedLocationOptions { get; } = new();

        public string AvailableDisksTitle => $"可选库内盘（{AvailableDisks.Count}）";

        public string RegisterDisksTitle => $"待登记硬盘（{Items.Count}）";

        public string FilterKeyword
        {
            get => _filterKeyword;
            set
            {
                if (SetProperty(ref _filterKeyword, value))
                {
                    RefreshAvailableDisks();
                }
            }
        }

        public string RegisterNo
        {
            get => _registerNo;
            set => SetProperty(ref _registerNo, value);
        }

        public string RegisterKind
        {
            get => _registerKind;
            set
            {
                if (SetProperty(ref _registerKind, value))
                {
                    OnPropertyChanged(nameof(RequiresTargetLocation));
                    OnPropertyChanged(nameof(TargetLocationHint));
                    RefreshAvailableDisks();
                    RaiseCommandStates();
                }
            }
        }

        public string Reason
        {
            get => _reason;
            set => SetProperty(ref _reason, value);
        }

        public string Remark
        {
            get => _remark;
            set => SetProperty(ref _remark, value);
        }

        public HardDiskInventoryRegisterItemViewModel? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    OnPropertyChanged(nameof(CanShowTargetLocationPreview));
                    RaiseCommandStates();
                }
            }
        }

        public bool RequiresTargetLocation =>
            HardDiskInventoryRegisterDomainValues.RequiresDamagedTargetLocation(RegisterKind);

        public string TargetLocationHint => RequiresTargetLocation
            ? "请为每块盘指定损坏硬盘专用档口（可用推荐档口、档口预览）。办结时将核验档口容量（10盘/档口）。"
            : "盘失登记无需归位档口，办结后清空存放位置。";

        public bool CanEditHeader =>
            ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser)
            && _record.Status == HardDiskInventoryRegisterRecord.StatusDraft;

        public bool CanComplete => CanEditHeader && Items.Count > 0;

        public bool CanWithdraw => CanEditHeader && _record.Id > 0;

        public bool CanShowTargetLocationPreview =>
            RequiresTargetLocation
            && SelectedItem != null
            && !string.IsNullOrWhiteSpace(SelectedItem.TargetStorageLocation);

        public RelayCommand MoveToRegisterCommand { get; }
        public RelayCommand MoveToAvailableCommand { get; }
        public RelayCommand RecommendTargetLocationCommand { get; }
        public RelayCommand ShowTargetLocationPreviewCommand { get; }
        public RelayCommand SaveDraftCommand { get; }
        public RelayCommand CompleteCommand { get; }
        public RelayCommand WithdrawCommand { get; }
        public RelayCommand CloseCommand { get; }

        private async Task InitializeAsync()
        {
            try
            {
                RegisterNo = _record.RegisterNo;
                RegisterKind = string.IsNullOrWhiteSpace(_record.RegisterKind)
                    ? HardDiskInventoryRegisterDomainValues.KindDamage
                    : _record.RegisterKind;
                Reason = _record.Reason;
                Remark = _record.Remark;

                if (_record.Id <= 0 && string.IsNullOrWhiteSpace(RegisterNo))
                {
                    RegisterNo = await _registerService.GenerateNextRegisterNoAsync();
                }

                var options = await _registerService.GetDamagedTargetLocationOptionsAsync();
                DamagedLocationOptions.Clear();
                foreach (var option in options)
                {
                    DamagedLocationOptions.Add(option);
                }

                await ReloadMediaPoolAsync();
                Items.Clear();
                foreach (var item in _record.Items.OrderBy(detail => detail.SortOrder))
                {
                    Items.Add(HardDiskInventoryRegisterItemViewModel.FromItem(item));
                }

                RefreshAvailableDisks();
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(StatusDisplay));
                RaiseCommandStates();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ReloadMediaPoolAsync()
        {
            int? currentId = _record.Id > 0 ? _record.Id : null;
            var media = await _registerService.GetSelectableMediaAsync(currentId);
            _mediaPool.Clear();
            foreach (var medium in media)
            {
                _mediaPool.Add(HardDiskInventoryRegisterCandidateViewModel.FromMedium(medium));
            }
        }

        private void RefreshAvailableDisks()
        {
            HashSet<int> selectedIds = Items.Select(item => item.MediumId).ToHashSet();
            string keyword = FilterKeyword?.Trim() ?? string.Empty;
            string kind = RegisterKind?.Trim() ?? string.Empty;

            AvailableDisks.Clear();
            foreach (var candidate in _mediaPool.Where(item => !selectedIds.Contains(item.MediumId)))
            {
                string status = candidate.MediaStatus?.Trim() ?? string.Empty;
                bool compatible = string.Equals(kind, HardDiskInventoryRegisterDomainValues.KindDamage, StringComparison.Ordinal)
                    ? string.Equals(status, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal)
                    : string.Equals(kind, HardDiskInventoryRegisterDomainValues.KindRelocateDamaged, StringComparison.Ordinal)
                        ? string.Equals(status, HardDiskMedium.StatusInStockDamaged, StringComparison.Ordinal)
                        : string.Equals(status, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal)
                          || string.Equals(status, HardDiskMedium.StatusInStockDamaged, StringComparison.Ordinal);

                if (!compatible)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(keyword)
                    && !candidate.DiskCode.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    && !candidate.SerialNumber.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    && !candidate.StorageLocation.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AvailableDisks.Add(candidate);
            }

            OnPropertyChanged(nameof(AvailableDisksTitle));
            OnPropertyChanged(nameof(RegisterDisksTitle));
            RaiseCommandStates();
        }

        private void MoveToRegister()
        {
            var selected = AvailableDisks.Where(item => item.IsSelected).ToList();
            foreach (var candidate in selected)
            {
                if (Items.Any(item => item.MediumId == candidate.MediumId))
                {
                    continue;
                }

                Items.Add(HardDiskInventoryRegisterItemViewModel.FromCandidate(candidate));
            }

            RefreshAvailableDisks();
        }

        private void MoveToAvailable()
        {
            var selected = Items.Where(item => item.IsSelected).ToList();
            foreach (var item in selected)
            {
                Items.Remove(item);
            }

            RefreshAvailableDisks();
        }

        private async Task RecommendTargetLocationAsync()
        {
            if (SelectedItem == null)
            {
                return;
            }

            await EnsureDamagedLocationOptionsAsync();
            if (DamagedLocationOptions.Count == 0)
            {
                _dialogService.ShowMessage("未找到损坏硬盘专用档口，请先在磁盘柜开柜界面完成设置。", "推荐档口");
                return;
            }

            int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(
                CabinetHardDiskSlotCategoryAssignment.CategoryDamaged);
            var preferred = DamagedLocationOptions
                .Where(option => option.ExistingMediumCount < slotCapacity)
                .OrderBy(option => option.ExistingMediumCount)
                .ThenBy(option => option.Location, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (preferred == null)
            {
                _dialogService.ShowMessage($"损坏硬盘专用档口均已满（每档口最多 {slotCapacity} 盘），请先腾出容量或新增专用档口。", "推荐档口");
                return;
            }

            SelectedItem.TargetStorageLocation = preferred.Location;
            RaiseCommandStates();
            _dialogService.ShowMessage($"已推荐档口：{SelectedItem.TargetStorageLocation}（现有 {preferred.ExistingMediumCount} 盘）", "推荐档口");
        }

        private async Task ShowTargetLocationPreviewAsync()
        {
            if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.TargetStorageLocation))
            {
                _dialogService.ShowMessage("请先填写目标档口后再预览。", "档口预览");
                return;
            }

            if (!TryParseCabinetLocation(SelectedItem.TargetStorageLocation, out string cabinetName, out CabinetFace face, out string slotCode))
            {
                _dialogService.ShowMessage("目标档口无法解析，请核对格式（如：柜名A-1-2）后再预览。", "档口预览");
                return;
            }

            var cabinet = (await _cabinetService.GetAllCabinetsAsync())
                .FirstOrDefault(item =>
                    item.Type == CabinetType.MagneticDisk
                    && string.Equals(item.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
            if (cabinet == null)
            {
                _dialogService.ShowMessage($"未找到柜号 [{cabinetName}] 对应的防磁磁盘柜。", "档口预览");
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

        private async Task EnsureDamagedLocationOptionsAsync()
        {
            var options = await _registerService.GetDamagedTargetLocationOptionsAsync();
            DamagedLocationOptions.Clear();
            foreach (var option in options)
            {
                DamagedLocationOptions.Add(option);
            }
        }

        private static bool TryParseCabinetLocation(string? location, out string cabinetName, out CabinetFace face, out string slotCode)
        {
            cabinetName = string.Empty;
            face = CabinetFace.A;
            slotCode = string.Empty;

            if (!HardDiskBlankSlotLocationSupport.TryParseLocationCode(
                    location,
                    out string parsedCabinet,
                    out string faceCode,
                    out int row,
                    out int column))
            {
                return false;
            }

            cabinetName = parsedCabinet;
            face = string.Equals(faceCode, "B", StringComparison.OrdinalIgnoreCase) ? CabinetFace.B : CabinetFace.A;
            slotCode = $"{row}-{column}";
            return !string.IsNullOrWhiteSpace(cabinetName);
        }

        private async Task SaveDraftAsync()
        {
            try
            {
                var user = RequireCurrentUser();
                var payload = BuildDraftPayload();
                var itemDrafts = BuildItemDrafts();

                _record = _record.Id <= 0
                    ? await _registerService.CreateDraftAsync(payload, itemDrafts, user)
                    : await _registerService.UpdateDraftAsync(payload, itemDrafts, user);

                _hasCommittedChanges = true;
                RegisterNo = _record.RegisterNo;
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(StatusDisplay));
                RaiseCommandStates();
                _dialogService.ShowMessage("草稿已保存。");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task CompleteAsync()
        {
            try
            {
                var user = RequireCurrentUser();
                if (!_dialogService.ShowConfirm("确认登记办结？办结后将即时更新硬盘台账状态/档口并写入流转流水。", "确认登记办结"))
                {
                    return;
                }

                var payload = BuildDraftPayload();
                var itemDrafts = BuildItemDrafts();
                _record = _record.Id <= 0
                    ? await _registerService.CreateDraftAsync(payload, itemDrafts, user)
                    : await _registerService.UpdateDraftAsync(payload, itemDrafts, user);

                await _registerService.CompleteAsync(_record.Id, user);
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("盘库登记已确认办结。", "确认登记办结");
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task WithdrawAsync()
        {
            try
            {
                if (_record.Id <= 0)
                {
                    RequestClose?.Invoke(false);
                    return;
                }

                if (!_dialogService.ShowConfirm("确认撤回作废本草稿？", "撤回作废"))
                {
                    return;
                }

                await _registerService.WithdrawAsync(_record.Id, "办理窗口撤回作废", RequireCurrentUser());
                _hasCommittedChanges = true;
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private HardDiskInventoryRegisterRecord BuildDraftPayload()
        {
            return new HardDiskInventoryRegisterRecord
            {
                Id = _record.Id,
                RegisterNo = RegisterNo,
                RegisterKind = RegisterKind,
                Reason = Reason,
                Remark = Remark
            };
        }

        private List<HardDiskInventoryRegisterItemDraft> BuildItemDrafts()
        {
            return Items
                .Select(item => new HardDiskInventoryRegisterItemDraft
                {
                    MediumId = item.MediumId,
                    TargetStorageLocation = RequiresTargetLocation ? item.TargetStorageLocation : string.Empty
                })
                .ToList();
        }

        private User RequireCurrentUser()
        {
            return _userContextService.CurrentUser
                ?? throw new InvalidOperationException("当前用户无效，请重新登录。");
        }

        private void RaiseCommandStates()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>库内可选硬盘行。</summary>
    public sealed class HardDiskInventoryRegisterCandidateViewModel : ViewModelBase
    {
        private bool _isSelected;

        public static HardDiskInventoryRegisterCandidateViewModel FromMedium(HardDiskMedium medium)
        {
            ArgumentNullException.ThrowIfNull(medium);
            return new HardDiskInventoryRegisterCandidateViewModel
            {
                MediumId = medium.Id,
                DiskCode = medium.DiskCode?.Trim() ?? string.Empty,
                SerialNumber = medium.SerialNumber?.Trim() ?? string.Empty,
                Brand = medium.Brand?.Trim() ?? string.Empty,
                Capacity = medium.Capacity?.Trim() ?? string.Empty,
                MediaStatus = medium.Ledger?.MediaStatus?.Trim() ?? string.Empty,
                StorageLocation = medium.Ledger?.StorageLocation?.Trim() ?? string.Empty
            };
        }

        public int MediumId { get; init; }
        public string DiskCode { get; init; } = string.Empty;
        public string SerialNumber { get; init; } = string.Empty;
        public string Brand { get; init; } = string.Empty;
        public string Capacity { get; init; } = string.Empty;
        public string MediaStatus { get; init; } = string.Empty;
        public string StorageLocation { get; init; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    /// <summary>待登记明细行。</summary>
    public sealed class HardDiskInventoryRegisterItemViewModel : ViewModelBase
    {
        private bool _isSelected;
        private string _targetStorageLocation = string.Empty;

        public static HardDiskInventoryRegisterItemViewModel FromCandidate(HardDiskInventoryRegisterCandidateViewModel candidate)
        {
            return new HardDiskInventoryRegisterItemViewModel
            {
                MediumId = candidate.MediumId,
                DiskCode = candidate.DiskCode,
                SerialNumber = candidate.SerialNumber,
                BeforeMediaStatus = candidate.MediaStatus,
                BeforeStorageLocation = candidate.StorageLocation
            };
        }

        public static HardDiskInventoryRegisterItemViewModel FromItem(HardDiskInventoryRegisterItem item)
        {
            return new HardDiskInventoryRegisterItemViewModel
            {
                MediumId = item.MediumId,
                DiskCode = item.DiskCode,
                SerialNumber = item.SerialNumber,
                BeforeMediaStatus = item.BeforeMediaStatus,
                BeforeStorageLocation = item.BeforeStorageLocation,
                TargetStorageLocation = item.TargetStorageLocation
            };
        }

        public int MediumId { get; init; }
        public string DiskCode { get; init; } = string.Empty;
        public string SerialNumber { get; init; } = string.Empty;
        public string BeforeMediaStatus { get; init; } = string.Empty;
        public string BeforeStorageLocation { get; init; } = string.Empty;

        public string TargetStorageLocation
        {
            get => _targetStorageLocation;
            set => SetProperty(ref _targetStorageLocation, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
