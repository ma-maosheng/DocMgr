using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using DocMgr.Models.Cabinets;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.HardDiskMedia;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 资料离库处置办理弹窗 ViewModel（草稿编辑 + 审批办结工作台，布局对齐硬盘离库处置）。
    /// </summary>
    public sealed class ArchiveDisposalEditDialogViewModel : ViewModelBase
    {
        private const string AllFilterText = "全部";

        private readonly IArchiveDisposalService _disposalService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly ICabinetService _cabinetService;
        private readonly IUserService _userService;
        private readonly List<ArchiveDisposalCandidateRow> _candidatePool = new();
        private YearlyArchiveDisposalRecord _record;
        private bool _hasCommittedChanges;
        private bool _isApplyingFilters;
        private bool _suppressBatchMethodApply;
        private string _disposalNo = string.Empty;
        private string _reason = string.Empty;
        private string _remark = string.Empty;
        private string _approvalOpinion = "同意";
        private string _batchDispositionMethod = string.Empty;
        private string _uploadCategory = ArchiveDisposalDomainValues.AttachmentCategorySignedForm;
        private SystemAttachment? _selectedAttachment;
        private bool _physicalRemovalConfirmed;
        private bool _formatRetainedConfirmed;
        private bool _showPhysicalRemovalConfirm;
        private bool _showFormatRetainConfirm;
        private string _filterKeyword = string.Empty;
        private string _filterSourceRegisterKind = AllFilterText;
        private string _filterMediumKind = AllFilterText;
        private string _defaultArchiveRoomHead = string.Empty;
        private string _defaultProductionHead = string.Empty;
        private string _defaultArchiveDeputyPresident = string.Empty;
        private string _defaultProductionVicePresident = string.Empty;

        public ArchiveDisposalEditDialogViewModel(
            IArchiveDisposalService disposalService,
            IDialogService dialogService,
            IUserContextService userContextService,
            IHardDiskMediaService hardDiskMediaService,
            ICabinetService cabinetService,
            IUserService userService,
            YearlyArchiveDisposalRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            _disposalService = disposalService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _hardDiskMediaService = hardDiskMediaService;
            _cabinetService = cabinetService;
            _userService = userService;
            _record = record;

            MoveToDisposalCommand = new RelayCommand(_ => MoveToDisposal(), _ => CanEditHeader && AvailableItems.Any(i => i.IsSelected));
            MoveToAvailableCommand = new RelayCommand(_ => MoveToAvailable(), _ => CanEditHeader && Items.Any(i => i.IsSelected));
            ApplyDispositionMethodCommand = new RelayCommand(
                _ => ApplyDispositionMethodToSelected(BatchDispositionMethod),
                _ => CanEditHeader
                    && Items.Any(i => i.IsSelected)
                    && !string.IsNullOrWhiteSpace(BatchDispositionMethod));
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters(), _ => CanEditHeader);
            RecommendBlankSlotCommand = new RelayCommand<ArchiveDisposalItemRow>(
                async item => await RecommendBlankSlotAsync(item),
                item => CanEditBlankSlots
                        && item is { IsFormatRetain: true });
            ShowBlankSlotSnapshotCommand = new RelayCommand<ArchiveDisposalItemRow>(
                async item => await ShowBlankSlotSnapshotAsync(item),
                item => item is { IsFormatRetain: true }
                        && !string.IsNullOrWhiteSpace(item.TargetBlankSlotLocation));
            SaveDraftCommand = new RelayCommand(async _ => await SaveDraftAsync(), _ => CanEditHeader);
            SubmitCommand = new RelayCommand(async _ => await SubmitAsync(), _ => CanSubmit);
            ApproveCommand = new RelayCommand(async _ => await ApproveAsync(), _ => CanApprove);
            ConfirmUploadCommand = new RelayCommand(async _ => await ConfirmUploadAsync(), _ => CanConfirmUpload);
            UploadAttachmentCommand = new RelayCommand(async _ => await UploadAttachmentAsync(), _ => CanUploadAttachment);
            DeleteAttachmentCommand = new RelayCommand(
                async item => await DeleteAttachmentAsync(item as SystemAttachment),
                item => item is SystemAttachment && CanUploadAttachment);
            ViewAttachmentCommand = new RelayCommand(
                async item => await ViewAttachmentAsync(item as SystemAttachment),
                item => item is SystemAttachment);
            CompleteCommand = new RelayCommand(async _ => await CompleteAsync(), _ => CanComplete);
            PrintCommand = new RelayCommand(async _ => await PrintAsync(), _ => CanPrint);
            WithdrawCommand = new RelayCommand(async _ => await WithdrawAsync(), _ => CanWithdraw);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            _ = InitializeAsync();
        }

        public event Action<bool?>? RequestClose;

        public bool HasCommittedChanges => _hasCommittedChanges;

        public string WindowTitle =>
            $"{(IsSimulated ? "模拟" : "电子")}资料离库处置 · {(string.IsNullOrWhiteSpace(DisposalNo) ? "待编单" : DisposalNo)} · {StatusDisplay}";

        public string StatusDisplay => ArchiveDisposalDomainValues.ToStatusDisplay(_record.Status);

        public bool IsSimulated =>
            string.Equals(_record.MediaKind?.Trim(), ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal);

        public bool ShowMediumKindFilter => !IsSimulated;

        public string BannerText =>
            "流程：保存草稿 → 提交 → 打印签批单并线下签字 → 审批 → 确认可上传 → 上传签批单（销毁须现场照片）→ 办结。办结释档空盒/空袋前须确认物理移除；拟销硬盘低格留盘须确认已低格并填写目标空盘档口。";

        public ObservableCollection<ArchiveDisposalCandidateRow> AvailableItems { get; } = new();

        public ObservableCollection<ArchiveDisposalItemRow> Items { get; } = new();

        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public ObservableCollection<string> DispositionMethodOptions { get; } = new();

        public ObservableCollection<string> UploadCategoryOptions { get; } = new(ArchiveDisposalDomainValues.AttachmentCategoryOptions);

        public ObservableCollection<string> SourceRegisterKindFilterOptions { get; } = new();

        public ObservableCollection<string> MediumKindFilterOptions { get; } = new();

        public string AvailableItemsTitle => $"可选盘库资料（{AvailableItems.Count}）";

        public string DisposalItemsTitle => $"待处置明细（{Items.Count}）";

        public string FilterKeyword
        {
            get => _filterKeyword;
            set
            {
                if (SetProperty(ref _filterKeyword, value))
                {
                    RefreshAvailableItems();
                }
            }
        }

        public string FilterSourceRegisterKind
        {
            get => _filterSourceRegisterKind;
            set
            {
                if (SetProperty(ref _filterSourceRegisterKind, value))
                {
                    RefreshAvailableItems();
                }
            }
        }

        public string FilterMediumKind
        {
            get => _filterMediumKind;
            set
            {
                if (SetProperty(ref _filterMediumKind, value))
                {
                    RefreshAvailableItems();
                }
            }
        }

        public string DisposalNo
        {
            get => _disposalNo;
            private set
            {
                if (SetProperty(ref _disposalNo, value))
                {
                    OnPropertyChanged(nameof(WindowTitle));
                }
            }
        }

        public string ApplicantName => _record.ApplicantName;

        public string ApplicantDept => _record.ApplicantDept;

        /// <summary>默认资料室负责人（只读展示）。</summary>
        public string DefaultArchiveRoomHeadDisplay => EmptyAsDash(_defaultArchiveRoomHead);

        /// <summary>默认生产科负责人（只读展示）。</summary>
        public string DefaultProductionHeadDisplay => EmptyAsDash(_defaultProductionHead);

        /// <summary>默认分管资料室副院长（只读展示）。</summary>
        public string DefaultArchiveDeputyPresidentDisplay => EmptyAsDash(_defaultArchiveDeputyPresident);

        /// <summary>默认分管生产副院长（只读展示）。</summary>
        public string DefaultProductionVicePresidentDisplay => EmptyAsDash(_defaultProductionVicePresident);

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

        public string ApprovalOpinion
        {
            get => _approvalOpinion;
            set => SetProperty(ref _approvalOpinion, value);
        }

        public string BatchDispositionMethod
        {
            get => _batchDispositionMethod;
            set
            {
                if (!SetProperty(ref _batchDispositionMethod, value))
                {
                    return;
                }

                RefreshCommandStates();
                if (!_suppressBatchMethodApply)
                {
                    ApplyDispositionMethodToSelected(value);
                }
            }
        }

        public string UploadCategory
        {
            get => _uploadCategory;
            set => SetProperty(ref _uploadCategory, value);
        }

        public SystemAttachment? SelectedAttachment
        {
            get => _selectedAttachment;
            set => SetProperty(ref _selectedAttachment, value);
        }

        public bool PhysicalRemovalConfirmed
        {
            get => _physicalRemovalConfirmed;
            set => SetProperty(ref _physicalRemovalConfirmed, value);
        }

        public bool FormatRetainedConfirmed
        {
            get => _formatRetainedConfirmed;
            set => SetProperty(ref _formatRetainedConfirmed, value);
        }

        public bool ShowPhysicalRemovalConfirm
        {
            get => _showPhysicalRemovalConfirm;
            private set => SetProperty(ref _showPhysicalRemovalConfirm, value);
        }

        public bool ShowFormatRetainConfirm
        {
            get => _showFormatRetainConfirm;
            private set => SetProperty(ref _showFormatRetainConfirm, value);
        }

        public bool CanOperate =>
            ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);

        public bool CanEditHeader =>
            CanOperate && _record.Status == YearlyArchiveDisposalRecord.StatusDraft;

        /// <summary>办结前可编辑低格留盘目标空盘档口（不做预占用）。</summary>
        public bool CanEditBlankSlots =>
            CanOperate
            && _record.Status == YearlyArchiveDisposalRecord.StatusSignedUploaded
            && Items.Any(item => ArchiveDisposalDomainValues.IsFormatRetainMethod(item.DispositionMethod));

        public bool CanInteractItemsGrid => CanEditHeader || CanEditBlankSlots;

        public bool ShowBlankSlotColumn =>
            Items.Any(item => ArchiveDisposalDomainValues.IsFormatRetainMethod(item.DispositionMethod));

        public bool CanSubmit =>
            CanOperate && _record.Status == YearlyArchiveDisposalRecord.StatusDraft && _record.Id > 0 && Items.Count > 0;

        public bool CanApprove =>
            CanOperate && _record.Status == YearlyArchiveDisposalRecord.StatusSubmitted;

        public bool CanConfirmUpload =>
            CanOperate && _record.Status == YearlyArchiveDisposalRecord.StatusApproved;

        public bool CanUploadAttachment =>
            CanOperate
            && (_record.Status == YearlyArchiveDisposalRecord.StatusApproved
                || _record.Status == YearlyArchiveDisposalRecord.StatusSignedUploaded);

        public bool CanComplete =>
            CanOperate && _record.Status == YearlyArchiveDisposalRecord.StatusSignedUploaded;

        public bool CanPrint =>
            CanOperate
            && _record.Id > 0
            && _record.Status is not YearlyArchiveDisposalRecord.StatusDraft
                and not YearlyArchiveDisposalRecord.StatusWithdrawn
                and not YearlyArchiveDisposalRecord.StatusForceWithdrawn;

        public bool CanWithdraw =>
            CanOperate
            && _record.Id > 0
            && _record.Status is not YearlyArchiveDisposalRecord.StatusCompleted
                and not YearlyArchiveDisposalRecord.StatusWithdrawn
                and not YearlyArchiveDisposalRecord.StatusForceWithdrawn;

        public RelayCommand MoveToDisposalCommand { get; }
        public RelayCommand MoveToAvailableCommand { get; }
        public RelayCommand ApplyDispositionMethodCommand { get; }
        public RelayCommand ClearFiltersCommand { get; }
        public RelayCommand<ArchiveDisposalItemRow> RecommendBlankSlotCommand { get; }
        public RelayCommand<ArchiveDisposalItemRow> ShowBlankSlotSnapshotCommand { get; }
        public RelayCommand SaveDraftCommand { get; }
        public RelayCommand SubmitCommand { get; }
        public RelayCommand ApproveCommand { get; }
        public RelayCommand ConfirmUploadCommand { get; }
        public RelayCommand UploadAttachmentCommand { get; }
        public RelayCommand DeleteAttachmentCommand { get; }
        public RelayCommand ViewAttachmentCommand { get; }
        public RelayCommand CompleteCommand { get; }
        public RelayCommand PrintCommand { get; }
        public RelayCommand WithdrawCommand { get; }
        public RelayCommand CloseCommand { get; }

        private async Task InitializeAsync()
        {
            try
            {
                BindFromRecord(_record);
                ReloadDefaultApprovers();
                if (_record.Id <= 0 && string.IsNullOrWhiteSpace(_record.DisposalNo))
                {
                    DisposalNo = await _disposalService.GenerateNextDisposalNoAsync();
                    _record.DisposalNo = DisposalNo;
                }
                else
                {
                    DisposalNo = _record.DisposalNo?.Trim() ?? string.Empty;
                }

                await ReloadCandidatePoolAsync();
                if (_record.Id > 0)
                {
                    await ReloadAttachmentsAsync();
                    await RefreshCompleteHintsAsync();
                }

                RefreshCommandStates();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private void ReloadDefaultApprovers()
        {
            var approvers = ArchiveDisposalDefaultApproverSupport.Resolve(_userService.GetAllUsers());
            _defaultArchiveRoomHead = approvers.ArchiveRoomHead;
            _defaultProductionHead = approvers.ProductionHead;
            _defaultArchiveDeputyPresident = approvers.ArchiveDeputyPresident;
            _defaultProductionVicePresident = approvers.ProductionVicePresident;
            OnPropertyChanged(nameof(DefaultArchiveRoomHeadDisplay));
            OnPropertyChanged(nameof(DefaultProductionHeadDisplay));
            OnPropertyChanged(nameof(DefaultArchiveDeputyPresidentDisplay));
            OnPropertyChanged(nameof(DefaultProductionVicePresidentDisplay));
        }

        private static string EmptyAsDash(string? value)
            => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

        private void BindFromRecord(YearlyArchiveDisposalRecord record)
        {
            _record = record;
            DisposalNo = record.DisposalNo?.Trim() ?? DisposalNo;
            Reason = record.Reason;
            Remark = record.Remark;
            ApprovalOpinion = string.IsNullOrWhiteSpace(record.ApprovalOpinion) ? "同意" : record.ApprovalOpinion;
            PhysicalRemovalConfirmed = record.PhysicalRemovalConfirmed;
            FormatRetainedConfirmed = record.FormatRetainedConfirmed;

            Items.Clear();
            foreach (var item in record.Items.OrderBy(i => i.SortOrder))
            {
                Items.Add(ArchiveDisposalItemRow.FromEntity(item));
            }

            RefreshDispositionMethodOptions();
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(ApplicantName));
            OnPropertyChanged(nameof(ApplicantDept));
            OnPropertyChanged(nameof(ShowMediumKindFilter));
            OnPropertyChanged(nameof(AvailableItemsTitle));
            OnPropertyChanged(nameof(DisposalItemsTitle));
            OnPropertyChanged(nameof(CanEditHeader));
            OnPropertyChanged(nameof(CanEditBlankSlots));
            OnPropertyChanged(nameof(CanInteractItemsGrid));
            OnPropertyChanged(nameof(ShowBlankSlotColumn));
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(CanApprove));
            OnPropertyChanged(nameof(CanConfirmUpload));
            OnPropertyChanged(nameof(CanUploadAttachment));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanPrint));
            OnPropertyChanged(nameof(CanWithdraw));
        }

        private async Task ReloadCandidatePoolAsync()
        {
            var selectable = await _disposalService.GetSelectableItemsAsync(
                _record.MediaKind,
                _record.Id > 0 ? _record.Id : null);

            _candidatePool.Clear();
            foreach (var item in selectable.OrderBy(i => i.DisplayTitle, StringComparer.Ordinal))
            {
                _candidatePool.Add(new ArchiveDisposalCandidateRow(item));
            }

            RebuildFilterOptions();
            RefreshAvailableItems();
        }

        private void RebuildFilterOptions()
        {
            _isApplyingFilters = true;
            try
            {
                string previousKind = FilterSourceRegisterKind;
                string previousMedium = FilterMediumKind;

                SourceRegisterKindFilterOptions.Clear();
                SourceRegisterKindFilterOptions.Add(AllFilterText);
                foreach (var value in _candidatePool
                    .Select(item => item.SourceRegisterKind)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal))
                {
                    SourceRegisterKindFilterOptions.Add(value);
                }

                MediumKindFilterOptions.Clear();
                MediumKindFilterOptions.Add(AllFilterText);
                foreach (var value in _candidatePool
                    .Select(item => item.MediumKind)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal))
                {
                    MediumKindFilterOptions.Add(value);
                }

                _filterSourceRegisterKind = SourceRegisterKindFilterOptions.Contains(previousKind)
                    ? previousKind
                    : AllFilterText;
                _filterMediumKind = MediumKindFilterOptions.Contains(previousMedium)
                    ? previousMedium
                    : AllFilterText;
                OnPropertyChanged(nameof(FilterSourceRegisterKind));
                OnPropertyChanged(nameof(FilterMediumKind));
            }
            finally
            {
                _isApplyingFilters = false;
            }
        }

        private void RefreshAvailableItems()
        {
            if (_isApplyingFilters)
            {
                return;
            }

            HashSet<string> selectedKeys = Items.Select(i => i.SelectionKey).ToHashSet(StringComparer.Ordinal);
            IEnumerable<ArchiveDisposalCandidateRow> query = _candidatePool
                .Where(item => !selectedKeys.Contains(item.SelectionKey));

            if (!string.IsNullOrWhiteSpace(FilterKeyword))
            {
                string keyword = FilterKeyword.Trim();
                query = query.Where(item =>
                    item.DisplayTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.BeforeStorageLocation.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.ContainerCode.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.ElectronicArchiveNo.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.MediumCode.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.MaterialName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.ItemName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || item.FormNo.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(FilterSourceRegisterKind, AllFilterText, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(FilterSourceRegisterKind))
            {
                query = query.Where(item =>
                    string.Equals(item.SourceRegisterKind, FilterSourceRegisterKind, StringComparison.Ordinal));
            }

            if (ShowMediumKindFilter
                && !string.Equals(FilterMediumKind, AllFilterText, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(FilterMediumKind))
            {
                query = query.Where(item =>
                    string.Equals(item.MediumKind, FilterMediumKind, StringComparison.Ordinal));
            }

            AvailableItems.Clear();
            foreach (var item in query.OrderBy(row => row.DisplayTitle, StringComparer.Ordinal))
            {
                item.IsSelected = false;
                AvailableItems.Add(item);
            }

            OnPropertyChanged(nameof(AvailableItemsTitle));
            OnPropertyChanged(nameof(DisposalItemsTitle));
            RefreshCommandStates();
        }

        private void ClearFilters()
        {
            _isApplyingFilters = true;
            try
            {
                _filterKeyword = string.Empty;
                _filterSourceRegisterKind = AllFilterText;
                _filterMediumKind = AllFilterText;
                OnPropertyChanged(nameof(FilterKeyword));
                OnPropertyChanged(nameof(FilterSourceRegisterKind));
                OnPropertyChanged(nameof(FilterMediumKind));
            }
            finally
            {
                _isApplyingFilters = false;
            }

            RefreshAvailableItems();
        }

        /// <summary>为低格留盘明细推荐空白硬盘专用档口（不做预占用）。</summary>
        private async Task RecommendBlankSlotAsync(ArchiveDisposalItemRow? item)
        {
            if (item == null || !CanEditBlankSlots || !item.IsFormatRetain)
            {
                return;
            }

            try
            {
                var options = await _hardDiskMediaService.GetOrderedBlankDedicatedSlotLocationOptionsAsync();
                if (options.Count == 0)
                {
                    _dialogService.ShowMessage("未找到空白硬盘专用档口，请先在磁盘柜开柜界面完成设置。", "推荐档口");
                    return;
                }

                string? recommended = await _hardDiskMediaService.RecommendBlankDedicatedSlotLocationAsync();
                string slot = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(recommended);
                if (string.IsNullOrWhiteSpace(slot))
                {
                    _dialogService.ShowMessage("当前未找到可用的空白硬盘专用档口。", "推荐档口");
                    return;
                }

                var matched = options.FirstOrDefault(option =>
                    string.Equals(
                        HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(option.Location),
                        slot,
                        StringComparison.OrdinalIgnoreCase));
                item.TargetBlankSlotLocation = slot;
                RefreshCommandStates();
                string hint = matched == null
                    ? slot
                    : $"{matched.DisplayText}";
                _dialogService.ShowMessage($"已推荐档口：{hint}", "推荐档口");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        /// <summary>打开低格留盘目标空盘档口的占用快照。</summary>
        private async Task ShowBlankSlotSnapshotAsync(ArchiveDisposalItemRow? item)
        {
            if (item == null || !item.IsFormatRetain)
            {
                return;
            }

            try
            {
                string location = item.TargetBlankSlotLocation?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(location))
                {
                    _dialogService.ShowMessage("请先填写或推荐空盘档口后再查看快照。", "档口快照");
                    return;
                }

                if (!TryParseCabinetLocation(location, out string cabinetName, out CabinetFace face, out string slotCode))
                {
                    _dialogService.ShowMessage("空盘档口无法解析，请核对格式（如：柜名A-1-2）后再查看快照。", "档口快照");
                    return;
                }

                var cabinet = (await _cabinetService.GetAllCabinetsAsync())
                    .FirstOrDefault(c =>
                        c.Type == CabinetType.MagneticDisk
                        && string.Equals(c.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
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
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private static bool TryParseCabinetLocation(
            string? location,
            out string cabinetName,
            out CabinetFace face,
            out string slotCode)
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

        private void RefreshDispositionMethodOptions()
        {
            _suppressBatchMethodApply = true;
            try
            {
                DispositionMethodOptions.Clear();
                var methods = Items
                    .SelectMany(item => ArchiveDisposalDomainValues.ResolveAllowedMethods(
                        _record.MediaKind,
                        item.DisposalReason,
                        item.MediumKind))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                foreach (var method in methods)
                {
                    DispositionMethodOptions.Add(method);
                }

                if (DispositionMethodOptions.Count > 0
                    && !DispositionMethodOptions.Contains(BatchDispositionMethod, StringComparer.Ordinal))
                {
                    _batchDispositionMethod = DispositionMethodOptions[0];
                    OnPropertyChanged(nameof(BatchDispositionMethod));
                }
            }
            finally
            {
                _suppressBatchMethodApply = false;
            }
        }

        private void MoveToDisposal()
        {
            var selected = AvailableItems.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            foreach (var row in selected)
            {
                if (Items.Any(item => string.Equals(item.SelectionKey, row.SelectionKey, StringComparison.Ordinal)))
                {
                    continue;
                }

                Items.Add(ArchiveDisposalItemRow.FromSelectable(row.Source));
            }

            RenumberItems();
            RefreshDispositionMethodOptions();
            RefreshAvailableItems();
        }

        private void MoveToAvailable()
        {
            var selected = Items.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            foreach (var row in selected)
            {
                Items.Remove(row);
            }

            RenumberItems();
            RefreshDispositionMethodOptions();
            RefreshAvailableItems();
        }

        private void ApplyDispositionMethodToSelected(string? method)
        {
            if (!CanEditHeader)
            {
                return;
            }

            string normalized = ArchiveDisposalDomainValues.NormalizeDispositionMethod(method);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            var selected = Items.Where(i => i.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            var distinctReasons = selected
                .Select(item => item.DisposalReason?.Trim() ?? string.Empty)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (distinctReasons.Count > 1)
            {
                _dialogService.ShowMessage(
                    "所选明细的「离库原因」不一致，请按同一原因分批勾选后再赋值。",
                    "无法赋值");
                return;
            }

            foreach (var item in selected)
            {
                var allowed = ArchiveDisposalDomainValues.ResolveAllowedMethods(
                    _record.MediaKind,
                    item.DisposalReason,
                    item.MediumKind);
                if (!allowed.Contains(normalized, StringComparer.Ordinal))
                {
                    _dialogService.ShowError($"「{item.DisplayTitle}」不允许处置方式「{normalized}」。");
                    return;
                }
            }

            foreach (var item in selected)
            {
                item.DispositionMethod = normalized;
            }
        }

        private void RenumberItems()
        {
            int sort = 1;
            foreach (var item in Items)
            {
                item.SortOrder = sort++;
                item.IsSelected = false;
            }

            OnPropertyChanged(nameof(DisposalItemsTitle));
        }

        private List<YearlyArchiveDisposalItem> BuildEntityItems()
        {
            return Items.Select(item => item.ToEntity()).ToList();
        }

        private async Task SaveDraftAsync()
        {
            try
            {
                var user = RequireUser();
                var draft = new YearlyArchiveDisposalRecord
                {
                    Id = _record.Id,
                    DisposalNo = DisposalNo,
                    MediaKind = _record.MediaKind,
                    Reason = Reason,
                    Remark = Remark
                };
                var items = BuildEntityItems();
                var saved = _record.Id > 0
                    ? await _disposalService.UpdateDraftAsync(draft, items, user)
                    : await _disposalService.CreateDraftAsync(draft, items, user);

                _hasCommittedChanges = true;
                BindFromRecord(saved);
                await ReloadCandidatePoolAsync();
                _dialogService.ShowMessage("草稿已保存。");
                RefreshCommandStates();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task SubmitAsync()
        {
            try
            {
                if (_record.Id <= 0 || HasUnsavedHeaderChanges())
                {
                    await SaveDraftAsync();
                    if (_record.Id <= 0)
                    {
                        return;
                    }
                }

                if (!_dialogService.ShowConfirm("确认提交该离库处置单？提交后将锁定关联介质。"))
                {
                    return;
                }

                await _disposalService.SubmitAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                await ReloadRecordAsync();
                _dialogService.ShowMessage("已提交，待审批。");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ApproveAsync()
        {
            try
            {
                if (!_dialogService.ShowConfirm("确认审批通过该离库处置单？"))
                {
                    return;
                }

                await _disposalService.ApproveAsync(_record.Id, ApprovalOpinion, RequireUser());
                _hasCommittedChanges = true;
                await ReloadRecordAsync();
                _dialogService.ShowMessage("审批已通过。");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ConfirmUploadAsync()
        {
            try
            {
                if (!_dialogService.ShowConfirm("确认进入上传签批单阶段？请先打印签批单并完成线下签字。"))
                {
                    return;
                }

                await _disposalService.ConfirmReadyForUploadAsync(_record.Id, RequireUser());
                _hasCommittedChanges = true;
                await ReloadRecordAsync();
                _dialogService.ShowMessage("已确认可上传签批单。");
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
                await RefreshCompleteHintsAsync();
                if (ShowPhysicalRemovalConfirm && !PhysicalRemovalConfirmed)
                {
                    _dialogService.ShowError("请勾选确认：已完成处置后空档案盒/介质袋的物理移除。");
                    return;
                }

                if (ShowFormatRetainConfirm && !FormatRetainedConfirmed)
                {
                    _dialogService.ShowError("请勾选确认：拟销硬盘已完成低级格式化。");
                    return;
                }

                if (ShowFormatRetainConfirm)
                {
                    var missingSlot = Items
                        .Where(item => ArchiveDisposalDomainValues.IsFormatRetainMethod(item.DispositionMethod))
                        .FirstOrDefault(item => string.IsNullOrWhiteSpace(item.TargetBlankSlotLocation));
                    if (missingSlot != null)
                    {
                        _dialogService.ShowError($"「{missingSlot.DisplayTitle}」为低格留盘，请先填写目标空盘档口。");
                        return;
                    }
                }

                if (!_dialogService.ShowConfirm("确认办结？办结后将写入正式清账结果并释档空盒/空袋。"))
                {
                    return;
                }

                var blankSlots = Items
                    .Where(item => ArchiveDisposalDomainValues.IsFormatRetainMethod(item.DispositionMethod) && item.Id > 0)
                    .ToDictionary(
                        item => item.Id,
                        item => item.TargetBlankSlotLocation?.Trim() ?? string.Empty);

                await _disposalService.CompleteAsync(
                    _record.Id,
                    RequireUser(),
                    PhysicalRemovalConfirmed,
                    FormatRetainedConfirmed,
                    blankSlots);
                _hasCommittedChanges = true;
                await ReloadRecordAsync();
                _dialogService.ShowMessage("已办结。");
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
                if (!_dialogService.ShowConfirm("确认撤回作废该离库处置单？"))
                {
                    return;
                }

                await _disposalService.WithdrawAsync(_record.Id, null, RequireUser());
                _hasCommittedChanges = true;
                await ReloadRecordAsync();
                _dialogService.ShowMessage("已撤回作废。");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task PrintAsync()
        {
            try
            {
                var data = await _disposalService.BuildPrintDataAsync(_record.Id);
                FlowDocument document = ArchiveDisposalPrintDocumentFactory.Create(data);
                var previewWindow = new PrintPreviewWindow(document);
                previewWindow.ShowDialog();
                await _disposalService.RecordPrintAsync(_record.Id);
                _hasCommittedChanges = true;
                await ReloadRecordAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task UploadAttachmentAsync()
        {
            try
            {
                if (_record.Status == YearlyArchiveDisposalRecord.StatusApproved
                    && !string.Equals(UploadCategory, ArchiveDisposalDomainValues.AttachmentCategoryOther, StringComparison.Ordinal))
                {
                    _dialogService.ShowMessage("请先点击「确认可上传」，再上传签批单或处置现场照片。");
                    return;
                }

                string? path = _dialogService.OpenFileDialog(
                    "图片与文档|*.jpg;*.jpeg;*.png;*.bmp;*.pdf;*.doc;*.docx|所有文件|*.*",
                    "选择附件");
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return;
                }

                byte[] content = await File.ReadAllBytesAsync(path);
                string fileName = Path.GetFileName(path);
                string extension = Path.GetExtension(path);
                var (ok, message, _) = await _disposalService.UploadAttachmentAsync(
                    _record.Id,
                    UploadCategory,
                    fileName,
                    extension,
                    content.LongLength,
                    content,
                    RequireUser());
                if (!ok)
                {
                    _dialogService.ShowError(message);
                    return;
                }

                _hasCommittedChanges = true;
                await ReloadAttachmentsAsync();
                await ReloadRecordAsync();
                _dialogService.ShowMessage(message);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task DeleteAttachmentAsync(SystemAttachment? attachment)
        {
            if (attachment == null)
            {
                return;
            }

            try
            {
                if (!_dialogService.ShowConfirm($"确认删除附件【{attachment.FileName}】？"))
                {
                    return;
                }

                var (ok, message) = await _disposalService.DeleteAttachmentAsync(attachment.Id, RequireUser());
                if (!ok)
                {
                    _dialogService.ShowError(message);
                    return;
                }

                _hasCommittedChanges = true;
                await ReloadAttachmentsAsync();
                await ReloadRecordAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ViewAttachmentAsync(SystemAttachment? attachment)
        {
            if (attachment == null)
            {
                return;
            }

            try
            {
                var latest = await _disposalService.GetAttachmentByIdAsync(attachment.Id);
                if (latest == null)
                {
                    _dialogService.ShowError("附件不存在。");
                    return;
                }

                _dialogService.ShowSystemAttachmentView(latest);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ReloadRecordAsync()
        {
            var latest = await _disposalService.GetRecordByIdAsync(_record.Id);
            if (latest != null)
            {
                BindFromRecord(latest);
                await ReloadCandidatePoolAsync();
                await RefreshCompleteHintsAsync();
            }

            RefreshCommandStates();
        }

        private async Task ReloadAttachmentsAsync()
        {
            if (string.IsNullOrWhiteSpace(_record.DisposalNo))
            {
                Attachments.Clear();
                return;
            }

            var list = await _disposalService.GetAttachmentsAsync(_record.DisposalNo);
            Attachments.Clear();
            foreach (var item in list)
            {
                Attachments.Add(item);
            }
        }

        private async Task RefreshCompleteHintsAsync()
        {
            if (_record.Id <= 0)
            {
                ShowPhysicalRemovalConfirm = false;
                ShowFormatRetainConfirm = false;
                return;
            }

            ShowPhysicalRemovalConfirm = await _disposalService.RequiresPhysicalRemovalConfirmationAsync(_record.Id);
            ShowFormatRetainConfirm = await _disposalService.RequiresFormatRetainConfirmationAsync(_record.Id);
            OnPropertyChanged(nameof(CanEditBlankSlots));
            OnPropertyChanged(nameof(CanInteractItemsGrid));
            OnPropertyChanged(nameof(ShowBlankSlotColumn));
        }

        private bool HasUnsavedHeaderChanges()
        {
            static string BuildKey(int mediumId, string? mediumKind, int filingFactId) =>
                mediumId > 0
                    ? $"M:{mediumKind?.Trim()}:{mediumId}"
                    : $"F:{filingFactId}";

            return !string.Equals(_record.Reason ?? string.Empty, Reason ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(_record.Remark ?? string.Empty, Remark ?? string.Empty, StringComparison.Ordinal)
                || !_record.Items
                    .Select(item => BuildKey(item.MediumId, item.MediumKind, item.FilingFactId))
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .SequenceEqual(Items.Select(item => item.SelectionKey).OrderBy(key => key, StringComparer.Ordinal))
                || !_record.Items
                    .OrderBy(item => BuildKey(item.MediumId, item.MediumKind, item.FilingFactId), StringComparer.Ordinal)
                    .Select(item => $"{BuildKey(item.MediumId, item.MediumKind, item.FilingFactId)}|{item.DispositionMethod?.Trim()}")
                    .SequenceEqual(Items
                        .OrderBy(item => item.SelectionKey, StringComparer.Ordinal)
                        .Select(item => $"{item.SelectionKey}|{item.DispositionMethod}"));
        }

        private void RefreshCommandStates()
        {
            CommandManager.InvalidateRequerySuggested();
            OnPropertyChanged(nameof(CanEditHeader));
            OnPropertyChanged(nameof(CanEditBlankSlots));
            OnPropertyChanged(nameof(CanInteractItemsGrid));
            OnPropertyChanged(nameof(ShowBlankSlotColumn));
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(CanApprove));
            OnPropertyChanged(nameof(CanConfirmUpload));
            OnPropertyChanged(nameof(CanUploadAttachment));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanPrint));
            OnPropertyChanged(nameof(CanWithdraw));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(StatusDisplay));
        }

        private Models.SystemSettings.User RequireUser()
        {
            return _userContextService.CurrentUser
                ?? throw new InvalidOperationException("当前用户无效。");
        }
    }

    /// <summary>
    /// 可选盘库资料行。
    /// </summary>
    public sealed class ArchiveDisposalCandidateRow : ViewModelBase
    {
        private bool _isSelected;

        public ArchiveDisposalCandidateRow(ArchiveDisposalSelectableItem source)
        {
            Source = source;
        }

        public ArchiveDisposalSelectableItem Source { get; }

        public string SelectionKey => Source.SelectionKey;

        public string DisplayTitle => Source.DisplayTitle;

        public string DisposalReason => Source.DisposalReason;

        public string SourceRegisterKind => Source.SourceRegisterKind;

        public string BeforeStorageLocation => Source.BeforeStorageLocation;

        public string ContainerCode => Source.ContainerCode;

        public string ElectronicArchiveNo => Source.ElectronicArchiveNo;

        public string MediumKind => Source.MediumKind;

        public string MediumCode => Source.MediumCode;

        public string MaterialName => Source.MaterialName;

        public string ItemName => Source.ItemName;

        public string FormNo => Source.FormNo;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    /// <summary>
    /// 待处置明细行。
    /// </summary>
    public sealed class ArchiveDisposalItemRow : ViewModelBase
    {
        private bool _isSelected;
        private string _dispositionMethod = string.Empty;
        private string _targetBlankSlotLocation = string.Empty;

        /// <summary>已持久化明细主键；新建未保存行为 0。</summary>
        public int Id { get; set; }

        public int SortOrder { get; set; }

        public int FilingFactId { get; set; }

        public int ContainerId { get; set; }

        public string ContainerCode { get; set; } = string.Empty;

        public string BeforeStorageLocation { get; set; } = string.Empty;

        public string SourceRegisterKind { get; set; } = string.Empty;

        public string DisposalReason { get; set; } = string.Empty;

        public string DispositionMethod
        {
            get => _dispositionMethod;
            set
            {
                if (SetProperty(ref _dispositionMethod, value))
                {
                    OnPropertyChanged(nameof(IsFormatRetain));
                }
            }
        }

        public string MaterialName { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string FormNo { get; set; } = string.Empty;

        public int InventoryLostCopyCount { get; set; }

        public int InventoryScrapCopyCount { get; set; }

        public string BeforeLifecycleStatus { get; set; } = string.Empty;

        public string MediumKind { get; set; } = string.Empty;

        public int MediumId { get; set; }

        public string MediumCode { get; set; } = string.Empty;

        public int ElectronicArchiveUnitId { get; set; }

        public string ElectronicArchiveNo { get; set; } = string.Empty;

        public string BeforeMediaStatus { get; set; } = string.Empty;

        public string TargetBlankSlotLocation
        {
            get => _targetBlankSlotLocation;
            set => SetProperty(ref _targetBlankSlotLocation, value);
        }

        public bool IsFormatRetain =>
            ArchiveDisposalDomainValues.IsFormatRetainMethod(DispositionMethod);

        public string DisplayTitle =>
            MediumId > 0
                ? $"{MediumKind} {MediumCode}（{ElectronicArchiveNo}）"
                : $"[{ContainerCode}] {(string.IsNullOrWhiteSpace(ItemName) ? MaterialName : ItemName)}";

        public string SelectionKey =>
            MediumId > 0 ? $"M:{MediumKind}:{MediumId}" : $"F:{FilingFactId}";

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public static ArchiveDisposalItemRow FromSelectable(ArchiveDisposalSelectableItem source)
        {
            string method = ArchiveDisposalDomainValues.ResolveDefaultMethod(
                source.MediaKind,
                source.DisposalReason,
                source.MediumKind);
            return new ArchiveDisposalItemRow
            {
                FilingFactId = source.FilingFactId,
                ContainerId = source.ContainerId,
                ContainerCode = source.ContainerCode,
                BeforeStorageLocation = source.BeforeStorageLocation,
                SourceRegisterKind = source.SourceRegisterKind,
                DisposalReason = source.DisposalReason,
                DispositionMethod = method,
                MaterialName = source.MaterialName,
                ItemName = source.ItemName,
                FormNo = source.FormNo,
                InventoryLostCopyCount = source.InventoryLostCopyCount,
                InventoryScrapCopyCount = source.InventoryScrapCopyCount,
                BeforeLifecycleStatus = source.BeforeLifecycleStatus,
                MediumKind = source.MediumKind,
                MediumId = source.MediumId,
                MediumCode = source.MediumCode,
                ElectronicArchiveUnitId = source.ElectronicArchiveUnitId,
                ElectronicArchiveNo = source.ElectronicArchiveNo,
                BeforeMediaStatus = source.BeforeMediaStatus
            };
        }

        public static ArchiveDisposalItemRow FromEntity(YearlyArchiveDisposalItem item)
        {
            return new ArchiveDisposalItemRow
            {
                Id = item.Id,
                SortOrder = item.SortOrder,
                FilingFactId = item.FilingFactId,
                ContainerId = item.ContainerId,
                ContainerCode = item.ContainerCode,
                BeforeStorageLocation = item.BeforeStorageLocation,
                SourceRegisterKind = item.SourceRegisterKind,
                DisposalReason = item.DisposalReason,
                DispositionMethod = ArchiveDisposalDomainValues.NormalizeDispositionMethod(item.DispositionMethod),
                MaterialName = item.MaterialName,
                ItemName = item.ItemName,
                FormNo = item.FormNo,
                InventoryLostCopyCount = item.InventoryLostCopyCount,
                InventoryScrapCopyCount = item.InventoryScrapCopyCount,
                BeforeLifecycleStatus = item.BeforeLifecycleStatus,
                MediumKind = item.MediumKind,
                MediumId = item.MediumId,
                MediumCode = item.MediumCode,
                ElectronicArchiveUnitId = item.ElectronicArchiveUnitId,
                ElectronicArchiveNo = item.ElectronicArchiveNo,
                BeforeMediaStatus = item.BeforeMediaStatus,
                TargetBlankSlotLocation = item.TargetBlankSlotLocation
            };
        }

        public YearlyArchiveDisposalItem ToEntity()
        {
            return new YearlyArchiveDisposalItem
            {
                Id = Id,
                SortOrder = SortOrder,
                FilingFactId = FilingFactId,
                ContainerId = ContainerId,
                ContainerCode = ContainerCode,
                BeforeStorageLocation = BeforeStorageLocation,
                SourceRegisterKind = SourceRegisterKind,
                DisposalReason = DisposalReason,
                DispositionMethod = ArchiveDisposalDomainValues.NormalizeDispositionMethod(DispositionMethod),
                MaterialName = MaterialName,
                ItemName = ItemName,
                FormNo = FormNo,
                InventoryLostCopyCount = InventoryLostCopyCount,
                InventoryScrapCopyCount = InventoryScrapCopyCount,
                BeforeLifecycleStatus = BeforeLifecycleStatus,
                MediumKind = MediumKind,
                MediumId = MediumId,
                MediumCode = MediumCode,
                ElectronicArchiveUnitId = ElectronicArchiveUnitId,
                ElectronicArchiveNo = ElectronicArchiveNo,
                BeforeMediaStatus = BeforeMediaStatus,
                TargetBlankSlotLocation = TargetBlankSlotLocation
            };
        }
    }
}
