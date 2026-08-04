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
    /// 模拟资料盘库登记办理弹窗 ViewModel（仅服务模拟介质轨）。
    /// </summary>
    public sealed class SimulatedArchiveInventoryRegisterEditDialogViewModel : ViewModelBase
    {
        private readonly IArchiveInventoryRegisterService _registerService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly List<SimulatedInventoryCandidateRow> _candidatePool = new();
        private const string SlotFilterAll = "全部";

        private YearlyArchiveInventoryRegisterRecord _record;
        private bool _hasCommittedChanges;
        private string _registerNo = string.Empty;
        private string _registerKind = ArchiveInventoryRegisterDomainValues.KindLost;
        private string _reason = string.Empty;
        private string _remark = string.Empty;
        private string _filterKeyword = string.Empty;
        private string _selectedSlot = SlotFilterAll;
        private SimulatedInventoryItemRow? _selectedItem;

        public SimulatedArchiveInventoryRegisterEditDialogViewModel(
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
            MoveToAvailableCommand = new RelayCommand(_ => MoveToAvailable(), _ => CanEditHeader && Items.Any(item => item.IsSelected));
            SaveDraftCommand = new RelayCommand(async _ => await SaveDraftAsync(), _ => CanEditHeader);
            CompleteCommand = new RelayCommand(async _ => await CompleteAsync(), _ => CanComplete);
            WithdrawCommand = new RelayCommand(async _ => await WithdrawAsync(), _ => CanWithdraw);
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke(false));

            _ = InitializeAsync();
        }

        public event Action<bool?>? RequestClose;

        public bool HasCommittedChanges => _hasCommittedChanges;

        public string WindowTitle =>
            $"模拟资料盘库登记 · {(string.IsNullOrWhiteSpace(RegisterNo) ? "待编单" : RegisterNo)} · {StatusDisplay}";

        public string StatusDisplay => ArchiveInventoryRegisterDomainValues.ToStatusDisplay(_record.Status);

        public string BannerText =>
            "按资料子项登记盘失或拟销份数（不可超过当前库内可用份数，不含待还）。拟销用于无存档价值资料。办结即时写台账；盘库空盒仍占档口。正式离库请后期走「离库处置」。";

        public ObservableCollection<string> RegisterKindOptions { get; } = new();

        public ObservableCollection<string> SlotOptions { get; } = new();

        public ObservableCollection<SimulatedInventoryCandidateRow> AvailableCandidates { get; } = new();

        public ObservableCollection<SimulatedInventoryItemRow> Items { get; } = new();

        public string AvailableTitle => $"可选在库子项（{AvailableCandidates.Count}）";

        public string RegisterTitle => $"待登记子项（{Items.Count}）";

        public bool CanEditRegisterKind => CanEditHeader;

        /// <summary>拟销登记时「丢失份数」显示为「-」（此项为空，按可用份数全额拟销）。</summary>
        public bool IsScrapRegisterKind =>
            string.Equals(RegisterKind?.Trim(), ArchiveInventoryRegisterDomainValues.KindScrap, StringComparison.Ordinal);

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
                    OnPropertyChanged(nameof(IsScrapRegisterKind));
                    SyncLostCopyDisplayMode();
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

        public SimulatedInventoryItemRow? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public bool CanEditHeader =>
            ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser)
            && _record.Status == YearlyArchiveInventoryRegisterRecord.StatusDraft;

        public bool CanComplete => CanEditHeader && Items.Count > 0;

        public bool CanWithdraw => CanEditHeader && _record.Id > 0;

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
                foreach (var kind in ArchiveInventoryRegisterDomainValues.SimulatedRegisterKindOptions)
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
                    Items.Add(SimulatedInventoryItemRow.FromItem(item, IsScrapRegisterKind));
                }

                if (IsScrapRegisterKind)
                {
                    SyncLostCopyDisplayMode();
                }

                RefreshAvailableCandidates();
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(StatusDisplay));
                OnPropertyChanged(nameof(CanEditRegisterKind));
                OnPropertyChanged(nameof(IsScrapRegisterKind));
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

            var facts = await _registerService.GetSelectableSimulatedFilingFactsAsync(currentId);
            foreach (var fact in facts)
            {
                _candidatePool.Add(SimulatedInventoryCandidateRow.FromFact(fact));
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

        private static string ResolveSlotKey(SimulatedInventoryCandidateRow candidate)
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

                Items.Add(SimulatedInventoryItemRow.FromCandidate(candidate, IsScrapRegisterKind));
            }

            RefreshAvailableCandidates();
        }

        private void MoveToAvailable()
        {
            foreach (var item in Items.Where(row => row.IsSelected).ToList())
            {
                Items.Remove(item);
            }

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
                _dialogService.ShowError(ex.Message);
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
                _dialogService.ShowError(ex.Message);
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
                _dialogService.ShowError(ex.Message);
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
                MediaKind = ArchiveInventoryRegisterDomainValues.MediaKindSimulated,
                RegisterKind = RegisterKind?.Trim() ?? string.Empty,
                Reason = Reason?.Trim() ?? string.Empty,
                Remark = Remark?.Trim() ?? string.Empty,
                Status = YearlyArchiveInventoryRegisterRecord.StatusDraft
            };

        private List<ArchiveInventoryRegisterItemDraft> BuildDrafts()
        {
            if (Items.Count == 0)
            {
                throw new InvalidOperationException("请至少选择一个资料子项。");
            }

            bool isScrap = IsScrapRegisterKind;
            var drafts = new List<ArchiveInventoryRegisterItemDraft>();
            foreach (var item in Items)
            {
                // 拟销按可用份数全额登记；盘失使用用户录入的丢失份数。
                int registerCopyCount = isScrap ? item.AvailableCopyCount : item.LostCopyCount;

                if (registerCopyCount <= 0)
                {
                    throw new InvalidOperationException(
                        isScrap
                            ? $"【{item.DisplayName}】可用份数须大于 0，无法拟销登记。"
                            : $"【{item.DisplayName}】丢失份数须大于 0。");
                }

                if (registerCopyCount > item.AvailableCopyCount)
                {
                    throw new InvalidOperationException(
                        $"【{item.DisplayName}】丢失份数 {registerCopyCount} 不能大于可用份数 {item.AvailableCopyCount}。");
                }

                drafts.Add(new ArchiveInventoryRegisterItemDraft
                {
                    FilingFactId = item.FilingFactId,
                    LostCopyCount = registerCopyCount
                });
            }

            return drafts;
        }

        private void SyncLostCopyDisplayMode()
        {
            bool showAsEmpty = IsScrapRegisterKind;
            foreach (var item in Items)
            {
                item.ShowLostCopyAsEmpty = showAsEmpty;
            }
        }

        private void RaiseCommandStates()
        {
            CommandManager.InvalidateRequerySuggested();
            OnPropertyChanged(nameof(CanEditHeader));
            OnPropertyChanged(nameof(CanEditRegisterKind));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CanWithdraw));
        }
    }

    /// <summary>模拟盘库可选在库子项行。</summary>
    public sealed class SimulatedInventoryCandidateRow : ViewModelBase
    {
        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public int FilingFactId { get; init; }

        public string ProjectName { get; init; } = string.Empty;

        public string Year { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string ItemName { get; init; } = string.Empty;

        public string ContainerCode { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public int AvailableCopyCount { get; init; }

        public string SelectionKey => $"F:{FilingFactId}";

        public string DisplayName =>
            string.IsNullOrWhiteSpace(ItemName) ? MaterialName : $"{MaterialName}/{ItemName}";

        public bool MatchesKeyword(string keyword)
        {
            return ProjectName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || Year.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || MaterialName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || ItemName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || ContainerCode.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || StorageLocation.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        public static SimulatedInventoryCandidateRow FromFact(ArchiveInventorySelectableSimulatedFact fact)
        {
            return new SimulatedInventoryCandidateRow
            {
                FilingFactId = fact.FilingFactId,
                ProjectName = fact.ProjectName?.Trim() ?? string.Empty,
                Year = fact.Year?.Trim() ?? string.Empty,
                MaterialName = fact.MaterialName?.Trim() ?? string.Empty,
                ItemName = fact.ItemName?.Trim() ?? string.Empty,
                ContainerCode = fact.ContainerCode?.Trim() ?? string.Empty,
                StorageLocation = fact.StorageLocation?.Trim() ?? string.Empty,
                AvailableCopyCount = Math.Max(0, fact.AvailableCopyCount)
            };
        }
    }

    /// <summary>模拟盘库待登记子项行。</summary>
    public sealed class SimulatedInventoryItemRow : ViewModelBase
    {
        private bool _isSelected;
        private int _lostCopyCount = 1;
        private bool _showLostCopyAsEmpty;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public int FilingFactId { get; init; }

        public string ProjectName { get; init; } = string.Empty;

        public string Year { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string ItemName { get; init; } = string.Empty;

        public string ContainerCode { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public int AvailableCopyCount { get; init; }

        /// <summary>拟销登记时为 true，「丢失份数」显示为「-」表示此项为空。</summary>
        public bool ShowLostCopyAsEmpty
        {
            get => _showLostCopyAsEmpty;
            set
            {
                if (SetProperty(ref _showLostCopyAsEmpty, value))
                {
                    OnPropertyChanged(nameof(LostCopyCountDisplay));
                    OnPropertyChanged(nameof(IsLostCopyCountEditable));
                }
            }
        }

        public bool IsLostCopyCountEditable => !ShowLostCopyAsEmpty;

        public int LostCopyCount
        {
            get => _lostCopyCount;
            set
            {
                if (SetProperty(ref _lostCopyCount, Math.Max(0, value)))
                {
                    OnPropertyChanged(nameof(LostCopyCountDisplay));
                }
            }
        }

        /// <summary>拟销显示「-」；盘失显示可编辑份数。</summary>
        public string LostCopyCountDisplay
        {
            get => ShowLostCopyAsEmpty ? "-" : LostCopyCount.ToString();
            set
            {
                if (ShowLostCopyAsEmpty)
                {
                    return;
                }

                string trimmed = value?.Trim() ?? string.Empty;
                if (int.TryParse(trimmed, out int parsed))
                {
                    LostCopyCount = parsed;
                }
            }
        }

        public string SelectionKey => $"F:{FilingFactId}";

        public string DisplayName =>
            string.IsNullOrWhiteSpace(ItemName) ? MaterialName : $"{MaterialName}/{ItemName}";

        public static SimulatedInventoryItemRow FromCandidate(
            SimulatedInventoryCandidateRow candidate,
            bool showLostCopyAsEmpty = false)
        {
            return new SimulatedInventoryItemRow
            {
                FilingFactId = candidate.FilingFactId,
                ProjectName = candidate.ProjectName,
                Year = candidate.Year,
                MaterialName = candidate.MaterialName,
                ItemName = candidate.ItemName,
                ContainerCode = candidate.ContainerCode,
                StorageLocation = candidate.StorageLocation,
                AvailableCopyCount = candidate.AvailableCopyCount,
                LostCopyCount = 1,
                ShowLostCopyAsEmpty = showLostCopyAsEmpty
            };
        }

        public static SimulatedInventoryItemRow FromItem(
            YearlyArchiveInventoryRegisterItem item,
            bool showLostCopyAsEmpty = false)
        {
            return new SimulatedInventoryItemRow
            {
                FilingFactId = item.FilingFactId,
                ProjectName = item.ProjectName?.Trim() ?? string.Empty,
                Year = item.Year?.Trim() ?? string.Empty,
                MaterialName = item.MaterialName?.Trim() ?? string.Empty,
                ItemName = item.ItemName?.Trim() ?? string.Empty,
                ContainerCode = item.ContainerCode?.Trim() ?? string.Empty,
                StorageLocation = item.BeforeStorageLocation?.Trim() ?? string.Empty,
                AvailableCopyCount = item.BeforeAvailableCopyCount,
                LostCopyCount = Math.Max(1, item.LostCopyCount),
                ShowLostCopyAsEmpty = showLostCopyAsEmpty
            };
        }
    }
}
