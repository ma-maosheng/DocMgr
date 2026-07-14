using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 借出申请按档案盒/电子介质袋分组的领用设置单元。
    /// </summary>
    public sealed class ArchiveOutboundContainerUnitViewModel : ViewModelBase
    {
        private const string BlankTargetSelectionMode = "BlankTarget";

        private readonly IDialogService _dialogService;
        private readonly Action<ArchiveOutboundContainerUnitViewModel>? _onSharedDiskStateChanged;
        private readonly Func<ArchiveOutboundContainerUnitViewModel, Task>? _revokeGroupRegistrationAsync;
        private readonly string _needReturnRadioGroup = Guid.NewGuid().ToString("N");
        private readonly string _withdrawalHardDiskNeedReturnRadioGroup = Guid.NewGuid().ToString("N");
        private readonly string _useInStockBlankRadioGroup = Guid.NewGuid().ToString("N");
        private readonly string _requisitionedDiskNeedReturnRadioGroup = Guid.NewGuid().ToString("N");
        private bool _canEdit;
        private string _usageMode = ArchiveOutboundDomainValues.UsageModeWithdrawal;
        private readonly IReadOnlyList<OutboundUsageModeOption> _usageModeOptions;
        private bool _needReturn = true;
        private bool _useInStockBlankDisk;
        private string _duplicateMediumKind = ArchiveOutboundDomainValues.DuplicateMediumSelfUsb;
        private string _requisitionedDiskCode = string.Empty;
        private bool _requisitionedDiskNeedReturn = true;
        private DateTime? _expectedReturnDate;
        private bool _suppressSharedDiskCallback;
        private bool _isSharedDiskSettingsReadOnly;
        private string _sharedDiskSettingsHint = string.Empty;

        public ArchiveOutboundContainerUnitViewModel(
            string unitKey,
            string mediaKind,
            string containerCode,
            string currentStorageLocation,
            IEnumerable<ArchiveOutboundItemRowViewModel> itemRows,
            bool canEdit,
            IDialogService dialogService,
            int unitIndex = 1,
            Action<ArchiveOutboundContainerUnitViewModel>? onSharedDiskStateChanged = null,
            Func<ArchiveOutboundContainerUnitViewModel, Task>? revokeGroupRegistrationAsync = null)
        {
            UnitKey = unitKey;
            MediaKind = mediaKind;
            ContainerCode = containerCode;
            CurrentStorageLocation = currentStorageLocation;
            UnitIndex = unitIndex;
            _canEdit = canEdit;
            _dialogService = dialogService;
            _onSharedDiskStateChanged = onSharedDiskStateChanged;
            _revokeGroupRegistrationAsync = revokeGroupRegistrationAsync;

            ItemRows = new System.Collections.ObjectModel.ObservableCollection<ArchiveOutboundItemRowViewModel>(itemRows);

            _usageModeOptions = BuildUsageModeOptions(
                mediaKind,
                ItemRows.FirstOrDefault()?.ArchivePurpose?.Trim());

            PickBlankDiskCommand = new RelayCommand(_ => PickBlankDisk(), _ => CanPickBlankDisk);
            RevokeGroupRegistrationCommand = new RelayCommand(
                async _ =>
                {
                    if (_revokeGroupRegistrationAsync != null)
                    {
                        await _revokeGroupRegistrationAsync(this);
                    }
                },
                _ => CanRevokeGroupRegistration);

            ItemDetailsPanel = new ItemDetailsListPresenter<ArchiveOutboundItemRowViewModel>(
                "盒/袋内资料明细",
                summaryBuilder: ItemDetailsPanelSummarySupport.BuildOutboundItemSummary);
            ItemDetailsPanel.RefreshItems(ItemRows);

            LoadFromItems();
        }

        public string UnitKey { get; }

        public string MediaKind { get; }

        public string ContainerCode { get; }

        public string CurrentStorageLocation { get; }

        public int UnitIndex { get; }

        public int ItemCount => ItemRows.Count;

        public string DisplayTitle => $"{UnitIndex}. {UnitTitle}";

        public string MediaTypeSummary
        {
            get
            {
                string mediaType = ItemRows.FirstOrDefault()?.MediaType?.Trim() ?? string.Empty;
                return string.IsNullOrWhiteSpace(mediaType) ? "—" : mediaType;
            }
        }

        public string LocationDisplay
        {
            get
            {
                string location = CurrentStorageLocation?.Trim() ?? string.Empty;
                return string.IsNullOrWhiteSpace(location) ? "未登记位置" : location;
            }
        }

        public string ItemCountDisplay => $"{ItemCount} 条资料";

        public string UnitTitle => ArchiveOutboundContainerUnitSupport.FormatUnitTitle(MediaKind, ContainerCode);

        public string ContainerKindLabel => ArchiveOutboundContainerUnitSupport.GetContainerKindLabel(MediaKind);

        public System.Collections.ObjectModel.ObservableCollection<ArchiveOutboundItemRowViewModel> ItemRows { get; }

        public RelayCommand PickBlankDiskCommand { get; }

        /// <summary>
        /// 撤销本盒/袋下通过检索集登记的全部拟领用资料。
        /// </summary>
        public RelayCommand RevokeGroupRegistrationCommand { get; }

        public bool CanRevokeGroupRegistration =>
            CanEdit && ItemRows.Any(row => row.IsFromSearchResultSetRegistration);

        public bool ShowRevokeGroupRegistration => CanRevokeGroupRegistration;

        public ItemDetailsListPresenter<ArchiveOutboundItemRowViewModel> ItemDetailsPanel { get; }

        public IReadOnlyList<OutboundUsageModeOption> UsageModeOptions => _usageModeOptions;

        public string ArchivePurpose =>
            ItemRows.FirstOrDefault()?.ArchivePurpose?.Trim() ?? string.Empty;

        public string StorageCarrierType =>
            ItemRows.FirstOrDefault()?.Source.StorageCarrierType?.Trim() ?? string.Empty;

        public bool IsLongTermElectronicArchive =>
            IsElectronicMedia && ArchiveOutboundDomainValues.IsLongTermElectronicArchivePurpose(ArchivePurpose);

        public bool IsHardDiskElectronicCarrier =>
            IsElectronicMedia && ArchiveOutboundDomainValues.IsHardDiskStorageCarrier(StorageCarrierType);

        public string ElectronicOutboundUsageHint
        {
            get
            {
                if (!IsElectronicMedia)
                {
                    return string.Empty;
                }

                var lines = new List<string>
                {
                    "任何方式出库的电子介质资料，其资料都将不再归还资料室。"
                };

                if (IsLongTermElectronicArchive
                    && UsageMode == ArchiveOutboundDomainValues.UsageModeDuplicate)
                {
                    lines.Add("长期存档电子介质仅支持拷贝借出，原件留存库内。");
                }

                return string.Join(Environment.NewLine, lines);
            }
        }

        public bool ShowElectronicOutboundUsageHint => IsElectronicMedia;

        public string SimulatedOutboundUsageHint =>
            IsSimulatedMedia
            && UsageMode == ArchiveOutboundDomainValues.UsageModeCopy
                ? "拷贝方式出库的模拟介质资料，其资料不再归还资料室。"
                : string.Empty;

        public bool ShowSimulatedOutboundUsageHint =>
            !string.IsNullOrWhiteSpace(SimulatedOutboundUsageHint);

        public string WithdrawalNeedReturnLabel => "提档资料是否归还";

        public bool ShowWithdrawalMaterialNeedReturn =>
            UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal && IsSimulatedMedia;

        public bool ShowWithdrawalHardDiskReturn =>
            UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal && IsHardDiskElectronicCarrier;

        public bool ShowDuplicateDiskNeedReturn => ShowBlankDiskFields;

        public IReadOnlyList<OutboundUsageModeOption> DuplicateMediumOptions { get; } =
            ArchiveOutboundDomainValues.DuplicateMediumOptions
                .Where(option => !string.Equals(
                    option.Value,
                    ArchiveOutboundDomainValues.DuplicateMediumInStockBlank,
                    StringComparison.Ordinal))
                .Select(option => new OutboundUsageModeOption(option.Value, option.Display))
                .ToList();

        public bool CanEdit
        {
            get => _canEdit;
            set
            {
                if (SetProperty(ref _canEdit, value))
                {
                    OnPropertyChanged(nameof(CanPickBlankDisk));
                    OnPropertyChanged(nameof(CanRevokeGroupRegistration));
                    OnPropertyChanged(nameof(ShowRevokeGroupRegistration));
                    OnPropertyChanged(nameof(CanEditRequisitionedDiskNeedReturn));
                    OnPropertyChanged(nameof(CanEditExpectedReturnDate));
                    foreach (var row in ItemRows)
                    {
                        row.CanEdit = value;
                    }

                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsElectronicMedia =>
            string.Equals(MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal);

        public bool IsSimulatedMedia =>
            string.Equals(MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal);

        public string UsageMode
        {
            get => _usageMode;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    // ComboBox 在 ItemsSource 刷新时可能回写空值，忽略以免领用方式被重置。
                    if (!string.IsNullOrWhiteSpace(_usageMode))
                    {
                        return;
                    }

                    value = ArchiveOutboundDomainValues.UsageModeWithdrawal;
                }

                string normalized = value.Trim();

                if (IsLongTermElectronicArchive
                    && normalized == ArchiveOutboundDomainValues.UsageModeWithdrawal)
                {
                    normalized = ArchiveOutboundDomainValues.UsageModeDuplicate;
                }

                if (_usageMode == normalized)
                {
                    return;
                }

                string previousMode = _usageMode;
                _usageMode = normalized;
                ApplyUsageModeTransition(previousMode, normalized);
                ApplyToItems();
                NotifyUsageRelatedPropertiesChanged(refreshUsageModeBinding: false);
                RaiseSharedDiskStateChanged();
            }
        }

        public bool ShowMaterialNeedReturn =>
            ShowWithdrawalMaterialNeedReturn;

        public string NeedReturnRadioGroup => _needReturnRadioGroup;

        public string WithdrawalHardDiskNeedReturnRadioGroup => _withdrawalHardDiskNeedReturnRadioGroup;

        public bool NeedReturn
        {
            get => _needReturn;
            set
            {
                if (_needReturn == value)
                {
                    return;
                }

                _needReturn = value;
                ApplyToItems();
                OnPropertyChanged();
                UpdateExpectedReturnDateApplicability();
            }
        }

        public bool ShowDuplicateSettings =>
            IsElectronicMedia && UsageMode == ArchiveOutboundDomainValues.UsageModeDuplicate;

        public string UseInStockBlankRadioGroup => _useInStockBlankRadioGroup;

        public bool UseInStockBlankDisk
        {
            get => _useInStockBlankDisk;
            set
            {
                if (_useInStockBlankDisk == value)
                {
                    return;
                }

                _useInStockBlankDisk = value;
                if (!value)
                {
                    ClearRequisitionedDisk();
                    EnsureSelfProvidedDuplicateMediumKind();
                }
                else
                {
                    _duplicateMediumKind = ArchiveOutboundDomainValues.DuplicateMediumInStockBlank;
                    _requisitionedDiskNeedReturn = true;
                }

                ApplyToItems();
                NotifyDuplicateRelatedPropertiesChanged();
                UpdateExpectedReturnDateApplicability();
                RaiseSharedDiskStateChanged();
            }
        }

        public bool ShowSelfProvidedDuplicateMedium =>
            ShowDuplicateSettings && !UseInStockBlankDisk;

        public string DuplicateMediumKind
        {
            get => _duplicateMediumKind;
            set
            {
                string normalized = value?.Trim() ?? string.Empty;
                if (_duplicateMediumKind == normalized)
                {
                    return;
                }

                _duplicateMediumKind = normalized;
                ApplyToItems();
                OnPropertyChanged();
            }
        }

        public bool ShowBlankDiskFields =>
            ShowDuplicateSettings && UseInStockBlankDisk;

        public string RequisitionedDiskCode
        {
            get => _requisitionedDiskCode;
            private set => SetProperty(ref _requisitionedDiskCode, value);
        }

        public bool CanPickBlankDisk => CanEdit && ShowBlankDiskFields;

        public int? RequisitionedMediumId =>
            ItemRows
                .Select(row => row.Source.RequisitionedMediumId)
                .FirstOrDefault(id => id is > 0);

        public bool IsSharedDiskSettingsReadOnly => _isSharedDiskSettingsReadOnly;

        public string SharedDiskSettingsHint => _sharedDiskSettingsHint;

        public bool ShowSharedDiskSettingsHint =>
            !string.IsNullOrWhiteSpace(_sharedDiskSettingsHint);

        public bool CanEditRequisitionedDiskNeedReturn =>
            CanEdit && ShowDuplicateDiskNeedReturn && !_isSharedDiskSettingsReadOnly;

        public bool CanEditExpectedReturnDate
        {
            get
            {
                if (!CanEdit || !RequiresExpectedReturnDate)
                {
                    return false;
                }

                return !ShowBlankDiskFields || !_isSharedDiskSettingsReadOnly;
            }
        }

        public bool ShowRequisitionedDiskNeedReturn => ShowDuplicateDiskNeedReturn;

        public string RequisitionedDiskNeedReturnRadioGroup => _requisitionedDiskNeedReturnRadioGroup;

        public bool RequisitionedDiskNeedReturn
        {
            get => _requisitionedDiskNeedReturn;
            set
            {
                if (_requisitionedDiskNeedReturn == value)
                {
                    return;
                }

                _requisitionedDiskNeedReturn = value;
                ApplyToItems();
                OnPropertyChanged();
                OnPropertyChanged(nameof(RequisitionedDiskNeedReturnDisplay));
                UpdateExpectedReturnDateApplicability();
                RaiseSharedDiskStateChanged();
            }
        }

        public bool RequiresExpectedReturnDate =>
            ArchiveOutboundReturnSupport.UnitRequiresExpectedReturnDate(
                UsageMode,
                NeedReturn,
                UseInStockBlankDisk,
                RequisitionedDiskNeedReturn,
                IsElectronicMedia,
                IsHardDiskElectronicCarrier);

        public DateTime? ExpectedReturnDate
        {
            get => _expectedReturnDate;
            set
            {
                DateTime? normalized = value?.Date;
                if (_expectedReturnDate == normalized)
                {
                    return;
                }

                _expectedReturnDate = normalized;
                ApplyToItems();
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExpectedReturnDateDisplay));
                RaiseSharedDiskStateChanged();
            }
        }

        public string ExpectedReturnDateDisplay =>
            ExpectedReturnDate?.ToString("yyyy-MM-dd") ?? "—";

        public string UsageModeDisplay => ItemRows.FirstOrDefault()?.UsageModeDisplay ?? string.Empty;

        public string NeedReturnDisplay => ShowWithdrawalMaterialNeedReturn || ShowWithdrawalHardDiskReturn
            ? NeedReturn ? "是" : "否"
            : "—";

        public string UseInStockBlankDiskDisplay => ShowDuplicateSettings
            ? UseInStockBlankDisk ? "是" : "否"
            : "—";

        public string DuplicateMediumKindDisplay =>
            ShowSelfProvidedDuplicateMedium
                ? ArchiveOutboundDomainValues.GetDuplicateMediumDisplay(DuplicateMediumKind)
                : "—";

        public string RequisitionedDiskNeedReturnDisplay => ShowDuplicateDiskNeedReturn
            ? RequisitionedDiskNeedReturn ? "是" : "否"
            : "—";

        /// <summary>
        /// 将单元级领用设置同步到组内全部明细。
        /// </summary>
        public void ApplyToItems()
        {
            foreach (var row in ItemRows)
            {
                ApplySettingsToItem(row.Source);
                row.RefreshDisplayProperties();
            }
        }

        private void LoadFromItems()
        {
            var sample = ItemRows.FirstOrDefault()?.Source;
            if (sample == null)
            {
                return;
            }

            _usageMode = sample.UsageMode;
            _needReturn = sample.NeedReturn;
            _useInStockBlankDisk = ResolveUseInStockBlankDiskFromItem(sample);
            _duplicateMediumKind = ResolveDuplicateMediumKindFromItem(sample);
            _requisitionedDiskCode = sample.RequisitionedDiskCode ?? string.Empty;
            _requisitionedDiskNeedReturn = sample.RequisitionedDiskNeedReturn;
            _expectedReturnDate = sample.ExpectedReturnDate?.Date;

            EnsureValidElectronicUsageMode();
            NotifyUsageRelatedPropertiesChanged();
            NotifyDuplicateRelatedPropertiesChanged();
        }

        private void ApplySettingsToItem(YearlyArchiveOutboundItem item)
        {
            item.UsageMode = UsageMode;
            item.NeedReturn = UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal
                && (IsSimulatedMedia || IsHardDiskElectronicCarrier)
                && NeedReturn;

            if (IsElectronicMedia)
            {
                item.CopyCount = 1;
            }
            else if (item.CopyCount is null or <= 0)
            {
                item.CopyCount = 1;
            }

            if (UsageMode == ArchiveOutboundDomainValues.UsageModeDuplicate && IsElectronicMedia)
            {
                if (UseInStockBlankDisk)
                {
                    ArchiveOutboundDomainValues.ApplyDuplicateMediumSelection(
                        item,
                        ArchiveOutboundDomainValues.DuplicateMediumInStockBlank);
                    item.RequisitionedDiskNeedReturn = RequisitionedDiskNeedReturn;
                    item.RequisitionedDiskCode = RequisitionedDiskCode;
                    int? mediumId = ItemRows
                        .Select(row => row.Source.RequisitionedMediumId)
                        .FirstOrDefault(id => id is > 0);
                    if (mediumId is > 0)
                    {
                        item.RequisitionedMediumId = mediumId;
                    }
                }
                else
                {
                    ArchiveOutboundDomainValues.ApplyDuplicateMediumSelection(item, DuplicateMediumKind);
                }
            }
            else
            {
                item.ElectronicMediaSource = string.Empty;
                item.ElectronicMediumType = string.Empty;
                item.RequisitionedMediumId = null;
                item.RequisitionedDiskCode = string.Empty;
                item.RequisitionedDiskNeedReturn = false;
            }

            item.ExpectedReturnDate = RequiresExpectedReturnDate ? ExpectedReturnDate : null;
        }

        private void PickBlankDisk()
        {
            if (!CanPickBlankDisk)
            {
                return;
            }

            var selectedMedia = _dialogService.ShowHardDiskMediumSelectionDialog(
                string.IsNullOrWhiteSpace(RequisitionedDiskCode)
                    ? null
                    : new[] { RequisitionedDiskCode },
                currentElectronicArchiveUnitId: null,
                selectionMode: BlankTargetSelectionMode);

            if (selectedMedia == null || selectedMedia.Count == 0)
            {
                return;
            }

            if (selectedMedia.Count > 1)
            {
                _dialogService.ShowMessage("请只选择一块库内空盘。", "提示");
                return;
            }

            var medium = selectedMedia[0];
            RequisitionedDiskCode = medium.DiskCode;
            _requisitionedDiskNeedReturn = true;

            foreach (var row in ItemRows)
            {
                row.Source.RequisitionedMediumId = medium.Id;
                row.Source.RequisitionedDiskCode = medium.DiskCode;
                row.Source.RequisitionedDiskNeedReturn = true;
                row.Source.ElectronicMediaSource = ArchiveOutboundDomainValues.ElectronicMediaSourceInStockBlank;
                row.Source.ElectronicMediumType = ArchiveOutboundDomainValues.DuplicateMediumInStockBlank;
                row.RefreshDisplayProperties();
            }

            ApplyToItems();
            NotifyDuplicateRelatedPropertiesChanged();
            UpdateExpectedReturnDateApplicability();
            RaiseSharedDiskStateChanged();
        }

        private void ClearRequisitionedDisk()
        {
            ClearRequisitionedDiskStateOnly();
            UpdateExpectedReturnDateApplicability();
            RaiseSharedDiskStateChanged();
        }

        /// <summary>
        /// 由申请单级协调器设置共用硬盘归还控件的只读状态与提示。
        /// </summary>
        internal void SetSharedDiskPresentation(bool readOnly, string hint)
        {
            string normalizedHint = hint?.Trim() ?? string.Empty;
            if (_isSharedDiskSettingsReadOnly == readOnly
                && string.Equals(_sharedDiskSettingsHint, normalizedHint, StringComparison.Ordinal))
            {
                OnPropertyChanged(nameof(CanEditRequisitionedDiskNeedReturn));
                OnPropertyChanged(nameof(CanEditExpectedReturnDate));
                return;
            }

            _isSharedDiskSettingsReadOnly = readOnly;
            _sharedDiskSettingsHint = normalizedHint;
            OnPropertyChanged(nameof(IsSharedDiskSettingsReadOnly));
            OnPropertyChanged(nameof(SharedDiskSettingsHint));
            OnPropertyChanged(nameof(ShowSharedDiskSettingsHint));
            OnPropertyChanged(nameof(CanEditRequisitionedDiskNeedReturn));
            OnPropertyChanged(nameof(CanEditExpectedReturnDate));
        }

        /// <summary>
        /// 从共用同一库内硬盘的其他介质袋同步归还设置。
        /// </summary>
        internal void ApplySharedDiskSettingsFromPeer(bool needReturn, DateTime? expectedReturnDate)
        {
            _suppressSharedDiskCallback = true;
            try
            {
                _requisitionedDiskNeedReturn = needReturn;
                _expectedReturnDate = needReturn ? expectedReturnDate?.Date : null;
                ApplyToItems();
                OnPropertyChanged(nameof(RequisitionedDiskNeedReturn));
                OnPropertyChanged(nameof(RequisitionedDiskNeedReturnDisplay));
                UpdateExpectedReturnDateApplicability();
            }
            finally
            {
                _suppressSharedDiskCallback = false;
            }
        }

        private void RaiseSharedDiskStateChanged()
        {
            if (_suppressSharedDiskCallback)
            {
                return;
            }

            _onSharedDiskStateChanged?.Invoke(this);
        }

        private void UpdateExpectedReturnDateApplicability()
        {
            if (!RequiresExpectedReturnDate && _expectedReturnDate.HasValue)
            {
                _expectedReturnDate = null;
                ApplyToItems();
            }

            OnPropertyChanged(nameof(RequiresExpectedReturnDate));
            OnPropertyChanged(nameof(ExpectedReturnDate));
            OnPropertyChanged(nameof(ExpectedReturnDateDisplay));
            OnPropertyChanged(nameof(CanEditExpectedReturnDate));
        }

        private void EnsureValidElectronicUsageMode()
        {
            if (!IsLongTermElectronicArchive)
            {
                return;
            }

            if (_usageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal)
            {
                string previousMode = _usageMode;
                _usageMode = ArchiveOutboundDomainValues.UsageModeDuplicate;
                ApplyUsageModeTransition(previousMode, _usageMode);
                ApplyToItems();
                NotifyUsageRelatedPropertiesChanged();
            }
        }

        private void ApplyUsageModeTransition(string previousMode, string newMode)
        {
            if (string.Equals(newMode, ArchiveOutboundDomainValues.UsageModeDuplicate, StringComparison.Ordinal)
                && IsElectronicMedia)
            {
                _needReturn = false;
                if (!string.Equals(previousMode, ArchiveOutboundDomainValues.UsageModeDuplicate, StringComparison.Ordinal))
                {
                    _useInStockBlankDisk = false;
                    ClearRequisitionedDiskStateOnly();
                    EnsureSelfProvidedDuplicateMediumKind();
                }
                else if (!_useInStockBlankDisk)
                {
                    EnsureSelfProvidedDuplicateMediumKind();
                }

                return;
            }

            if (!string.Equals(newMode, ArchiveOutboundDomainValues.UsageModeDuplicate, StringComparison.Ordinal))
            {
                _useInStockBlankDisk = false;
                ClearRequisitionedDiskStateOnly();
                EnsureSelfProvidedDuplicateMediumKind();

                if (string.Equals(newMode, ArchiveOutboundDomainValues.UsageModeWithdrawal, StringComparison.Ordinal))
                {
                    _needReturn = IsSimulatedMedia || IsHardDiskElectronicCarrier;
                }
            }
        }

        private static bool ResolveUseInStockBlankDiskFromItem(YearlyArchiveOutboundItem sample) =>
            string.Equals(sample.UsageMode, ArchiveOutboundDomainValues.UsageModeDuplicate, StringComparison.Ordinal)
            && string.Equals(
                sample.ElectronicMediaSource,
                ArchiveOutboundDomainValues.ElectronicMediaSourceInStockBlank,
                StringComparison.Ordinal);

        private static string ResolveDuplicateMediumKindFromItem(YearlyArchiveOutboundItem sample)
        {
            if (!string.Equals(sample.UsageMode, ArchiveOutboundDomainValues.UsageModeDuplicate, StringComparison.Ordinal))
            {
                return ArchiveOutboundDomainValues.DuplicateMediumSelfUsb;
            }

            if (ResolveUseInStockBlankDiskFromItem(sample))
            {
                return ArchiveOutboundDomainValues.DuplicateMediumInStockBlank;
            }

            string mediumKind = sample.ElectronicMediumType?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(mediumKind)
                || string.Equals(mediumKind, ArchiveOutboundDomainValues.DuplicateMediumInStockBlank, StringComparison.Ordinal))
            {
                return ArchiveOutboundDomainValues.DuplicateMediumSelfUsb;
            }

            return mediumKind;
        }

        private void EnsureSelfProvidedDuplicateMediumKind()
        {
            if (_useInStockBlankDisk)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_duplicateMediumKind)
                || string.Equals(_duplicateMediumKind, ArchiveOutboundDomainValues.DuplicateMediumInStockBlank, StringComparison.Ordinal)
                || !DuplicateMediumOptions.Any(option =>
                    string.Equals(option.Value, _duplicateMediumKind, StringComparison.Ordinal)))
            {
                _duplicateMediumKind = ArchiveOutboundDomainValues.DuplicateMediumSelfUsb;
            }
        }

        private void ClearRequisitionedDiskStateOnly()
        {
            RequisitionedDiskCode = string.Empty;
            _requisitionedDiskNeedReturn = false;

            foreach (var row in ItemRows)
            {
                row.Source.RequisitionedMediumId = null;
                row.Source.RequisitionedDiskCode = string.Empty;
                row.Source.RequisitionedDiskNeedReturn = false;
                row.RefreshDisplayProperties();
            }
        }

        private void NotifyUsageRelatedPropertiesChanged(bool refreshUsageModeBinding = true)
        {
            if (refreshUsageModeBinding)
            {
                OnPropertyChanged(nameof(UsageMode));
            }

            OnPropertyChanged(nameof(UsageModeDisplay));
            OnPropertyChanged(nameof(ShowMaterialNeedReturn));
            OnPropertyChanged(nameof(ShowWithdrawalMaterialNeedReturn));
            OnPropertyChanged(nameof(ShowWithdrawalHardDiskReturn));
            OnPropertyChanged(nameof(ShowDuplicateDiskNeedReturn));
            OnPropertyChanged(nameof(ShowElectronicOutboundUsageHint));
            OnPropertyChanged(nameof(SimulatedOutboundUsageHint));
            OnPropertyChanged(nameof(ShowSimulatedOutboundUsageHint));
            OnPropertyChanged(nameof(NeedReturn));
            OnPropertyChanged(nameof(NeedReturnDisplay));
            OnPropertyChanged(nameof(ShowDuplicateSettings));
            OnPropertyChanged(nameof(ShowSelfProvidedDuplicateMedium));
            OnPropertyChanged(nameof(IsLongTermElectronicArchive));
            OnPropertyChanged(nameof(IsHardDiskElectronicCarrier));
            OnPropertyChanged(nameof(ElectronicOutboundUsageHint));
            OnPropertyChanged(nameof(WithdrawalNeedReturnLabel));
            OnPropertyChanged(nameof(UseInStockBlankDiskDisplay));
            OnPropertyChanged(nameof(DuplicateMediumKindDisplay));
            UpdateExpectedReturnDateApplicability();
            NotifyDuplicateRelatedPropertiesChanged();
        }

        private void NotifyDuplicateRelatedPropertiesChanged()
        {
            OnPropertyChanged(nameof(UseInStockBlankDisk));
            OnPropertyChanged(nameof(ShowBlankDiskFields));
            OnPropertyChanged(nameof(RequisitionedDiskCode));
            OnPropertyChanged(nameof(CanPickBlankDisk));
            OnPropertyChanged(nameof(ShowRequisitionedDiskNeedReturn));
            OnPropertyChanged(nameof(RequisitionedDiskNeedReturn));
            OnPropertyChanged(nameof(RequisitionedDiskNeedReturnDisplay));
            OnPropertyChanged(nameof(DuplicateMediumKind));
            OnPropertyChanged(nameof(DuplicateMediumKindDisplay));
            OnPropertyChanged(nameof(ShowSelfProvidedDuplicateMedium));
            OnPropertyChanged(nameof(UseInStockBlankDiskDisplay));
            OnPropertyChanged(nameof(RequiresExpectedReturnDate));
            OnPropertyChanged(nameof(ExpectedReturnDateDisplay));
            OnPropertyChanged(nameof(CanEditRequisitionedDiskNeedReturn));
            OnPropertyChanged(nameof(CanEditExpectedReturnDate));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private static IReadOnlyList<OutboundUsageModeOption> BuildUsageModeOptions(
            string mediaKind,
            string? archivePurpose = null) =>
            ArchiveOutboundItemRowViewModel.BuildUsageModeOptions(mediaKind, archivePurpose);
    }
}
