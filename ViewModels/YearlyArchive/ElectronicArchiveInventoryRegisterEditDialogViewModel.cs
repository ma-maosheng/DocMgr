using System.Collections.ObjectModel;
using System.Windows.Input;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 电子资料盘库登记办理弹窗 ViewModel（仅服务电子介质轨）。
    /// </summary>
    public sealed class ElectronicArchiveInventoryRegisterEditDialogViewModel : ViewModelBase
    {
        private readonly IArchiveInventoryRegisterService _registerService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly List<ElectronicInventoryCandidateRow> _candidatePool = new();
        private const string SlotFilterAll = "全部";

        private YearlyArchiveInventoryRegisterRecord _record;
        private bool _hasCommittedChanges;
        private string _registerNo = string.Empty;
        private string _registerKind = ArchiveInventoryRegisterDomainValues.KindLost;
        private string _reason = string.Empty;
        private string _remark = string.Empty;
        private string _filterKeyword = string.Empty;
        private string _selectedSlot = SlotFilterAll;
        private ElectronicInventoryItemRow? _selectedItem;

        public ElectronicArchiveInventoryRegisterEditDialogViewModel(
            IArchiveInventoryRegisterService registerService,
            IDialogService dialogService,
            IUserContextService userContextService,
            YearlyArchiveInventoryRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            _registerService = registerService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _record = record;

            MoveToRegisterCommand = new RelayCommand(_ => MoveToRegister(), _ => CanEditHeader && AvailableCandidates.Any(item => item.IsSelected));
            MoveToAvailableCommand = new RelayCommand(_ => MoveToAvailable(), _ => CanEditHeader && HasRemovableItems);
            SaveDraftCommand = new RelayCommand(async _ => await SaveDraftAsync(), _ => CanEditHeader);
            CompleteCommand = new RelayCommand(async _ => await CompleteAsync(), _ => CanComplete);
            WithdrawCommand = new RelayCommand(async _ => await WithdrawAsync(), _ => CanWithdraw);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            _ = InitializeAsync();
        }

        public event Action<bool?>? RequestClose;

        public bool HasCommittedChanges => _hasCommittedChanges;

        public string WindowTitle =>
            $"电子资料盘库登记 · {(string.IsNullOrWhiteSpace(RegisterNo) ? "待编单" : RegisterNo)} · {StatusDisplay}";

        public string StatusDisplay => ArchiveInventoryRegisterDomainValues.ToStatusDisplay(_record.Status);

        public string BannerText =>
            "按电子袋内硬盘/光盘登记损坏、盘失或拟销。拟销用于无存档价值资料，办结效应与盘失相同。办结改介质台账但保留档口与介质袋；关联资料不可再借出。正式离库请后期走「离库处置」。";

        public ObservableCollection<string> RegisterKindOptions { get; } = new();

        public ObservableCollection<string> SlotOptions { get; } = new();

        public ObservableCollection<ElectronicInventoryCandidateRow> AvailableCandidates { get; } = new();

        public ObservableCollection<ElectronicInventoryItemRow> Items { get; } = new();

        public string AvailableTitle => $"可选袋内介质（{AvailableCandidates.Count}）";

        public string RegisterTitle => $"待登记介质（{Items.Count}）";

        public bool CanEditRegisterKind => CanEditHeader;

        public string FilterKeyword
        {
            get => _filterKeyword;
            set
            {
                if (SetProperty(ref _filterKeyword, value))
                {
                    RefreshAvailableCandidates();
                }
            }
        }

        public string SelectedSlot
        {
            get => _selectedSlot;
            set
            {
                if (SetProperty(ref _selectedSlot, value ?? SlotFilterAll))
                {
                    RefreshAvailableCandidates();
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
                    OnPropertyChanged(nameof(WindowTitle));
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

        public ElectronicInventoryItemRow? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (!SetProperty(ref _selectedItem, value))
                {
                    return;
                }

                // 行选中时同步勾选，保证「← 移除」可用。
                if (value != null && !value.IsSelected)
                {
                    value.IsSelected = true;
                }

                RaiseCommandStates();
            }
        }

        public bool CanEditHeader =>
            ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser)
            && _record.Status == YearlyArchiveInventoryRegisterRecord.StatusDraft;

        public bool CanComplete => CanEditHeader && Items.Count > 0;

        public bool CanWithdraw => CanEditHeader && _record.Id > 0;

        private bool HasRemovableItems =>
            SelectedItem != null || Items.Any(item => item.IsSelected);

        public RelayCommand MoveToRegisterCommand { get; }
        public RelayCommand MoveToAvailableCommand { get; }
        public RelayCommand SaveDraftCommand { get; }
        public RelayCommand CompleteCommand { get; }
        public RelayCommand WithdrawCommand { get; }
        public RelayCommand CloseCommand { get; }

        private async Task InitializeAsync()
        {
            try
            {
                RegisterKindOptions.Clear();
                foreach (var kind in ArchiveInventoryRegisterDomainValues.RegisterKindOptions)
                {
                    RegisterKindOptions.Add(kind);
                }

                RegisterNo = _record.RegisterNo;
                RegisterKind = string.IsNullOrWhiteSpace(_record.RegisterKind)
                    ? ArchiveInventoryRegisterDomainValues.KindLost
                    : _record.RegisterKind.Trim();
                Reason = _record.Reason;
                Remark = _record.Remark;

                if (_record.Id <= 0 && string.IsNullOrWhiteSpace(RegisterNo))
                {
                    RegisterNo = await _registerService.GenerateNextRegisterNoAsync();
                }

                await ReloadCandidatePoolAsync();
                Items.Clear();
                foreach (var item in _record.Items.OrderBy(detail => detail.SortOrder))
                {
                    Items.Add(ElectronicInventoryItemRow.FromItem(item));
                }

                RefreshAvailableCandidates();
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(StatusDisplay));
                OnPropertyChanged(nameof(CanEditRegisterKind));
                RaiseCommandStates();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private async Task ReloadCandidatePoolAsync()
        {
            int? currentId = _record.Id > 0 ? _record.Id : null;
            _candidatePool.Clear();

            var media = await _registerService.GetSelectableElectronicMediaAsync(currentId);
            foreach (var row in media)
            {
                _candidatePool.Add(ElectronicInventoryCandidateRow.FromMedia(row));
            }

            RebuildSlotOptions();
        }

        /// <summary>从候选池收集非空档口键，供筛选下拉使用。</summary>
        private void RebuildSlotOptions()
        {
            string previous = SelectedSlot;
            SlotOptions.Clear();
            SlotOptions.Add(SlotFilterAll);

            foreach (string slot in _candidatePool
                .Select(ResolveSlotKey)
                .Where(slot => !string.IsNullOrWhiteSpace(slot))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(slot => slot, StringComparer.OrdinalIgnoreCase))
            {
                SlotOptions.Add(slot);
            }

            _selectedSlot = SlotOptions.Contains(previous, StringComparer.OrdinalIgnoreCase)
                ? previous
                : SlotFilterAll;
            OnPropertyChanged(nameof(SelectedSlot));
        }

        private static string ResolveSlotKey(ElectronicInventoryCandidateRow candidate)
        {
            string location = candidate.StorageLocation?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(location))
            {
                return string.Empty;
            }

            string slotKey = ArchiveSlotLocationSupport.BuildSlotKey(location);
            return string.IsNullOrWhiteSpace(slotKey) ? location : slotKey;
        }

        private void RefreshAvailableCandidates()
        {
            string keyword = FilterKeyword?.Trim() ?? string.Empty;
            string selectedSlot = SelectedSlot?.Trim() ?? SlotFilterAll;
            bool filterBySlot = !string.Equals(selectedSlot, SlotFilterAll, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(selectedSlot);
            HashSet<string> selectedKeys = Items.Select(item => item.SelectionKey).ToHashSet(StringComparer.Ordinal);

            AvailableCandidates.Clear();
            foreach (var candidate in _candidatePool.Where(item => !selectedKeys.Contains(item.SelectionKey)))
            {
                if (filterBySlot && !MatchesSelectedSlot(candidate.StorageLocation, selectedSlot))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(keyword) && !candidate.MatchesKeyword(keyword))
                {
                    continue;
                }

                AvailableCandidates.Add(candidate);
            }

            OnPropertyChanged(nameof(AvailableTitle));
            OnPropertyChanged(nameof(RegisterTitle));
            RaiseCommandStates();
        }

        private static bool MatchesSelectedSlot(string? storageLocation, string selectedSlot)
        {
            if (ArchiveSlotLocationSupport.IsSameSlot(storageLocation, selectedSlot))
            {
                return true;
            }

            return string.Equals(
                storageLocation?.Trim(),
                selectedSlot.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private void MoveToRegister()
        {
            foreach (var candidate in AvailableCandidates.Where(item => item.IsSelected).ToList())
            {
                if (Items.Any(item => string.Equals(item.SelectionKey, candidate.SelectionKey, StringComparison.Ordinal)))
                {
                    continue;
                }

                Items.Add(ElectronicInventoryItemRow.FromCandidate(candidate));
            }

            RefreshAvailableCandidates();
        }

        private void MoveToAvailable()
        {
            var toRemove = Items.Where(row => row.IsSelected).ToList();
            if (toRemove.Count == 0 && SelectedItem != null)
            {
                toRemove.Add(SelectedItem);
            }

            foreach (var item in toRemove)
            {
                Items.Remove(item);
            }

            SelectedItem = null;
            RefreshAvailableCandidates();
        }

        private async Task SaveDraftAsync()
        {
            if (!CanEditHeader)
            {
                return;
            }

            try
            {
                EnsureReasonFilled();
                var drafts = BuildDrafts();
                var header = BuildHeader();
                if (_record.Id <= 0)
                {
                    _record = await _registerService.CreateDraftAsync(header, drafts, _userContextService.CurrentUser!);
                }
                else
                {
                    header.Id = _record.Id;
                    _record = await _registerService.UpdateDraftAsync(header, drafts, _userContextService.CurrentUser!);
                }

                _hasCommittedChanges = true;
                RegisterNo = _record.RegisterNo;
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(StatusDisplay));
                OnPropertyChanged(nameof(CanWithdraw));
                _dialogService.ShowMessage("草稿已保存。", "盘库登记");
                RaiseCommandStates();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(FormatExceptionMessage(ex));
            }
        }

        private async Task CompleteAsync()
        {
            if (!CanComplete)
            {
                return;
            }

            try
            {
                EnsureReasonFilled();
                _ = BuildDrafts();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(FormatExceptionMessage(ex));
                return;
            }

            if (!_dialogService.ShowConfirm("确认登记办结？办结后即时写入台账，不可再改。", "确认登记办结"))
            {
                return;
            }

            try
            {
                var drafts = BuildDrafts();
                var header = BuildHeader();
                if (_record.Id <= 0)
                {
                    _record = await _registerService.CreateDraftAsync(header, drafts, _userContextService.CurrentUser!);
                }
                else
                {
                    header.Id = _record.Id;
                    _record = await _registerService.UpdateDraftAsync(header, drafts, _userContextService.CurrentUser!);
                }

                await _registerService.CompleteAsync(_record.Id, _userContextService.CurrentUser!);
                _hasCommittedChanges = true;
                _dialogService.ShowMessage("盘库登记已确认办结。", "确认登记办结");
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(FormatExceptionMessage(ex));
            }
        }

        /// <summary>
        /// 登记说明未填时，默认用当前登记类型，避免办结被硬拦。
        /// </summary>
        private void EnsureReasonFilled()
        {
            if (!string.IsNullOrWhiteSpace(Reason))
            {
                return;
            }

            string kind = RegisterKind?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(kind))
            {
                throw new InvalidOperationException("请选择登记类型，或填写登记说明。");
            }

            Reason = kind;
        }

        private async Task WithdrawAsync()
        {
            if (!CanWithdraw)
            {
                return;
            }

            if (!_dialogService.ShowConfirm("确认撤回作废当前草稿？", "撤回作废"))
            {
                return;
            }

            try
            {
                await _registerService.WithdrawAsync(_record.Id, "办理弹窗撤回作废", _userContextService.CurrentUser!);
                _hasCommittedChanges = true;
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private YearlyArchiveInventoryRegisterRecord BuildHeader() =>
            new()
            {
                Id = _record.Id,
                RegisterNo = RegisterNo?.Trim() ?? string.Empty,
                MediaKind = ArchiveInventoryRegisterDomainValues.MediaKindElectronic,
                RegisterKind = RegisterKind?.Trim() ?? string.Empty,
                Reason = Reason?.Trim() ?? string.Empty,
                Remark = Remark?.Trim() ?? string.Empty,
                Status = YearlyArchiveInventoryRegisterRecord.StatusDraft
            };

        private List<ArchiveInventoryRegisterItemDraft> BuildDrafts()
        {
            if (Items.Count == 0)
            {
                throw new InvalidOperationException("请至少选择一块介质。");
            }

            var drafts = new List<ArchiveInventoryRegisterItemDraft>();
            foreach (var item in Items)
            {
                drafts.Add(new ArchiveInventoryRegisterItemDraft
                {
                    MediumKind = item.MediumKind,
                    MediumId = item.MediumId
                });
            }

            return drafts;
        }

        private void RaiseCommandStates()
        {
            CommandManager.InvalidateRequerySuggested();
            OnPropertyChanged(nameof(CanEditHeader));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanWithdraw));
            OnPropertyChanged(nameof(CanEditRegisterKind));
        }

        private static string FormatExceptionMessage(Exception ex)
        {
            Exception current = ex;
            while (current.InnerException != null)
            {
                current = current.InnerException;
            }

            string root = current.Message?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(root) || string.Equals(root, ex.Message, StringComparison.Ordinal))
            {
                return ex.Message;
            }

            return $"{ex.Message}\n\n详情：{root}";
        }
    }

    /// <summary>电子盘库可选袋内介质行。</summary>
    public sealed class ElectronicInventoryCandidateRow : ViewModelBase
    {
        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ProjectName { get; init; } = string.Empty;

        public string Year { get; init; } = string.Empty;

        public string MediumKind { get; init; } = string.Empty;

        public int MediumId { get; init; }

        public string MediumCode { get; init; } = string.Empty;

        /// <summary>光盘无编号，显示为「-」。</summary>
        public string MediumCodeDisplay =>
            ArchiveInventoryRegisterDomainValues.ResolveMediumCodeDisplay(MediumKind, MediumCode);

        public int ElectronicArchiveUnitId { get; init; }

        public string ElectronicArchiveNo { get; init; } = string.Empty;

        public string MediaStatus { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public string SelectionKey => $"M:{MediumKind}:{MediumId}";

        public string DisplayName => $"{MediumKind} {MediumCodeDisplay}";

        public bool MatchesKeyword(string keyword)
        {
            return ProjectName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || Year.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || MediumCodeDisplay.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || MediumCode.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || ElectronicArchiveNo.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || StorageLocation.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        public static ElectronicInventoryCandidateRow FromMedia(ArchiveInventorySelectableElectronicMedia row)
        {
            return new ElectronicInventoryCandidateRow
            {
                ProjectName = row.ProjectName?.Trim() ?? string.Empty,
                Year = row.Year?.Trim() ?? string.Empty,
                MediumKind = row.MediumKind?.Trim() ?? string.Empty,
                MediumId = row.MediumId,
                MediumCode = row.MediumCode?.Trim() ?? string.Empty,
                ElectronicArchiveUnitId = row.ElectronicArchiveUnitId,
                ElectronicArchiveNo = row.ElectronicArchiveNo?.Trim() ?? string.Empty,
                MediaStatus = row.BeforeMediaStatus?.Trim() ?? string.Empty,
                StorageLocation = row.BeforeStorageLocation?.Trim() ?? string.Empty
            };
        }
    }

    /// <summary>电子盘库待登记介质行。</summary>
    public sealed class ElectronicInventoryItemRow : ViewModelBase
    {
        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ProjectName { get; init; } = string.Empty;

        public string Year { get; init; } = string.Empty;

        public string MediumKind { get; init; } = string.Empty;

        public int MediumId { get; init; }

        public string MediumCode { get; init; } = string.Empty;

        /// <summary>光盘无编号，显示为「-」。</summary>
        public string MediumCodeDisplay =>
            ArchiveInventoryRegisterDomainValues.ResolveMediumCodeDisplay(MediumKind, MediumCode);

        public string ElectronicArchiveNo { get; init; } = string.Empty;

        public string BeforeMediaStatus { get; init; } = string.Empty;

        public string BeforeStorageLocation { get; init; } = string.Empty;

        public string SelectionKey => $"M:{MediumKind}:{MediumId}";

        public string DisplayName => $"{MediumKind} {MediumCodeDisplay}";

        public static ElectronicInventoryItemRow FromCandidate(ElectronicInventoryCandidateRow candidate)
        {
            return new ElectronicInventoryItemRow
            {
                ProjectName = candidate.ProjectName,
                Year = candidate.Year,
                MediumKind = candidate.MediumKind,
                MediumId = candidate.MediumId,
                MediumCode = candidate.MediumCode,
                ElectronicArchiveNo = candidate.ElectronicArchiveNo,
                BeforeMediaStatus = candidate.MediaStatus,
                BeforeStorageLocation = candidate.StorageLocation
            };
        }

        public static ElectronicInventoryItemRow FromItem(YearlyArchiveInventoryRegisterItem item)
        {
            return new ElectronicInventoryItemRow
            {
                ProjectName = item.ProjectName?.Trim() ?? string.Empty,
                Year = item.Year?.Trim() ?? string.Empty,
                MediumKind = item.MediumKind?.Trim() ?? string.Empty,
                MediumId = item.MediumId,
                MediumCode = item.MediumCode?.Trim() ?? string.Empty,
                ElectronicArchiveNo = item.ElectronicArchiveNo?.Trim() ?? string.Empty,
                BeforeMediaStatus = item.BeforeMediaStatus?.Trim() ?? string.Empty,
                BeforeStorageLocation = item.BeforeStorageLocation?.Trim() ?? string.Empty
            };
        }
    }
}
