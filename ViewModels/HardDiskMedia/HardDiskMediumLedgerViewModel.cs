using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘初始登记列表 ViewModel。
    /// </summary>
    public class HardDiskMediumLedgerViewModel : ViewModelBase
    {
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private bool _isInitialized;
        private string _searchKeyword = string.Empty;
        private string _selectedStatus = "全部";
        private string _selectedNature = "全部";
        private HardDiskMedium? _selectedMedium;

        public HardDiskMediumLedgerViewModel(IHardDiskMediaService hardDiskMediaService, IDialogService dialogService, IUserContextService userContextService)
        {
            _hardDiskMediaService = hardDiskMediaService;
            _dialogService = dialogService;
            _userContextService = userContextService;

            SearchCommand = new RelayCommand(async _ => await SearchAsync());
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            ImportCommand = new RelayCommand(async _ => await ImportAsync());
            ExportTemplateCommand = new RelayCommand(async _ => await ExportTemplateAsync());
            ShowImportTemplateHelpCommand = new RelayCommand(_ => ShowImportTemplateHelp());
            AddCommand = new RelayCommand(async _ => await AddMediumAsync());
            EditCommand = new RelayCommand(async _ => await EditMediumAsync(), _ => SelectedMedium != null);
            DeleteCommand = new RelayCommand(async _ => await DeleteMediumAsync(), _ => SelectedMedium != null);
        }

        public ObservableCollection<HardDiskMedium> MediaItems { get; } = new();
        public ObservableCollection<string> StatusOptions { get; } = new();
        public ObservableCollection<string> NatureOptions { get; } = new();

        public string SearchKeyword
        {
            get => _searchKeyword;
            set => SetProperty(ref _searchKeyword, value);
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set => SetProperty(ref _selectedStatus, value);
        }

        public string SelectedNature
        {
            get => _selectedNature;
            set => SetProperty(ref _selectedNature, value);
        }

        public HardDiskMedium? SelectedMedium
        {
            get => _selectedMedium;
            set
            {
                if (SetProperty(ref _selectedMedium, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public RelayCommand SearchCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ImportCommand { get; }
        public RelayCommand ExportTemplateCommand { get; }
        public RelayCommand ShowImportTemplateHelpCommand { get; }
        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            await LoadOptionsAsync();
            await SearchAsync();
            _isInitialized = true;
        }

        private async Task LoadOptionsAsync()
        {
            var statusOptions = await _hardDiskMediaService.GetDomainOptionLabelsAsync(nameof(HardDiskLedger), nameof(HardDiskLedger.MediaStatus));
            var natureOptions = await _hardDiskMediaService.GetDomainOptionLabelsAsync(nameof(HardDiskLedger), nameof(HardDiskLedger.MediaNature));

            ResetOptions(StatusOptions, statusOptions);
            ResetOptions(NatureOptions, natureOptions);
        }

        private async Task SearchAsync()
        {
            try
            {
                int? selectedId = SelectedMedium?.Id;
                string? status = SelectedStatus == "全部" ? null : SelectedStatus;
                string? nature = SelectedNature == "全部" ? null : SelectedNature;
                var items = await _hardDiskMediaService.SearchMediaAsync(SearchKeyword, status, nature);

                MediaItems.Clear();
                foreach (var item in items)
                {
                    MediaItems.Add(item);
                }

                SelectedMedium = selectedId.HasValue
                    ? MediaItems.FirstOrDefault(item => item.Id == selectedId.Value)
                    : MediaItems.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载硬盘初始登记列表失败：{ex.Message}");
            }
        }

        private async Task RefreshAsync()
        {
            SearchKeyword = string.Empty;
            SelectedStatus = "全部";
            SelectedNature = "全部";
            await SearchAsync();
        }

        private async Task AddMediumAsync()
        {
            await OpenAndReopenMediumDialogAsync(new HardDiskMedium
            {
                RegistrationMethod = HardDiskMedium.RegistrationMethodManual
            });
        }

        private void ShowImportTemplateHelp()
        {
            string description = _hardDiskMediaService.GetMediaImportTemplateDescription();
            _dialogService.ShowMessage(description, "导入模板说明");
        }

        private async Task ExportTemplateAsync()
        {
            string? filePath = _dialogService.SaveFileDialog("Excel Files|*.xlsx", "导出硬盘初始登记导入模板", "硬盘初始登记导入模板.xlsx");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            _dialogService.SetBusyState(true);
            try
            {
                await _hardDiskMediaService.ExportMediaImportTemplateAsync(filePath);
                _dialogService.ShowMessage($"模板导出完成：\n{filePath}", "完成");
            }
            catch (ArgumentException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _dialogService.ShowError($"没有权限写入目标文件：{ex.Message}");
            }
            catch (IOException ex)
            {
                _dialogService.ShowError($"写入模板文件失败：{ex.Message}");
            }
            finally
            {
                _dialogService.SetBusyState(false);
            }
        }

        private async Task ImportAsync()
        {
            string? filePath = _dialogService.OpenFileDialog("Excel Files|*.xlsx;*.xls", "选择硬盘初始登记导入文件");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                var sheetNames = await _hardDiskMediaService.GetImportSheetNamesAsync(filePath);
                if (sheetNames.Count == 0)
                {
                    _dialogService.ShowMessage("所选文件未找到可导入的工作表。", "提示");
                    return;
                }

                string? selectedSheet = _dialogService.ShowSheetSelectionDialog(sheetNames.ToList(), "选择初始登记工作表");
                if (string.IsNullOrWhiteSpace(selectedSheet))
                {
                    return;
                }

                ImportMode importMode = ImportMode.Append;
                if (await _hardDiskMediaService.HasMediaRecordsAsync())
                {
                    var selectedMode = _dialogService.ShowImportOptionDialog("硬盘初始登记");
                    if (!selectedMode.HasValue)
                    {
                        return;
                    }

                    importMode = selectedMode.Value;
                }

                _dialogService.SetBusyState(true);
                try
                {
                    var result = await _hardDiskMediaService.ImportMediaAsync(filePath, selectedSheet, importMode, _userContextService.CurrentUser);
                    await SearchAsync();

                    string modeSummary = result.Mode == ImportMode.Recreate
                        ? $"覆盖导入完成。\n已清理 {result.ClearedCount} 条旧记录，导入 {result.ImportedCount} 条新记录。"
                        : $"追加导入完成。\n成功导入 {result.ImportedCount} 条记录。";

                    string slotSummary = result.AssignedSlotCount > 0
                        ? $"\n\n已为 {result.AssignedSlotCount} 块无存放位置的空白硬盘，按防磁磁盘柜空白专用档口用途与容量自动入位。"
                        : string.Empty;

                    string ledgerReminder = "\n\n请资料室管理员前往【硬盘台账】核对存放位置，并按入库后台账完成后续业务操作。";
                    _dialogService.ShowMessage(modeSummary + slotSummary + ledgerReminder, "完成");
                }
                finally
                {
                    _dialogService.SetBusyState(false);
                }
            }
            catch (HardDiskMediaImportException ex)
            {
                _dialogService.ShowError(ex.Message, "导入失败");
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (ArgumentException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (IOException ex)
            {
                _dialogService.ShowError($"读取导入文件失败：{ex.Message}");
            }
        }

        private async Task EditMediumAsync()
        {
            if (SelectedMedium == null)
            {
                return;
            }

            var editable = CloneMedium(SelectedMedium);
            await OpenAndReopenMediumDialogAsync(editable);
        }

        private async Task OpenAndReopenMediumDialogAsync(HardDiskMedium medium)
        {
            while (_dialogService.ShowHardDiskMediumEditDialog(medium))
            {
                await SearchAsync();

                if (medium.Id <= 0)
                {
                    continue;
                }

                var latest = MediaItems.FirstOrDefault(item => item.Id == medium.Id);
                if (latest == null)
                {
                    continue;
                }

                SelectedMedium = latest;
                medium = CloneMedium(latest);
            }
        }

        private async Task DeleteMediumAsync()
        {
            if (SelectedMedium == null)
            {
                return;
            }

            if (!_dialogService.ShowConfirm($"确定要删除硬盘介质 [{SelectedMedium.DiskCode}] 吗？", "提示"))
            {
                return;
            }

            try
            {
                await _hardDiskMediaService.DeleteMediumAsync(SelectedMedium.Id);
                await SearchAsync();
                _dialogService.ShowMessage("删除成功。");
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private static HardDiskMedium CloneMedium(HardDiskMedium source)
        {
            return new HardDiskMedium
            {
                Id = source.Id,
                DiskCode = source.DiskCode,
                SerialNumber = source.SerialNumber,
                DiskType = source.DiskType,
                Brand = source.Brand,
                Capacity = source.Capacity,
                InterfaceType = source.InterfaceType,
                RegisterPerson = source.RegisterPerson,
                RegisterDate = source.RegisterDate,
                FactoryDate = source.FactoryDate,
                RegistrationMethod = source.RegistrationMethod,
                Ledger = source.Ledger == null
                    ? null
                    : new HardDiskLedger
                    {
                        MediaStatus = source.Ledger.MediaStatus,
                        MediaNature = source.Ledger.MediaNature,
                        StorageLocation = source.Ledger.StorageLocation,
                        HolderOrOrganization = source.Ledger.HolderOrOrganization,
                        NeedReturn = source.Ledger.NeedReturn,
                        RegisterPerson = source.Ledger.RegisterPerson,
                        RegisterDate = source.Ledger.RegisterDate,
                        Remark = source.Ledger.Remark
                    },
                Remark = source.Remark
            };
        }

        private static void ResetOptions(ObservableCollection<string> target, IReadOnlyList<string> values)
        {
            target.Clear();
            target.Add("全部");
            foreach (var value in values)
            {
                target.Add(value);
            }
        }
    }
}
