using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 资料立档拟使用硬盘选择弹窗 ViewModel。
    /// </summary>
    public class HardDiskMediumSelectionDialogViewModel : ViewModelBase
    {
        private const string SelectionModeBlankTarget = "BlankTarget";

        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IArchiveFilingService _archiveFilingService;
        private readonly IDialogService _dialogService;
        private readonly string? _initialSelectedCode;
        private readonly int? _currentElectronicArchiveUnitId;
        private readonly string _selectionMode;
        private bool _isInitialized;
        private string _keyword = string.Empty;
        private ArchiveFilingBlankHardDiskItemViewModel? _selectedDisk;

        public HardDiskMediumSelectionDialogViewModel(
            IHardDiskMediaService hardDiskMediaService,
            IArchiveFilingService archiveFilingService,
            IDialogService dialogService,
            IEnumerable<string>? initialSelectedCodes = null,
            int? currentElectronicArchiveUnitId = null,
            string? selectionMode = null)
        {
            _hardDiskMediaService = hardDiskMediaService;
            _archiveFilingService = archiveFilingService;
            _dialogService = dialogService;
            _currentElectronicArchiveUnitId = currentElectronicArchiveUnitId;
            _selectionMode = selectionMode?.Trim() ?? string.Empty;
            _initialSelectedCode = (initialSelectedCodes ?? Enumerable.Empty<string>())
                .Select(code => code?.Trim())
                .FirstOrDefault(code => !string.IsNullOrWhiteSpace(code));

            AvailableDisks = new ObservableCollection<ArchiveFilingBlankHardDiskItemViewModel>();
            ConfirmCommand = new RelayCommand(_ => Confirm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
            SearchCommand = new RelayCommand(async _ => await SearchAsync());
        }

        public ObservableCollection<ArchiveFilingBlankHardDiskItemViewModel> AvailableDisks { get; }

        public string Keyword
        {
            get => _keyword;
            set => SetProperty(ref _keyword, value);
        }

        public ArchiveFilingBlankHardDiskItemViewModel? SelectedDisk
        {
            get => _selectedDisk;
            set => SetProperty(ref _selectedDisk, value);
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand SearchCommand { get; }

        public IReadOnlyList<HardDiskMedium> SelectedMedia => SelectedDisk == null
            ? Array.Empty<HardDiskMedium>()
            : new[] { SelectedDisk.Source };

        public string HintText => _selectionMode switch
        {
            SelectionModeBlankTarget => "仅列出硬盘柜中在库、可正常使用的空白硬盘；已被登记占用或已关联其他电子袋的硬盘不会显示。",
            _ => "请选择一块拟用于资料立档的硬盘。"
        };

        public event Action<bool?>? RequestClose;

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            await SearchAsync();
        }

        private async Task SearchAsync()
        {
            if (!string.Equals(_selectionMode, SelectionModeBlankTarget, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(_selectionMode))
            {
                _dialogService.ShowMessage("当前弹窗仅支持资料立档库内空盘选择。", "提示");
                AvailableDisks.Clear();
                SelectedDisk = null;
                return;
            }

            var media = await _hardDiskMediaService.GetArchiveFilingCandidateBlankHardDisksAsync(Keyword?.Trim());
            var linkInfos = await _archiveFilingService.GetElectronicArchiveLinkInfosAsync(media.Select(item => item.Id));

            var candidates = media
                .Where(item => !HasBlockingElectronicArchiveLink(item.Id, linkInfos))
                .Select(item => new ArchiveFilingBlankHardDiskItemViewModel(item))
                .ToList();

            AvailableDisks.Clear();
            foreach (var item in candidates)
            {
                AvailableDisks.Add(item);
            }

            SelectedDisk = AvailableDisks.FirstOrDefault(item =>
                               string.Equals(item.DiskCode, _initialSelectedCode, StringComparison.Ordinal))
                           ?? AvailableDisks.FirstOrDefault();
        }

        private bool HasBlockingElectronicArchiveLink(int mediumId, IReadOnlyList<HardDiskElectronicArchiveLinkInfo> linkInfos)
        {
            var relatedLinks = linkInfos
                .Where(link => link.HardDiskMediumId == mediumId)
                .ToList();

            if (relatedLinks.Count == 0)
            {
                return false;
            }

            if (_currentElectronicArchiveUnitId == null)
            {
                return true;
            }

            return relatedLinks.Any(link => link.ElectronicArchiveUnitId != _currentElectronicArchiveUnitId);
        }

        private void Confirm()
        {
            if (SelectedDisk == null)
            {
                _dialogService.ShowMessage("请选择一块拟用于资料立档的在库空硬盘。");
                return;
            }

            RequestClose?.Invoke(true);
        }
    }

    /// <summary>
    /// 资料立档候选空盘列表项。
    /// </summary>
    public class ArchiveFilingBlankHardDiskItemViewModel
    {
        public ArchiveFilingBlankHardDiskItemViewModel(HardDiskMedium source)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public HardDiskMedium Source { get; }

        public string DiskCode => Source.DiskCode;
        public string SerialNumber => Source.SerialNumber;
        public string Brand => Source.Brand;
        public string Capacity => Source.Capacity;
        public DateTime? FactoryDate => Source.FactoryDate;
        public string InterfaceType => Source.InterfaceType;
        public string CurrentLocation => Source.Ledger?.StorageLocation ?? string.Empty;
    }
}
