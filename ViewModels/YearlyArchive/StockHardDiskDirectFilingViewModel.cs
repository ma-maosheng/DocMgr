using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 存量硬盘直办立档向导。
    /// </summary>
    public sealed class StockHardDiskDirectFilingViewModel : ViewModelBase
    {
        private readonly IStockHardDiskDirectFilingService _filingService;
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly ICabinetService _cabinetService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private ArchiveRegisterPageDomainOptions? _pageDomainOptions;

        private string _diskCode = string.Empty;
        private string _serialNumber = string.Empty;
        private string _diskType = string.Empty;
        private string _brand = string.Empty;
        private string _capacityValue = string.Empty;
        private string _capacityUnit = ElectronicMediaCapacitySupport.DefaultCapacityUnit;
        private string _interfaceType = string.Empty;
        private DateTime? _factoryDate;
        private string _registrationMethod = HardDiskMedium.RegistrationMethodArchive;
        private string _rootPath = string.Empty;
        private string _year = string.Empty;
        private string _projectName = string.Empty;
        private string _projectCode = string.Empty;
        private string _projectHint = "请先扫描目录。";
        private string _yearProjectHint = "填写年度或扫描目录后，可查看该年已有项目。";
        private string _existingBagHint = string.Empty;
        private string _businessNumberHint = "确认立档后：每份资料生成一条建档单号，整盘生成一个电子袋号。";
        private string _previewElectronicArchiveNo = string.Empty;
        private int _archiveNoPreviewToken;
        private readonly string _sourceType = ArchiveRegisterDomainValues.SourceTypeStockDirect;
        private string _archivePurpose = ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage;
        private string _confidentialLevel = "秘密";
        private readonly string _provideUnit = ArchiveRegisterDomainValues.ProvideUnitArchiveRoom;
        private string _materialCategory = ArchiveRegisterDomainValues.ElectronicMaterialCategoryData;
        private string _subCategory = ArchiveRegisterDomainValues.DefaultStockDirectSubCategory;
        private HardDiskMediaReturnTargetLocationOption? _selectedSlotOption;
        private string _storageLocation = string.Empty;
        private string _scanWarningText = string.Empty;
        private bool _isBusy;
        private bool _suppressSlotResolve;

        public StockHardDiskDirectFilingViewModel(
            IStockHardDiskDirectFilingService filingService,
            IHardDiskMediaService hardDiskMediaService,
            IArchiveRegisterService archiveRegisterService,
            ICabinetService cabinetService,
            IDialogService dialogService,
            IUserContextService userContextService)
        {
            _filingService = filingService;
            _hardDiskMediaService = hardDiskMediaService;
            _archiveRegisterService = archiveRegisterService;
            _cabinetService = cabinetService;
            _dialogService = dialogService;
            _userContextService = userContextService;

            PickDiskCommand = new RelayCommand(async _ => await PickDiskAsync(), _ => !IsBusy);
            GenerateDiskCodeCommand = new RelayCommand(async _ => await GenerateDiskCodeAsync(), _ => !IsBusy);
            PickRootCommand = new RelayCommand(_ => PickRoot(), _ => !IsBusy);
            ScanCommand = new RelayCommand(async _ => await ScanAsync(), _ => !IsBusy && !string.IsNullOrWhiteSpace(RootPath));
            ViewYearProjectsCommand = new RelayCommand(_ => ViewYearProjects(), _ => !IsBusy);
            RecommendSlotCommand = new RelayCommand(async _ => await RecommendSlotAsync(), _ => !IsBusy);
            ShowSlotSnapshotCommand = new RelayCommand(async _ => await ShowSlotSnapshotAsync(), _ => !IsBusy && CanShowSlotSnapshot);
            ConfirmCommand = new RelayCommand(async _ => await ConfirmAsync(), _ => !IsBusy);
        }

        public ObservableCollection<string> DiskTypeOptions { get; } = new();
        public ObservableCollection<string> BrandOptions { get; } = new();
        public ObservableCollection<string> InterfaceTypeOptions { get; } = new();
        public ObservableCollection<string> CapacityUnitOptions { get; } = new(ElectronicMediaCapacitySupport.CapacityUnits);
        public ObservableCollection<string> YearOptions { get; } = new();
        public ObservableCollection<string> ProjectNameOptions { get; } = new();
        public ObservableCollection<string> ArchivePurposeOptions { get; } = new();
        public ObservableCollection<string> ConfidentialLevelOptions { get; } = new();
        public ObservableCollection<string> MaterialCategoryOptions { get; } = new();
        public ObservableCollection<string> SubCategoryOptions { get; } = new();
        public ObservableCollection<HardDiskMediaReturnTargetLocationOption> SlotOptions { get; } = new();
        public ObservableCollection<StockHardDiskPreviewRow> PreviewRows { get; } = new();

        public ICommand PickDiskCommand { get; }
        public ICommand GenerateDiskCodeCommand { get; }
        public ICommand PickRootCommand { get; }
        public ICommand ScanCommand { get; }
        public ICommand ViewYearProjectsCommand { get; }
        public ICommand RecommendSlotCommand { get; }
        public ICommand ShowSlotSnapshotCommand { get; }
        public ICommand ConfirmCommand { get; }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string DiskCode
        {
            get => _diskCode;
            set => SetProperty(ref _diskCode, value);
        }

        public string SerialNumber
        {
            get => _serialNumber;
            set => SetProperty(ref _serialNumber, value);
        }

        public string DiskType
        {
            get => _diskType;
            set => SetProperty(ref _diskType, value);
        }

        public string Brand
        {
            get => _brand;
            set => SetProperty(ref _brand, value);
        }

        public string CapacityValue
        {
            get => _capacityValue;
            set => SetProperty(ref _capacityValue, value);
        }

        public string CapacityUnit
        {
            get => _capacityUnit;
            set => SetProperty(ref _capacityUnit, value);
        }

        public string CapacityText => ElectronicMediaCapacitySupport.CombineCapacityText(CapacityValue, CapacityUnit);

        public string InterfaceType
        {
            get => _interfaceType;
            set => SetProperty(ref _interfaceType, value);
        }

        public DateTime? FactoryDate
        {
            get => _factoryDate;
            set => SetProperty(ref _factoryDate, value);
        }

        public string RegistrationMethod
        {
            get => _registrationMethod;
            private set => SetProperty(ref _registrationMethod, value);
        }

        public string RootPath
        {
            get => _rootPath;
            set => SetProperty(ref _rootPath, value);
        }

        public string Year
        {
            get => _year;
            set
            {
                if (SetProperty(ref _year, value))
                {
                    EnsureOption(YearOptions, value);
                    RefreshProjectNameOptions();
                    RefreshProjectHint();
                    RefreshYearProjectHint();
                    _ = RefreshPreviewElectronicArchiveNoAsync();
                }
            }
        }

        public string ProjectName
        {
            get => _projectName;
            set
            {
                if (SetProperty(ref _projectName, value))
                {
                    EnsureOption(ProjectNameOptions, value);
                    TryFillProjectCodeFromRegistered(value);
                    RefreshProjectHint();
                }
            }
        }

        public string ProjectCode
        {
            get => _projectCode;
            set => SetProperty(ref _projectCode, value);
        }

        public string ProjectHint
        {
            get => _projectHint;
            private set => SetProperty(ref _projectHint, value);
        }

        public string YearProjectHint
        {
            get => _yearProjectHint;
            private set => SetProperty(ref _yearProjectHint, value);
        }

        public string ExistingBagHint
        {
            get => _existingBagHint;
            private set => SetProperty(ref _existingBagHint, value);
        }

        public string BusinessNumberHint
        {
            get => _businessNumberHint;
            private set => SetProperty(ref _businessNumberHint, value);
        }

        /// <summary>
        /// 标题行展示的介质袋编号预览。年度取自目录扫描「年度」字段（项目实施年度），不占用号段。
        /// </summary>
        public string PreviewElectronicArchiveNo
        {
            get => _previewElectronicArchiveNo;
            private set
            {
                if (SetProperty(ref _previewElectronicArchiveNo, value))
                {
                    OnPropertyChanged(nameof(PageTitle));
                }
            }
        }

        /// <summary>
        /// 页面标题（对齐 YA-DSP-ED：功能名 · 编号）。
        /// </summary>
        public string PageTitle
        {
            get
            {
                string archiveNo = string.IsNullOrWhiteSpace(PreviewElectronicArchiveNo)
                    ? "待扫描年度"
                    : PreviewElectronicArchiveNo.Trim();
                return $"存量硬盘直办立档 · {archiveNo}";
            }
        }

        public string SourceType => _sourceType;

        public string ArchivePurpose
        {
            get => _archivePurpose;
            set => SetProperty(ref _archivePurpose, value);
        }

        public string ConfidentialLevel
        {
            get => _confidentialLevel;
            set => SetProperty(ref _confidentialLevel, value);
        }

        public string ProvideUnit => _provideUnit;

        public string MaterialCategory
        {
            get => _materialCategory;
            set
            {
                if (SetProperty(ref _materialCategory, value))
                {
                    RefreshSubCategoryOptions();
                }
            }
        }

        public string SubCategory
        {
            get => _subCategory;
            set => SetProperty(ref _subCategory, value);
        }

        public HardDiskMediaReturnTargetLocationOption? SelectedSlotOption
        {
            get => _selectedSlotOption;
            set
            {
                if (SetProperty(ref _selectedSlotOption, value) && value != null && !_suppressSlotResolve)
                {
                    _ = ResolveSelectedSlotAsync(value.Location);
                }
            }
        }

        public string StorageLocation
        {
            get => _storageLocation;
            set
            {
                if (SetProperty(ref _storageLocation, value))
                {
                    OnPropertyChanged(nameof(CanShowSlotSnapshot));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool CanShowSlotSnapshot =>
            ArchiveSlotLocationSupport.TryParseSlotLocation(StorageLocation, out _, out _, out _, out _);

        public string ScanWarningText
        {
            get => _scanWarningText;
            private set => SetProperty(ref _scanWarningText, value);
        }

        public string PreviewSummary
        {
            get
            {
                int materials = PreviewRows.Select(row => row.MaterialName).Distinct(StringComparer.Ordinal).Count();
                return $"共 {materials} 份资料、{PreviewRows.Count} 个子项";
            }
        }

        /// <summary>
        /// 加载域值与推荐档口。
        /// </summary>
        public async Task InitializeAsync()
        {
            _pageDomainOptions = await _archiveRegisterService.GetPageDomainOptionsAsync();
            var options = _pageDomainOptions;
            Replace(ArchivePurposeOptions, options.ArchivePurposes, ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage);
            Replace(ConfidentialLevelOptions, options.ConfidentialLevels, "秘密");
            Replace(MaterialCategoryOptions, options.ElectronicMaterialCategories, ArchiveRegisterDomainValues.ElectronicMaterialCategoryData);
            RefreshYearOptions();
            RefreshProjectNameOptions();

            var diskTypes = await _hardDiskMediaService.GetDomainOptionLabelsAsync(nameof(HardDiskMedium), nameof(HardDiskMedium.DiskType));
            var interfaceTypes = await _hardDiskMediaService.GetDomainOptionLabelsAsync(nameof(HardDiskMedium), nameof(HardDiskMedium.InterfaceType));
            var brands = await _hardDiskMediaService.GetDomainOptionLabelsAsync(nameof(HardDiskMedium), nameof(HardDiskMedium.Brand));
            Replace(DiskTypeOptions, diskTypes, DiskType);
            Replace(InterfaceTypeOptions, interfaceTypes, InterfaceType);
            Replace(BrandOptions, brands, Brand);

            if (string.IsNullOrWhiteSpace(ArchivePurpose) || !ArchivePurposeOptions.Contains(ArchivePurpose))
            {
                ArchivePurpose = ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage;
            }

            if (string.IsNullOrWhiteSpace(ConfidentialLevel) || !ConfidentialLevelOptions.Contains(ConfidentialLevel))
            {
                ConfidentialLevel = ConfidentialLevelOptions.Contains("秘密") ? "秘密" : ConfidentialLevelOptions.FirstOrDefault() ?? "秘密";
            }

            if (string.IsNullOrWhiteSpace(MaterialCategory))
            {
                MaterialCategory = ArchiveRegisterDomainValues.ElectronicMaterialCategoryData;
            }

            RefreshSubCategoryOptions();
            RegistrationMethod = HardDiskMedium.RegistrationMethodArchive;
            await LoadSlotOptionsAsync();
            await RecommendSlotAsync();
        }

        private void RefreshYearOptions()
        {
            string current = Year?.Trim() ?? string.Empty;
            YearOptions.Clear();
            foreach (string year in _filingService.ListRegisteredYears())
            {
                YearOptions.Add(year);
            }

            EnsureOption(YearOptions, current);
        }

        private void RefreshProjectNameOptions()
        {
            string current = ProjectName?.Trim() ?? string.Empty;
            ProjectNameOptions.Clear();
            foreach (string name in _filingService.ListRegisteredProjectNames(Year))
            {
                ProjectNameOptions.Add(name);
            }

            EnsureOption(ProjectNameOptions, current);
        }

        private void TryFillProjectCodeFromRegistered(string? projectName)
        {
            string normalizedName = projectName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedName) || string.IsNullOrWhiteSpace(Year))
            {
                return;
            }

            var match = _filingService.ListProjectsByYear(Year)
                .FirstOrDefault(item =>
                    string.Equals(item.ProjectName?.Trim(), normalizedName, StringComparison.Ordinal)
                    && item.Id > 0
                    && !string.IsNullOrWhiteSpace(item.ProjectCode));
            if (match != null)
            {
                ProjectCode = match.ProjectCode.Trim();
            }
        }

        private async Task PickDiskAsync()
        {
            var disk = _dialogService.ShowLocalPhysicalDiskPickerDialog();
            if (disk == null)
            {
                return;
            }

            SerialNumber = disk.SerialNumber;
            DiskType = LocalPhysicalDiskHardwareSupport.MatchDomainOption(DiskTypeOptions, disk.DiskType);
            EnsureOption(DiskTypeOptions, DiskType);
            Brand = string.IsNullOrWhiteSpace(disk.Brand) ? disk.Model?.Trim() ?? string.Empty : disk.Brand.Trim();
            EnsureOption(BrandOptions, Brand);
            if (ElectronicMediaCapacitySupport.TrySplitCapacityText(disk.CapacityText, out string capacityValue, out string capacityUnit)
                || (!string.IsNullOrWhiteSpace(disk.CapacityValue) && !string.IsNullOrWhiteSpace(disk.CapacityUnit)))
            {
                CapacityValue = string.IsNullOrWhiteSpace(capacityValue) ? disk.CapacityValue : capacityValue;
                CapacityUnit = string.IsNullOrWhiteSpace(capacityUnit) ? disk.CapacityUnit : capacityUnit;
            }
            else if (!string.IsNullOrWhiteSpace(disk.CapacityValue))
            {
                CapacityValue = disk.CapacityValue;
                CapacityUnit = string.IsNullOrWhiteSpace(disk.CapacityUnit)
                    ? ElectronicMediaCapacitySupport.DefaultCapacityUnit
                    : disk.CapacityUnit;
            }

            InterfaceType = LocalPhysicalDiskHardwareSupport.MatchDomainOption(InterfaceTypeOptions, disk.InterfaceType);
            EnsureOption(InterfaceTypeOptions, InterfaceType);
            FactoryDate = disk.FactoryDate;
            RegistrationMethod = HardDiskMedium.RegistrationMethodArchive;

            string firstLetter = (disk.DriveLetters ?? string.Empty)
                .Split('、', ',', ';', ' ')
                .Select(part => part.Trim())
                .FirstOrDefault(part => part.Length >= 2 && part[1] == ':')
                ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(firstLetter))
            {
                RootPath = firstLetter.EndsWith("\\", StringComparison.Ordinal) ? firstLetter : firstLetter + "\\";
            }

            var existing = await _filingService.FindMediumBySerialNumberAsync(SerialNumber);
            if (existing != null)
            {
                DiskCode = existing.DiskCode;
                _dialogService.ShowMessage($"序列号已在库：硬盘编号 [{existing.DiskCode}]，状态 [{existing.Ledger?.MediaStatus}]。若为空白盘，提交时将转为数据盘入袋。");
            }
            else if (string.IsNullOrWhiteSpace(DiskCode))
            {
                await GenerateDiskCodeAsync();
            }
        }

        private async Task GenerateDiskCodeAsync()
        {
            DiskCode = await _hardDiskMediaService.GenerateNextDiskCodeAsync();
        }

        private void PickRoot()
        {
            string? folder = _dialogService.PickFolder("选择硬盘根目录（年度文件夹所在位置）");
            if (!string.IsNullOrWhiteSpace(folder))
            {
                RootPath = folder;
            }
        }

        private async Task ScanAsync()
        {
            IsBusy = true;
            _dialogService.SetBusyState(true);
            try
            {
                var result = _filingService.ScanDirectory(RootPath);
                if (!result.Succeeded)
                {
                    PreviewRows.Clear();
                    ScanWarningText = string.Empty;
                    _dialogService.ShowMessage(result.ErrorMessage, "目录扫描");
                    OnPropertyChanged(nameof(PreviewSummary));
                    return;
                }

                Year = result.Year;
                ProjectName = result.ProjectName;
                EnsureOption(YearOptions, Year);
                RefreshProjectNameOptions();
                EnsureOption(ProjectNameOptions, ProjectName);
                PreviewRows.Clear();
                foreach (var material in result.Materials)
                {
                    foreach (var item in material.Items)
                    {
                        PreviewRows.Add(new StockHardDiskPreviewRow(material, item));
                    }
                }

                ScanWarningText = result.Warnings.Count == 0
                    ? string.Empty
                    : string.Join(Environment.NewLine, result.Warnings);
                RefreshProjectHint();
                RefreshYearProjectHint();
                await RefreshExistingBagHintAsync();
                await RefreshPreviewElectronicArchiveNoAsync();
                await RefreshBusinessNumberHintAsync();
                OnPropertyChanged(nameof(PreviewSummary));
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
            }
        }

        private async Task RecommendSlotAsync()
        {
            await LoadSlotOptionsAsync();
            string? recommended = await _filingService.RecommendDataSlotLocationAsync();
            if (string.IsNullOrWhiteSpace(recommended))
            {
                _dialogService.ShowMessage("未找到仍有容量的年度数据硬盘专用档口，请先在磁盘柜开柜界面完成设置。");
                return;
            }

            StorageLocation = recommended;
            _suppressSlotResolve = true;
            try
            {
                SelectedSlotOption = SlotOptions.FirstOrDefault(item =>
                    recommended.StartsWith(item.Location, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                _suppressSlotResolve = false;
            }
        }

        private async Task LoadSlotOptionsAsync()
        {
            var options = await _filingService.GetDataSlotOptionsAsync();
            SlotOptions.Clear();
            foreach (var option in options)
            {
                SlotOptions.Add(option);
            }
        }

        private async Task ResolveSelectedSlotAsync(string slotLocation)
        {
            try
            {
                StorageLocation = await _filingService.ResolveDataFullLocationAsync(slotLocation);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private void ViewYearProjects()
        {
            if (string.IsNullOrWhiteSpace(Year))
            {
                _dialogService.ShowMessage("请先填写年度或扫描目录，再查看该年已有项目。", "年度已有项目");
                return;
            }

            var projects = _filingService.ListProjectsByYear(Year);
            RefreshYearProjectHint(projects);
            var selected = _dialogService.ShowYearProjectPickDialog(Year, projects);
            if (selected == null)
            {
                return;
            }

            ProjectName = selected.ProjectName?.Trim() ?? string.Empty;
            ProjectCode = selected.ProjectCode?.Trim() ?? string.Empty;
            EnsureOption(ProjectNameOptions, ProjectName);
            RefreshProjectHint();
            _ = RefreshExistingBagHintAsync();
        }

        private void RefreshProjectHint()
        {
            if (string.IsNullOrWhiteSpace(Year) || string.IsNullOrWhiteSpace(ProjectName))
            {
                ProjectHint = "请先扫描目录。";
                return;
            }

            var project = _filingService.FindProject(Year, ProjectName);
            ProjectHint = project == null
                ? "库中无此年度/项目，提交时将自动新建（项目编号非必填）。若实为已有项目，请先查看年度已有项目并采用其名称。"
                : $"已匹配项目：{project.ProjectName}（{project.ImplementYear}）";
        }

        private void RefreshYearProjectHint(IReadOnlyList<ProjectInfo>? projects = null)
        {
            if (string.IsNullOrWhiteSpace(Year))
            {
                YearProjectHint = "填写年度或扫描目录后，可查看该年已有项目。";
                return;
            }

            IReadOnlyList<ProjectInfo> yearProjects = projects ?? _filingService.ListProjectsByYear(Year);
            YearProjectHint = yearProjects.Count == 0
                ? $"库中尚无 {Year.Trim()} 年度项目（含模拟盒/电子袋），提交时将按当前名称新建。"
                : $"{Year.Trim()} 年度已登记 {yearProjects.Count} 个项目（项目信息 ∪ 模拟盒 ∪ 电子袋）。同一项目请采用已有名称，勿另起别名。";
        }

        private async Task RefreshExistingBagHintAsync()
        {
            if (string.IsNullOrWhiteSpace(Year) || string.IsNullOrWhiteSpace(ProjectName))
            {
                ExistingBagHint = string.Empty;
                return;
            }

            int count = await _filingService.CountExistingHardDiskBagsAsync(ProjectName, Year);
            ExistingBagHint = count <= 0
                ? "该年度/项目尚无电子硬盘袋，本次将建立第一袋。"
                : $"该年度/项目已有 {count} 个电子硬盘袋，本次将新建立第 {count + 1} 袋。";
        }

        private async Task RefreshPreviewElectronicArchiveNoAsync()
        {
            int token = ++_archiveNoPreviewToken;
            string year = Year?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(year))
            {
                PreviewElectronicArchiveNo = string.Empty;
                return;
            }

            try
            {
                // 存量直办：袋号年度必须用目录扫描/项目实施年度，不得回退到系统当前年。
                string preview = await _filingService.PeekNextElectronicArchiveNoAsync(year);
                if (token != _archiveNoPreviewToken)
                {
                    return;
                }

                PreviewElectronicArchiveNo = preview;
            }
            catch
            {
                if (token != _archiveNoPreviewToken)
                {
                    return;
                }

                PreviewElectronicArchiveNo = $"年度电子-{year}-???";
            }
        }

        private async Task RefreshBusinessNumberHintAsync()
        {
            int materialCount = PreviewRows.Select(row => row.MaterialName).Distinct(StringComparer.Ordinal).Count();
            if (materialCount <= 0 || string.IsNullOrWhiteSpace(Year))
            {
                BusinessNumberHint = "确认立档后：每份资料生成一条建档单号，整盘生成一个电子袋号。";
                return;
            }

            if (string.IsNullOrWhiteSpace(PreviewElectronicArchiveNo))
            {
                await RefreshPreviewElectronicArchiveNoAsync();
            }

            string previewBagNo = PreviewElectronicArchiveNo;
            BusinessNumberHint =
                $"确认立档后将生成 {materialCount} 条建档单号（入库申请编号），电子袋号预计为 [{previewBagNo}]（确认写入时按当时库内最大号顺延；建档单号、电子袋号、立档编号的年度均取自项目年度）。";
        }

        private async Task ShowSlotSnapshotAsync()
        {
            if (!ArchiveSlotLocationSupport.TryParseSlotLocation(
                    StorageLocation,
                    out string cabinetName,
                    out string side,
                    out int row,
                    out int column))
            {
                _dialogService.ShowMessage("当前档口无法解析，请先推荐空位或从列表选择后再查看快照。");
                return;
            }

            var cabinet = (await _cabinetService.GetAllCabinetsAsync())
                .FirstOrDefault(item => item.Type == CabinetType.MagneticDisk
                    && string.Equals(item.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
            if (cabinet == null)
            {
                _dialogService.ShowMessage($"未找到柜号 [{cabinetName}] 对应的防磁磁盘柜。");
                return;
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
        }

        private async Task ConfirmAsync()
        {
            if (!_archiveRegisterService.IsArchiveAdminUser(_userContextService.CurrentUser))
            {
                _dialogService.ShowMessage("仅资料室资料管理员可执行存量硬盘直办立档。");
                return;
            }

            if (PreviewRows.Count == 0)
            {
                _dialogService.ShowMessage("请先扫描硬盘目录。");
                return;
            }

            var request = BuildRequest();
            var errors = await _filingService.CollectCommitErrorsAsync(request, _userContextService.CurrentUser);
            if (errors.Count > 0)
            {
                _dialogService.ShowMessage(string.Join(Environment.NewLine, errors), "请先完善立档信息");
                return;
            }

            string confirmText =
                $"即将把硬盘 [{DiskCode}] 作为数据盘登记，并按扫描结果直接立档。\n"
                + $"年度/项目：{Year} / {ProjectName}\n"
                + $"来源：{SourceType}　提供单位：{ProvideUnit}\n"
                + $"档口：{StorageLocation}\n"
                + $"{PreviewSummary}\n"
                + $"{ExistingBagHint}\n"
                + $"{BusinessNumberHint}\n\n是否继续？";
            if (!_dialogService.ShowConfirm(confirmText, "确认存量直办立档"))
            {
                return;
            }

            IsBusy = true;
            _dialogService.SetBusyState(true);
            try
            {
                var result = await _filingService.CommitAsync(request, _userContextService.CurrentUser);

                if (!result.Succeeded)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                _dialogService.ShowMessage(result.Message + (result.FormNos.Count == 0
                    ? string.Empty
                    : Environment.NewLine + "建档单号：" + string.Join("、", result.FormNos)));
                await ResetForNextFilingAsync();
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
            }
        }

        /// <summary>
        /// 立档成功后清空表单并恢复初始状态，便于连续办理下一盘。
        /// </summary>
        private async Task ResetForNextFilingAsync()
        {
            // 取消进行中的袋号预览，避免异步回调把上一盘编号写回标题。
            _archiveNoPreviewToken++;

            ClearUserInputsForNextRound();
            ArchivePurpose = string.Empty;
            ConfidentialLevel = string.Empty;
            MaterialCategory = string.Empty;
            SubCategory = string.Empty;

            _suppressSlotResolve = true;
            try
            {
                SelectedSlotOption = null;
            }
            finally
            {
                _suppressSlotResolve = false;
            }

            StorageLocation = string.Empty;
            SlotOptions.Clear();

            await InitializeAsync();

            // WPF 可编辑 ComboBox：ItemsSource Clear/Replace 时常把旧 Text 写回绑定，
            // 导致年度/项目/类型等在初始化后仍残留上一盘内容，须再清一次。
            ClearUserInputsForNextRound();
            ForceNotifyEditableComboTexts();

            await GenerateDiskCodeAsync();
        }

        /// <summary>
        /// 清空本轮用户录入与扫描结果（保留/交由 Initialize 恢复的库管默认值与推荐档口）。
        /// </summary>
        private void ClearUserInputsForNextRound()
        {
            DiskCode = string.Empty;
            SerialNumber = string.Empty;
            DiskType = string.Empty;
            Brand = string.Empty;
            CapacityValue = string.Empty;
            CapacityUnit = ElectronicMediaCapacitySupport.DefaultCapacityUnit;
            InterfaceType = string.Empty;
            FactoryDate = null;
            RootPath = string.Empty;
            Year = string.Empty;
            ProjectName = string.Empty;
            ProjectCode = string.Empty;
            ScanWarningText = string.Empty;
            ExistingBagHint = string.Empty;
            PreviewRows.Clear();
            ProjectHint = "请先扫描目录。";
            YearProjectHint = "填写年度或扫描目录后，可查看该年已有项目。";
            BusinessNumberHint = "确认立档后：每份资料生成一条建档单号，整盘生成一个电子袋号。";
            PreviewElectronicArchiveNo = string.Empty;
            OnPropertyChanged(nameof(PreviewSummary));
            OnPropertyChanged(nameof(PageTitle));
        }

        /// <summary>
        /// 强制刷新可编辑 ComboBox 的 Text 绑定（绕过 SetProperty 相等短路）。
        /// </summary>
        private void ForceNotifyEditableComboTexts()
        {
            OnPropertyChanged(nameof(DiskType));
            OnPropertyChanged(nameof(Brand));
            OnPropertyChanged(nameof(InterfaceType));
            OnPropertyChanged(nameof(Year));
            OnPropertyChanged(nameof(ProjectName));
        }

        private static void Replace(ObservableCollection<string> target, IReadOnlyList<string> values, string preferred)
        {
            target.Clear();
            foreach (string value in values.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.Ordinal))
            {
                target.Add(value);
            }

            if (!string.IsNullOrWhiteSpace(preferred) && !target.Contains(preferred))
            {
                target.Insert(0, preferred);
            }
        }

        private StockHardDiskDirectFilingRequest BuildRequest()
        {
            var materials = PreviewRows
                .GroupBy(row => row.MaterialName, StringComparer.Ordinal)
                .Select(group => new StockHardDiskMaterialDraft
                {
                    MaterialName = group.Key,
                    Items = group.Select(row => row.Item).ToList()
                })
                .ToList();

            return new StockHardDiskDirectFilingRequest
            {
                RootPath = RootPath,
                Year = Year,
                ProjectName = ProjectName,
                ProjectCode = ProjectCode,
                DiskCode = DiskCode,
                SerialNumber = SerialNumber,
                DiskType = DiskType,
                Brand = Brand,
                Capacity = CapacityText,
                InterfaceType = InterfaceType,
                FactoryDate = FactoryDate,
                StorageLocation = StorageLocation,
                SourceType = ArchiveRegisterDomainValues.SourceTypeStockDirect,
                ArchivePurpose = ArchivePurpose,
                ConfidentialLevel = ConfidentialLevel,
                ProvideUnit = ArchiveRegisterDomainValues.ProvideUnitArchiveRoom,
                MaterialCategory = MaterialCategory,
                SubCategory = SubCategory,
                Materials = materials
            };
        }

        private void RefreshSubCategoryOptions()
        {
            IReadOnlyList<string> options = _pageDomainOptions == null
                ? Array.Empty<string>()
                : string.Equals(MaterialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocument, StringComparison.Ordinal)
                    ? _pageDomainOptions.ElectronicDocumentSubCategories
                    : string.Equals(MaterialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategoryData, StringComparison.Ordinal)
                        ? _pageDomainOptions.ElectronicDataSubCategories
                        : string.Equals(MaterialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategorySoftware, StringComparison.Ordinal)
                            ? _pageDomainOptions.ElectronicSoftwareSubCategories
                            : Array.Empty<string>();

            string preferred = string.Equals(
                    MaterialCategory,
                    ArchiveRegisterDomainValues.ElectronicMaterialCategoryData,
                    StringComparison.Ordinal)
                ? ArchiveRegisterDomainValues.DefaultStockDirectSubCategory
                : SubCategory;
            Replace(SubCategoryOptions, options, preferred);
            if (string.IsNullOrWhiteSpace(SubCategory) || !SubCategoryOptions.Contains(SubCategory))
            {
                SubCategory = SubCategoryOptions.FirstOrDefault() ?? string.Empty;
            }
        }

        private static void EnsureOption(ObservableCollection<string> target, string? value)
        {
            string trimmed = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed)
                || target.Any(item => string.Equals(item, trimmed, StringComparison.Ordinal)))
            {
                return;
            }

            target.Insert(0, trimmed);
        }
    }

    /// <summary>
    /// 扫描预览行。
    /// </summary>
    public sealed class StockHardDiskPreviewRow
    {
        public StockHardDiskPreviewRow(StockHardDiskMaterialDraft material, StockHardDiskItemDraft item)
        {
            Material = material;
            Item = item;
        }

        public StockHardDiskMaterialDraft Material { get; }

        public StockHardDiskItemDraft Item { get; }

        public string MaterialName => Material.MaterialName;

        public string ItemName => Item.ItemName;

        public string OrganizationForm => Item.DataOrganizationForm;

        public string DataSizeText => ElectronicMediaCapacitySupport.FormatCapacityMb(Item.DataSizeMb);

        public int FileCount => Item.FileCount;

        public string FilingStoragePath => Item.FilingStoragePath;
    }
}
