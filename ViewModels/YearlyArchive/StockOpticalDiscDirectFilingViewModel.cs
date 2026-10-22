using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DocMgr.Models.Cabinets;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.Projects;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;
using DocMgr.ViewModels.Shared;

namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 存量光盘直办立档向导。
    /// </summary>
    public sealed class StockOpticalDiscDirectFilingViewModel : ViewModelBase
    {
        private readonly IStockOpticalDiscDirectFilingService _filingService;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly ICabinetService _cabinetService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly IUserService _userService;
        private readonly PageDbOperationQueue _pageDb = new();
        private ArchiveRegisterPageDomainOptions? _pageDomainOptions;
        private StockOpticalDiscDirectoryScanResult? _scan;
        private bool _suppressModeRebuild;
        private bool _suppressSourceProvidePropagate;
        private bool _suppressSlotResolve;

        private string _rootPath = string.Empty;
        private string _year = string.Empty;
        private string _projectName = string.Empty;
        private string _projectCode = string.Empty;
        private string _projectHint = "请先填写年度和项目，或扫描光盘后再填写。";
        private string _yearProjectHint = "填写年度后，可查看该年已有项目。";
        private string _existingBagHint = string.Empty;
        private string _businessNumberHint = "确认立档后：每份资料生成一条建档单号，整张光盘生成一个电子袋号。";
        private string _previewElectronicArchiveNo = string.Empty;
        private int _archiveNoPreviewToken;
        private string _sourceType = ArchiveRegisterDomainValues.SourceTypeInternal;
        private string _archivePurpose = ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage;
        private string _provideUnit = ArchiveRegisterDomainValues.ProvideUnitArchiveRoom;
        private string _assignMode = AssignModeWholeDisc;
        private bool _canAssignByFolder;
        private bool _canAssignByExtension;
        private bool _ignoredContentAcknowledged;
        private HardDiskMediaReturnTargetLocationOption? _selectedSlotOption;
        private string _storageLocation = string.Empty;
        private string _scanWarningText = string.Empty;
        private bool _isBusy;
        private string _batchSourceType = string.Empty;
        private string _batchProvideUnit = string.Empty;
        private string _batchMaterialCategory = string.Empty;
        private string _batchSubCategory = string.Empty;
        private string _batchConfidentialLevel = string.Empty;
        private bool _suppressBatchProvide;

        public StockOpticalDiscDirectFilingViewModel(
            IStockOpticalDiscDirectFilingService filingService,
            IArchiveRegisterService archiveRegisterService,
            ICabinetService cabinetService,
            IDialogService dialogService,
            IUserContextService userContextService,
            IUserService userService)
        {
            _filingService = filingService;
            _archiveRegisterService = archiveRegisterService;
            _cabinetService = cabinetService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _userService = userService;

            PickRootCommand = new RelayCommand(_ => PickRoot(), _ => !IsBusy);
            ScanCommand = new RelayCommand(async _ => await _pageDb.RunAsync(ScanAsync), _ => !IsBusy && !string.IsNullOrWhiteSpace(RootPath));
            ViewYearProjectsCommand = new RelayCommand(_ => ViewYearProjects(), _ => !IsBusy);
            RecommendSlotCommand = new RelayCommand(async _ => await _pageDb.RunAsync(RecommendSlotAsync), _ => !IsBusy);
            ShowSlotSnapshotCommand = new RelayCommand(async _ => await _pageDb.RunAsync(ShowSlotSnapshotAsync), _ => !IsBusy && CanShowSlotSnapshot);
            ConfirmCommand = new RelayCommand(async _ => await _pageDb.RunAsync(ConfirmAsync), _ => !IsBusy);
            SelectAllExtensionsCommand = new RelayCommand(_ => SetIncluded(ExtensionRows, true), _ => !IsBusy && ExtensionRows.Count > 0);
            ClearExtensionsCommand = new RelayCommand(_ => SetIncluded(ExtensionRows, false), _ => !IsBusy && ExtensionRows.Count > 0);
            InvertExtensionsCommand = new RelayCommand(_ => InvertIncluded(ExtensionRows), _ => !IsBusy && ExtensionRows.Count > 0);
            SelectAllPreviewCommand = new RelayCommand(_ => SetIncluded(PreviewRows, true), _ => !IsBusy && PreviewRows.Count > 0);
            ClearPreviewCommand = new RelayCommand(_ => SetIncluded(PreviewRows, false), _ => !IsBusy && PreviewRows.Count > 0);
            InvertPreviewCommand = new RelayCommand(_ => InvertIncluded(PreviewRows), _ => !IsBusy && PreviewRows.Count > 0);
            ApplyPreviewBatchCommand = new RelayCommand(_ => ApplyPreviewBatch(), _ => !IsBusy && PreviewRows.Count > 0);
        }

        /// <summary>根目录可编辑下拉的初始盘符：D:\ 至 K:\。</summary>
        public ObservableCollection<string> RootPathOptions { get; } = new(
            Enumerable.Range('D', 'K' - 'D' + 1).Select(letter => $"{(char)letter}:\\"));

        public ObservableCollection<string> YearOptions { get; } = new();
        public ObservableCollection<string> ProjectNameOptions { get; } = new();
        public ObservableCollection<string> ArchivePurposeOptions { get; } = new();
        public ObservableCollection<string> SourceTypeOptions { get; } = new();
        public ObservableCollection<string> ConfidentialLevelOptions { get; } = new();
        public ObservableCollection<string> MaterialCategoryOptions { get; } = new();
        /// <summary>部门列表，供内部「提供单位」可编辑下拉绑定。</summary>
        public ObservableCollection<Department> Departments { get; } = new();
        public ObservableCollection<HardDiskMediaReturnTargetLocationOption> SlotOptions { get; } = new();
        public ObservableCollection<StockOpticalDiscPreviewRow> PreviewRows { get; } = new();

        /// <summary>按后缀指派时的过滤与组织行。子项名是后缀，资料名称用于把多个后缀归成一份资料。</summary>
        public ObservableCollection<StockOpticalDiscPreviewRow> ExtensionRows { get; } = new();

        /// <summary>把资料所属目录整体登记为一个子项。</summary>
        public const string AssignModeWholeDisc = "WholeDisc";

        /// <summary>按一级文件夹指派子项。</summary>
        public const string AssignModeFolder = "Folder";

        /// <summary>按资料所属目录下一层文件的后缀指派子项。</summary>
        public const string AssignModeExtension = "Extension";

        public ICommand PickRootCommand { get; }
        public ICommand ScanCommand { get; }
        public ICommand ViewYearProjectsCommand { get; }
        public ICommand RecommendSlotCommand { get; }
        public ICommand ShowSlotSnapshotCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand SelectAllExtensionsCommand { get; }
        public ICommand ClearExtensionsCommand { get; }
        public ICommand InvertExtensionsCommand { get; }
        public ICommand SelectAllPreviewCommand { get; }
        public ICommand ClearPreviewCommand { get; }
        public ICommand InvertPreviewCommand { get; }
        public ICommand ApplyPreviewBatchCommand { get; }

        /// <summary>扫描预览批量赋值可选子类，随批量资料类型变化。</summary>
        public ObservableCollection<string> BatchSubCategoryOptions { get; } = new();

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

        /// <summary>登记方式。存量光盘直办固定为资料存档登记。</summary>
        public string RegistrationMethod => OpticalDiscMedium.RegistrationMethodArchive;

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
                    RefreshCatalogPaths();
                    _pageDb.RunInBackground(RefreshPreviewElectronicArchiveNoAsync);
                    _pageDb.RunInBackground(RefreshExistingBagHintAsync);
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
                    RefreshCatalogPaths();
                    _pageDb.RunInBackground(RefreshExistingBagHintAsync);
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
        /// 介质袋编号预览。年度取自页面「年度」，确认写入前不占用号段。
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

        /// <summary>页面标题。</summary>
        public string PageTitle
        {
            get
            {
                string archiveNo = string.IsNullOrWhiteSpace(PreviewElectronicArchiveNo)
                    ? "待填写年度"
                    : PreviewElectronicArchiveNo.Trim();
                return $"存量光盘资料直办立档 · {archiveNo}";
            }
        }

        /// <summary>指派方式：所属目录一个子项、按文件夹指派、按后缀指派。</summary>
        public string AssignMode
        {
            get => _assignMode;
            set
            {
                string normalized = string.IsNullOrWhiteSpace(value) ? AssignModeWholeDisc : value.Trim();
                if (!SetProperty(ref _assignMode, normalized))
                {
                    return;
                }

                OnPropertyChanged(nameof(IsExtensionAssignMode));
                OnPropertyChanged(nameof(IgnoreConfirmText));
                if (!_suppressModeRebuild)
                {
                    RebuildPreviewFromScan();
                }
                else
                {
                    OnPropertyChanged(nameof(HasUnassignedContent));
                }
            }
        }

        /// <summary>当前是否按后缀指派。</summary>
        public bool IsExtensionAssignMode =>
            string.Equals(AssignMode, AssignModeExtension, StringComparison.Ordinal);

        /// <summary>扫描结果含一级业务文件夹时，才可按文件夹指派。</summary>
        public bool CanAssignByFolder
        {
            get => _canAssignByFolder;
            private set => SetProperty(ref _canAssignByFolder, value);
        }

        /// <summary>资料所属目录下一层存在带后缀的文件时，才可按后缀指派。</summary>
        public bool CanAssignByExtension
        {
            get => _canAssignByExtension;
            private set => SetProperty(ref _canAssignByExtension, value);
        }

        /// <summary>当前指派方式下，还有未纳入内容，确认前必须勾选忽略。</summary>
        public bool HasUnassignedContent
        {
            get
            {
                if (_scan == null)
                {
                    return false;
                }

                if (string.Equals(AssignMode, AssignModeFolder, StringComparison.Ordinal))
                {
                    return PreviewRows.Any(row => !row.Include) || _scan.LooseFileCount > 0;
                }

                if (IsExtensionAssignMode)
                {
                    return ExtensionRows.Any(row => !row.Include)
                        || _scan.ExtensionlessFileCount > 0;
                }

                return false;
            }
        }

        /// <summary>忽略确认勾选的文案，随指派方式变化。</summary>
        public string IgnoreConfirmText => IsExtensionAssignMode
            ? "已确认忽略未纳入的后缀和无后缀文件"
            : "已确认忽略未纳入的文件夹和资料所属目录散文件";

        public bool IgnoredContentAcknowledged
        {
            get => _ignoredContentAcknowledged;
            set => SetProperty(ref _ignoredContentAcknowledged, value);
        }

        public string SourceType
        {
            get => _sourceType;
            set
            {
                string normalized = value?.Trim() ?? string.Empty;
                string previousSource = _sourceType;
                string previousProvide = _provideUnit;
                if (!SetProperty(ref _sourceType, normalized))
                {
                    return;
                }

                OnPropertyChanged(nameof(IsExternalSource));
                ApplyProvideUnitForSourceType();
                PropagateDefaultSourceProvideUnit(previousSource, previousProvide);
            }
        }

        /// <summary>资料来源是否为「外来」。</summary>
        public bool IsExternalSource =>
            string.Equals(SourceType, ArchiveRegisterDomainValues.SourceTypeExternal, StringComparison.Ordinal);

        public string ArchivePurpose
        {
            get => _archivePurpose;
            set => SetProperty(ref _archivePurpose, value);
        }

        public string ProvideUnit
        {
            get => _provideUnit;
            set
            {
                string previous = _provideUnit;
                string normalized = value ?? string.Empty;
                if (!SetProperty(ref _provideUnit, normalized))
                {
                    return;
                }

                PropagateDefaultProvideUnit(previous);
            }
        }

        public HardDiskMediaReturnTargetLocationOption? SelectedSlotOption
        {
            get => _selectedSlotOption;
            set
            {
                if (SetProperty(ref _selectedSlotOption, value) && value != null && !_suppressSlotResolve)
                {
                    _pageDb.RunInBackground(() => ResolveSelectedSlotAsync(value.Location));
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
                var included = PreviewRows.Where(row => row.Include).ToList();
                int materials = included.Select(row => row.MaterialName).Distinct(StringComparer.Ordinal).Count();
                return $"本次纳入 {materials} 份资料、{included.Count} 个子项。编目路径按年度/项目/资料/子项生成，不改写光盘目录。";
            }
        }

        /// <summary>加载域值与推荐档口。</summary>
        public async Task InitializeAsync()
        {
            _pageDomainOptions = await _archiveRegisterService.GetPageDomainOptionsAsync();
            var options = _pageDomainOptions;
            Replace(
                ArchivePurposeOptions,
                ArchiveRegisterDomainValues.FilterDirectFilingArchivePurposes(options.ArchivePurposes),
                ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage);
            Replace(
                SourceTypeOptions,
                options.SourceTypes.Count > 0
                    ? options.SourceTypes
                    : new[]
                    {
                        ArchiveRegisterDomainValues.SourceTypeInternal,
                        ArchiveRegisterDomainValues.SourceTypeExternal
                    },
                ArchiveRegisterDomainValues.SourceTypeInternal);
            Replace(ConfidentialLevelOptions, options.ConfidentialLevels, "秘密");
            Replace(MaterialCategoryOptions, options.ElectronicMaterialCategories, ArchiveRegisterDomainValues.ElectronicMaterialCategoryData);
            LoadDepartments();
            RefreshYearOptions();
            RefreshProjectNameOptions();

            if (string.IsNullOrWhiteSpace(ArchivePurpose) || !ArchivePurposeOptions.Contains(ArchivePurpose))
            {
                ArchivePurpose = ArchiveOutboundDomainValues.ArchivePurposeLongTermStorage;
            }

            _suppressSourceProvidePropagate = true;
            try
            {
                if (string.IsNullOrWhiteSpace(SourceType) || !SourceTypeOptions.Contains(SourceType))
                {
                    SourceType = SourceTypeOptions.FirstOrDefault()
                        ?? ArchiveRegisterDomainValues.SourceTypeInternal;
                }
                else
                {
                    ApplyProvideUnitForSourceType();
                }

                if (string.IsNullOrWhiteSpace(ProvideUnit) && !IsExternalSource)
                {
                    ProvideUnit = ResolveDefaultInternalProvideUnit();
                }
            }
            finally
            {
                _suppressSourceProvidePropagate = false;
            }

            foreach (var row in PreviewRows)
            {
                row.EnsureDefaultsFromOwner();
            }

            await LoadSlotOptionsAsync();
            await RecommendSlotAsync();
        }

        /// <summary>预览行的资料名称、子项或纳入状态变化后刷新摘要。</summary>
        internal void NotifyPreviewChanged()
        {
            OnPropertyChanged(nameof(PreviewSummary));
            OnPropertyChanged(nameof(HasUnassignedContent));
            CommandManager.InvalidateRequerySuggested();
            _pageDb.RunInBackground(RefreshBusinessNumberHintAsync);
        }

        /// <summary>扫描预览批量赋值的来源。</summary>
        public string BatchSourceType
        {
            get => _batchSourceType;
            set
            {
                string trimmed = value?.Trim() ?? string.Empty;
                if (!SetProperty(ref _batchSourceType, trimmed))
                {
                    return;
                }

                OnPropertyChanged(nameof(IsBatchExternalSource));
                ApplyBatchProvideUnitForSourceType();
            }
        }

        /// <summary>批量来源是否为「外来」。</summary>
        public bool IsBatchExternalSource =>
            string.Equals(BatchSourceType, ArchiveRegisterDomainValues.SourceTypeExternal, StringComparison.Ordinal);

        /// <summary>扫描预览批量赋值的提供单位。</summary>
        public string BatchProvideUnit
        {
            get => _batchProvideUnit;
            set => SetProperty(ref _batchProvideUnit, value ?? string.Empty);
        }

        /// <summary>扫描预览批量赋值的资料类型。</summary>
        public string BatchMaterialCategory
        {
            get => _batchMaterialCategory;
            set
            {
                string trimmed = value?.Trim() ?? string.Empty;
                if (!SetProperty(ref _batchMaterialCategory, trimmed))
                {
                    return;
                }

                RefreshBatchSubCategoryOptions();
            }
        }

        /// <summary>扫描预览批量赋值的子类。</summary>
        public string BatchSubCategory
        {
            get => _batchSubCategory;
            set => SetProperty(ref _batchSubCategory, value?.Trim() ?? string.Empty);
        }

        /// <summary>扫描预览批量赋值的密级。</summary>
        public string BatchConfidentialLevel
        {
            get => _batchConfidentialLevel;
            set => SetProperty(ref _batchConfidentialLevel, value?.Trim() ?? string.Empty);
        }

        /// <summary>把已填写的批量字段写入当前扫描预览行。留空的字段保持各行原值。</summary>
        private void ApplyPreviewBatch()
        {
            bool hasSource = !string.IsNullOrWhiteSpace(BatchSourceType);
            bool hasProvideUnit = !string.IsNullOrWhiteSpace(BatchProvideUnit);
            bool hasCategory = !string.IsNullOrWhiteSpace(BatchMaterialCategory);
            bool hasSubCategory = !string.IsNullOrWhiteSpace(BatchSubCategory);
            bool hasConfidential = !string.IsNullOrWhiteSpace(BatchConfidentialLevel);
            if (!hasSource && !hasProvideUnit && !hasCategory && !hasSubCategory && !hasConfidential)
            {
                _dialogService.ShowMessage("请先填写要批量赋值的来源、提供单位、资料类型、子类或密级。");
                return;
            }

            foreach (StockOpticalDiscPreviewRow row in PreviewRows.ToList())
            {
                if (hasSource)
                {
                    row.SourceType = BatchSourceType;
                }

                if (hasProvideUnit)
                {
                    row.ProvideUnit = BatchProvideUnit;
                }

                if (hasCategory)
                {
                    row.MaterialCategory = BatchMaterialCategory;
                }

                if (hasSubCategory)
                {
                    row.SubCategory = BatchSubCategory;
                }

                if (hasConfidential)
                {
                    row.ConfidentialLevel = BatchConfidentialLevel;
                }
            }
        }

        private void ApplyBatchProvideUnitForSourceType()
        {
            if (_suppressBatchProvide || string.IsNullOrWhiteSpace(BatchSourceType))
            {
                return;
            }

            if (IsBatchExternalSource)
            {
                string internalDefault = ResolveDefaultInternalProvideUnit();
                if (string.Equals(BatchProvideUnit, internalDefault, StringComparison.Ordinal)
                    || string.Equals(BatchProvideUnit, ArchiveRegisterDomainValues.ProvideUnitArchiveRoom, StringComparison.Ordinal))
                {
                    BatchProvideUnit = string.Empty;
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(BatchProvideUnit))
            {
                BatchProvideUnit = ResolveDefaultInternalProvideUnit();
            }
        }

        private void RefreshBatchSubCategoryOptions()
        {
            IReadOnlyList<string> options = ResolveSubCategoryOptions(BatchMaterialCategory);
            string captured = BatchSubCategory;
            BatchSubCategoryOptions.Clear();
            foreach (string option in options.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.Ordinal))
            {
                BatchSubCategoryOptions.Add(option);
            }

            if (!string.IsNullOrWhiteSpace(captured) && !BatchSubCategoryOptions.Contains(captured))
            {
                BatchSubCategory = string.Empty;
            }
        }

        private void SetIncluded(IEnumerable<StockOpticalDiscPreviewRow> rows, bool include)
        {
            foreach (StockOpticalDiscPreviewRow row in rows.ToList())
            {
                row.SetIncludeWithoutNotify(include);
            }

            NotifyPreviewChanged();
        }

        private void InvertIncluded(IEnumerable<StockOpticalDiscPreviewRow> rows)
        {
            foreach (StockOpticalDiscPreviewRow row in rows.ToList())
            {
                row.SetIncludeWithoutNotify(!row.Include);
            }

            NotifyPreviewChanged();
        }

        /// <summary>按资料类型解析所属子类域值。</summary>
        internal IReadOnlyList<string> ResolveSubCategoryOptions(string? materialCategory)
        {
            if (_pageDomainOptions == null)
            {
                return Array.Empty<string>();
            }

            if (string.Equals(materialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocument, StringComparison.Ordinal))
            {
                return _pageDomainOptions.ElectronicDocumentSubCategories;
            }

            if (string.Equals(materialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategoryData, StringComparison.Ordinal))
            {
                return _pageDomainOptions.ElectronicDataSubCategories;
            }

            if (string.Equals(materialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategorySoftware, StringComparison.Ordinal))
            {
                return _pageDomainOptions.ElectronicSoftwareSubCategories;
            }

            return Array.Empty<string>();
        }

        /// <summary>扫描预览行的默认密级。</summary>
        internal string ResolveDefaultConfidentialLevel()
        {
            return ConfidentialLevelOptions.Contains("秘密")
                ? "秘密"
                : ConfidentialLevelOptions.FirstOrDefault() ?? "秘密";
        }

        /// <summary>内部来源默认提供单位：当前用户部门，缺省「资料室」。</summary>
        internal string ResolveDefaultInternalProvideUnit()
        {
            string dept = _userContextService.CurrentUser?.Department?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(dept)
                ? ArchiveRegisterDomainValues.ProvideUnitArchiveRoom
                : dept;
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

        private void PickRoot()
        {
            string? folder = _dialogService.PickFolder("选择资料所属目录");
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
                StockOpticalDiscDirectoryScanResult result;
                using (_dialogService.ShowOperationProgress("扫描光盘", "正在读取光盘目录。光盘较慢时请稍候，窗口仍可移动。"))
                {
                    string rootPath = RootPath;
                    result = await Task.Run(() => _filingService.ScanDirectory(rootPath));
                }

                if (!result.Succeeded)
                {
                _scan = null;
                PreviewRows.Clear();
                ExtensionRows.Clear();
                ScanWarningText = string.Empty;
                CanAssignByFolder = false;
                CanAssignByExtension = false;
                _dialogService.ShowMessage(result.ErrorMessage, "目录扫描");
                    NotifyPreviewChanged();
                    return;
                }

                _scan = result;
                IgnoredContentAcknowledged = false;
                BuildExtensionRows(result);
                _suppressModeRebuild = true;
                try
                {
                    CanAssignByFolder = result.Folders.Count > 0;
                    CanAssignByExtension = result.Extensions.Count > 0;
                    AssignMode = result.Folders.Count == 0 ? AssignModeWholeDisc : AssignModeFolder;
                }
                finally
                {
                    _suppressModeRebuild = false;
                }

                ScanWarningText = result.Warnings.Count == 0
                    ? string.Empty
                    : string.Join(Environment.NewLine, result.Warnings);
                RebuildPreviewFromScan();
                RefreshProjectHint();
                RefreshYearProjectHint();
                await RefreshExistingBagHintAsync();
                await RefreshPreviewElectronicArchiveNoAsync();
                await RefreshBusinessNumberHintAsync();
            }
            catch (Exception ex)
            {
                _scan = null;
                PreviewRows.Clear();
                ExtensionRows.Clear();
                ScanWarningText = string.Empty;
                CanAssignByFolder = false;
                CanAssignByExtension = false;
                NotifyPreviewChanged();
                _dialogService.ShowError("扫描光盘失败：" + ex.Message);
            }
            finally
            {
                _dialogService.SetBusyState(false);
                IsBusy = false;
            }
        }

        private void RebuildPreviewFromScan()
        {
            PreviewRows.Clear();
            if (_scan?.WholeDisc == null)
            {
                NotifyPreviewChanged();
                return;
            }

            RefreshCatalogPaths();
            if (IsExtensionAssignMode)
            {
                foreach (var row in ExtensionRows)
                {
                    PreviewRows.Add(row);
                }
            }
            else if (string.Equals(AssignMode, AssignModeFolder, StringComparison.Ordinal) && _scan.Folders.Count > 0)
            {
                foreach (var folder in _scan.Folders)
                {
                    PreviewRows.Add(new StockOpticalDiscPreviewRow(this, folder));
                }
            }
            else
            {
                PreviewRows.Add(new StockOpticalDiscPreviewRow(this, _scan.WholeDisc));
            }

            NotifyPreviewChanged();
        }

        private void BuildExtensionRows(StockOpticalDiscDirectoryScanResult result)
        {
            ExtensionRows.Clear();
            foreach (var draft in result.Extensions)
            {
                var row = new StockOpticalDiscPreviewRow(this, draft);
                row.EnsureDefaultsFromOwner();
                ExtensionRows.Add(row);
            }
        }

        private void RefreshCatalogPaths()
        {
            if (_scan == null)
            {
                return;
            }

            ApplyCatalogPath(_scan.WholeDisc);
            foreach (var folder in _scan.Folders)
            {
                ApplyCatalogPath(folder);
            }

            foreach (var extension in _scan.Extensions)
            {
                ApplyCatalogPath(extension);
            }

            foreach (var row in PreviewRows)
            {
                row.NotifyCatalogPath();
            }
        }

        private void ApplyCatalogPath(StockOpticalDiscItemDraft? draft)
        {
            if (draft == null)
            {
                return;
            }

            draft.FilingStoragePath = StockOpticalDiscDirectFilingDirectorySupport.BuildCatalogPath(
                Year,
                ProjectName,
                draft.MaterialName,
                draft.ItemName);
        }

        private async Task RecommendSlotAsync()
        {
            await LoadSlotOptionsAsync();
            string? recommended = await _filingService.RecommendDataSlotLocationAsync();
            if (string.IsNullOrWhiteSpace(recommended))
            {
                _dialogService.ShowMessage("未找到仍有容量的年度数据光盘专用档口，请先在磁盘柜开柜界面完成设置。");
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
                _dialogService.ShowMessage("请先填写年度，再查看该年已有项目。", "年度已有项目");
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
            _pageDb.RunInBackground(RefreshExistingBagHintAsync);
        }

        private void RefreshProjectHint()
        {
            if (string.IsNullOrWhiteSpace(Year) || string.IsNullOrWhiteSpace(ProjectName))
            {
                ProjectHint = "请填写年度和项目。光盘目录不要求包含年度、项目文件夹。";
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
                YearProjectHint = "填写年度后，可查看该年已有项目。";
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

            int count = await _filingService.CountExistingOpticalDiscBagsAsync(ProjectName, Year);
            ExistingBagHint = count <= 0
                ? "该年度/项目尚无电子光盘袋，本次将建立第一袋。"
                : $"该年度/项目已有 {count} 个电子光盘袋，本次将新建立第 {count + 1} 袋。";
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
            int materialCount = PreviewRows.Where(row => row.Include)
                .Select(row => row.MaterialName)
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (materialCount <= 0 || string.IsNullOrWhiteSpace(Year))
            {
                BusinessNumberHint = "确认立档后：每份资料生成一条建档单号，整张光盘生成一个电子袋号。光盘编号与介质袋编号相同。";
                return;
            }

            if (string.IsNullOrWhiteSpace(PreviewElectronicArchiveNo))
            {
                await RefreshPreviewElectronicArchiveNoAsync();
            }

            BusinessNumberHint =
                $"确认立档后将生成 {materialCount} 条建档单号，电子袋号预计为 [{PreviewElectronicArchiveNo}]（确认写入时按当时库内最大号顺延）。光盘编号与介质袋编号相同。";
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
                _dialogService.ShowMessage("仅资料管理员可执行存量光盘直办立档。");
                return;
            }

            if (_scan == null)
            {
                _dialogService.ShowMessage("请先扫描光盘目录。");
                return;
            }

            if (!PreviewRows.Any(row => row.Include))
            {
                _dialogService.ShowMessage(IsExtensionAssignMode
                    ? "请至少纳入一个后缀作为资料子项。"
                    : "请至少纳入一个子项。");
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
                $"即将把这张光盘登记为数据光盘，并按指派结果直接立档。光盘编号与介质袋编号相同，系统不改写光盘目录。\n"
                + $"年度/项目：{Year} / {ProjectName}\n"
                + $"来源：{SourceType}　提供单位：{ProvideUnit}\n"
                + $"介质袋编号：{PreviewElectronicArchiveNo}\n"
                + $"物理位置编号：{StorageLocation}\n"
                + $"{PreviewSummary}\n"
                + $"{ExistingBagHint}\n"
                + $"{BusinessNumberHint}\n\n是否继续？";
            if (!_dialogService.ShowConfirm(confirmText, "确认存量光盘直办立档"))
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

        private async Task ResetForNextFilingAsync()
        {
            _archiveNoPreviewToken++;
            ClearUserInputsForNextRound();
            SourceType = ArchiveRegisterDomainValues.SourceTypeInternal;
            ProvideUnit = ResolveDefaultInternalProvideUnit();
            ArchivePurpose = string.Empty;

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
            ClearUserInputsForNextRound();
            ForceNotifyEditableComboTexts();
        }

        private void ClearUserInputsForNextRound()
        {
            RootPath = string.Empty;
            Year = string.Empty;
            ProjectName = string.Empty;
            ProjectCode = string.Empty;
            ScanWarningText = string.Empty;
            ExistingBagHint = string.Empty;
            _scan = null;
            PreviewRows.Clear();
            ExtensionRows.Clear();
            CanAssignByFolder = false;
            CanAssignByExtension = false;
            _suppressModeRebuild = true;
            try
            {
                AssignMode = AssignModeWholeDisc;
            }
            finally
            {
                _suppressModeRebuild = false;
            }

            IgnoredContentAcknowledged = false;
            ProjectHint = "请先填写年度和项目，或扫描光盘后再填写。";
            YearProjectHint = "填写年度后，可查看该年已有项目。";
            BusinessNumberHint = "确认立档后：每份资料生成一条建档单号，整张光盘生成一个电子袋号。";
            PreviewElectronicArchiveNo = string.Empty;
            _suppressBatchProvide = true;
            try
            {
                BatchSourceType = string.Empty;
                BatchProvideUnit = string.Empty;
                BatchMaterialCategory = string.Empty;
                BatchSubCategory = string.Empty;
                BatchConfidentialLevel = string.Empty;
            }
            finally
            {
                _suppressBatchProvide = false;
            }

            OnPropertyChanged(nameof(PreviewSummary));
            OnPropertyChanged(nameof(HasUnassignedContent));
            OnPropertyChanged(nameof(PageTitle));
            CommandManager.InvalidateRequerySuggested();
        }

        private void ForceNotifyEditableComboTexts()
        {
            OnPropertyChanged(nameof(RootPath));
            OnPropertyChanged(nameof(Year));
            OnPropertyChanged(nameof(ProjectName));
        }

        private StockOpticalDiscDirectFilingRequest BuildRequest()
        {
            var included = PreviewRows.Where(row => row.Include).ToList();
            var materials = included
                .GroupBy(row => row.MaterialName.Trim(), StringComparer.Ordinal)
                .Select(group => new StockOpticalDiscMaterialDraft
                {
                    MaterialName = group.Key,
                    Items = group.Select(row => row.Item).ToList()
                })
                .ToList();

            return new StockOpticalDiscDirectFilingRequest
            {
                RootPath = RootPath,
                Year = Year,
                ProjectName = ProjectName,
                ProjectCode = ProjectCode,
                StorageLocation = StorageLocation,
                SourceType = SourceType?.Trim() ?? string.Empty,
                ArchivePurpose = ArchivePurpose?.Trim() ?? string.Empty,
                ProvideUnit = ProvideUnit?.Trim() ?? string.Empty,
                AssignByFolder = string.Equals(AssignMode, AssignModeFolder, StringComparison.Ordinal),
                AssignByExtension = IsExtensionAssignMode,
                IgnoredContentAcknowledged = IgnoredContentAcknowledged,
                LooseFileCount = string.Equals(AssignMode, AssignModeFolder, StringComparison.Ordinal)
                    ? _scan?.LooseFileCount ?? 0
                    : 0,
                ExcludedFolderCount = string.Equals(AssignMode, AssignModeFolder, StringComparison.Ordinal)
                    ? PreviewRows.Count(row => !row.Include)
                    : 0,
                ExcludedExtensionCount = IsExtensionAssignMode
                    ? ExtensionRows.Count(row => !row.Include)
                    : 0,
                IgnoredFolderCount = 0,
                ExtensionlessFileCount = IsExtensionAssignMode ? _scan?.ExtensionlessFileCount ?? 0 : 0,
                Materials = materials
            };
        }

        private void ApplyProvideUnitForSourceType()
        {
            string internalDefault = ResolveDefaultInternalProvideUnit();
            string next = _provideUnit;
            if (IsExternalSource)
            {
                if (string.Equals(next, internalDefault, StringComparison.Ordinal)
                    || string.Equals(next, ArchiveRegisterDomainValues.ProvideUnitArchiveRoom, StringComparison.Ordinal))
                {
                    next = string.Empty;
                }
            }
            else if (string.IsNullOrWhiteSpace(next))
            {
                next = internalDefault;
            }

            if (string.Equals(_provideUnit, next, StringComparison.Ordinal))
            {
                return;
            }

            _provideUnit = next;
            OnPropertyChanged(nameof(ProvideUnit));
        }

        private void PropagateDefaultSourceProvideUnit(string previousSource, string previousProvide)
        {
            if (_suppressSourceProvidePropagate)
            {
                return;
            }

            foreach (var row in EnumerateAssignmentRows())
            {
                bool sourceMatches = string.Equals(row.SourceType, previousSource, StringComparison.Ordinal);
                bool provideMatches = string.Equals(row.ProvideUnit, previousProvide, StringComparison.Ordinal);
                if (!sourceMatches && !provideMatches)
                {
                    continue;
                }

                row.ApplyPageDefaults(
                    sourceMatches ? SourceType : row.SourceType,
                    provideMatches || sourceMatches ? ProvideUnit : row.ProvideUnit);
            }
        }

        private void PropagateDefaultProvideUnit(string previousProvide)
        {
            if (_suppressSourceProvidePropagate)
            {
                return;
            }

            foreach (var row in EnumerateAssignmentRows())
            {
                if (string.Equals(row.ProvideUnit, previousProvide, StringComparison.Ordinal))
                {
                    row.ProvideUnit = ProvideUnit;
                }
            }
        }

        /// <summary>
        /// 预览行与后缀组织行。同一行只出现一次。
        /// </summary>
        private IEnumerable<StockOpticalDiscPreviewRow> EnumerateAssignmentRows()
        {
            var seen = new HashSet<StockOpticalDiscPreviewRow>();
            foreach (var row in ExtensionRows)
            {
                if (seen.Add(row))
                {
                    yield return row;
                }
            }

            foreach (var row in PreviewRows)
            {
                if (seen.Add(row))
                {
                    yield return row;
                }
            }
        }

        private void LoadDepartments()
        {
            try
            {
                Departments.Clear();
                foreach (Department department in _userService.GetAllDepartments())
                {
                    Departments.Add(department);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
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
    /// 存量光盘扫描预览行。资料名称、子项名称可改；来源、单位、类型、子类、密级按子项填写。
    /// </summary>
    public sealed class StockOpticalDiscPreviewRow : ViewModelBase
    {
        private readonly StockOpticalDiscDirectFilingViewModel _owner;
        private bool _suppressProvideUnitApply;

        public StockOpticalDiscPreviewRow(StockOpticalDiscDirectFilingViewModel owner, StockOpticalDiscItemDraft item)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Item = item ?? throw new ArgumentNullException(nameof(item));
            EnsureDefaultsFromOwner();
        }

        public StockOpticalDiscItemDraft Item { get; }

        public ObservableCollection<string> AvailableSubCategories { get; } = new();

        public bool Include
        {
            get => Item.Include;
            set
            {
                if (Item.Include == value)
                {
                    return;
                }

                Item.Include = value;
                OnPropertyChanged();
                _owner.NotifyPreviewChanged();
            }
        }

        /// <summary>批量改纳入状态，由调用方统一刷新预览。只改勾选，不移除行。</summary>
        internal void SetIncludeWithoutNotify(bool include)
        {
            if (Item.Include == include)
            {
                return;
            }

            Item.Include = include;
            OnPropertyChanged(nameof(Include));
        }

        public string MaterialName
        {
            get => Item.MaterialName;
            set
            {
                string trimmed = value?.Trim() ?? string.Empty;
                if (string.Equals(Item.MaterialName, trimmed, StringComparison.Ordinal))
                {
                    return;
                }

                Item.MaterialName = trimmed;
                RefreshCatalogPath();
                OnPropertyChanged();
                _owner.NotifyPreviewChanged();
            }
        }

        public string ItemName
        {
            get => Item.ItemName;
            set
            {
                string trimmed = value?.Trim() ?? string.Empty;
                if (string.Equals(Item.ItemName, trimmed, StringComparison.Ordinal))
                {
                    return;
                }

                Item.ItemName = trimmed;
                RefreshCatalogPath();
                OnPropertyChanged();
                _owner.NotifyPreviewChanged();
            }
        }

        public string PhysicalPath => Item.StoragePath;

        public string OrganizationForm => Item.DataOrganizationForm;

        public string DataSizeText => ElectronicMediaCapacitySupport.FormatCapacityMb(Item.DataSizeMb);

        public int FileCount => Item.FileCount;

        public string FilingStoragePath => Item.FilingStoragePath;

        public string SourceType
        {
            get => Item.SourceType;
            set
            {
                string trimmed = value?.Trim() ?? string.Empty;
                if (string.Equals(Item.SourceType, trimmed, StringComparison.Ordinal))
                {
                    return;
                }

                Item.SourceType = trimmed;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsExternalSource));
                ApplyProvideUnitForSourceType();
            }
        }

        /// <summary>本子项资料来源是否为「外来」。</summary>
        public bool IsExternalSource =>
            string.Equals(SourceType, ArchiveRegisterDomainValues.SourceTypeExternal, StringComparison.Ordinal);

        public string ProvideUnit
        {
            get => Item.ProvideUnit;
            set
            {
                string trimmed = value ?? string.Empty;
                if (string.Equals(Item.ProvideUnit, trimmed, StringComparison.Ordinal))
                {
                    return;
                }

                Item.ProvideUnit = trimmed;
                OnPropertyChanged();
            }
        }

        public string MaterialCategory
        {
            get => Item.MaterialCategory;
            set
            {
                string trimmed = value?.Trim() ?? string.Empty;
                if (string.Equals(Item.MaterialCategory, trimmed, StringComparison.Ordinal))
                {
                    return;
                }

                Item.MaterialCategory = trimmed;
                OnPropertyChanged();
                RefreshSubCategoryOptions(preferDefaultForDataCategory: false);
            }
        }

        public string SubCategory
        {
            get => Item.SubCategory;
            set
            {
                string trimmed = value?.Trim() ?? string.Empty;
                if (string.Equals(Item.SubCategory, trimmed, StringComparison.Ordinal))
                {
                    return;
                }

                Item.SubCategory = trimmed;
                OnPropertyChanged();
            }
        }

        public string ConfidentialLevel
        {
            get => Item.ConfidentialLevel;
            set
            {
                string trimmed = value?.Trim() ?? string.Empty;
                if (string.Equals(Item.ConfidentialLevel, trimmed, StringComparison.Ordinal))
                {
                    return;
                }

                Item.ConfidentialLevel = trimmed;
                OnPropertyChanged();
            }
        }

        /// <summary>刷新编目路径显示。</summary>
        public void NotifyCatalogPath()
        {
            OnPropertyChanged(nameof(FilingStoragePath));
        }

        /// <summary>按页面域值补齐默认来源、单位、密级、资料类型和子类。</summary>
        public void EnsureDefaultsFromOwner()
        {
            if (string.IsNullOrWhiteSpace(Item.SourceType)
                || (_owner.SourceTypeOptions.Count > 0 && !_owner.SourceTypeOptions.Contains(Item.SourceType)))
            {
                ApplyPageDefaults(_owner.SourceType, _owner.ProvideUnit);
            }
            else if (string.IsNullOrWhiteSpace(Item.ProvideUnit))
            {
                ApplyPageDefaults(Item.SourceType, _owner.ProvideUnit);
            }

            if (string.IsNullOrWhiteSpace(Item.ConfidentialLevel)
                || (_owner.ConfidentialLevelOptions.Count > 0
                    && !_owner.ConfidentialLevelOptions.Contains(Item.ConfidentialLevel)))
            {
                Item.ConfidentialLevel = _owner.ResolveDefaultConfidentialLevel();
                OnPropertyChanged(nameof(ConfidentialLevel));
            }

            if (string.IsNullOrWhiteSpace(Item.MaterialCategory)
                || (_owner.MaterialCategoryOptions.Count > 0
                    && !_owner.MaterialCategoryOptions.Contains(Item.MaterialCategory)))
            {
                Item.MaterialCategory = ArchiveRegisterDomainValues.ElectronicMaterialCategoryData;
                OnPropertyChanged(nameof(MaterialCategory));
            }

            RefreshSubCategoryOptions(preferDefaultForDataCategory: true);
        }

        /// <summary>写入来源和提供单位。</summary>
        public void ApplyPageDefaults(string? sourceType, string? provideUnit)
        {
            _suppressProvideUnitApply = true;
            try
            {
                string nextSource = string.IsNullOrWhiteSpace(sourceType)
                    ? ArchiveRegisterDomainValues.SourceTypeInternal
                    : sourceType.Trim();
                if (!string.Equals(Item.SourceType, nextSource, StringComparison.Ordinal))
                {
                    Item.SourceType = nextSource;
                    OnPropertyChanged(nameof(SourceType));
                    OnPropertyChanged(nameof(IsExternalSource));
                }

                string nextProvide = provideUnit ?? string.Empty;
                if (!IsExternalSource && string.IsNullOrWhiteSpace(nextProvide))
                {
                    nextProvide = _owner.ResolveDefaultInternalProvideUnit();
                }

                if (!string.Equals(Item.ProvideUnit, nextProvide, StringComparison.Ordinal))
                {
                    Item.ProvideUnit = nextProvide;
                    OnPropertyChanged(nameof(ProvideUnit));
                }
            }
            finally
            {
                _suppressProvideUnitApply = false;
            }
        }

        private void RefreshCatalogPath()
        {
            Item.FilingStoragePath = StockOpticalDiscDirectFilingDirectorySupport.BuildCatalogPath(
                _owner.Year,
                _owner.ProjectName,
                Item.MaterialName,
                Item.ItemName);
            OnPropertyChanged(nameof(FilingStoragePath));
        }

        private void ApplyProvideUnitForSourceType()
        {
            if (_suppressProvideUnitApply)
            {
                return;
            }

            string internalDefault = _owner.ResolveDefaultInternalProvideUnit();
            if (IsExternalSource)
            {
                if (string.Equals(ProvideUnit, internalDefault, StringComparison.Ordinal)
                    || string.Equals(ProvideUnit, ArchiveRegisterDomainValues.ProvideUnitArchiveRoom, StringComparison.Ordinal))
                {
                    ProvideUnit = string.Empty;
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(ProvideUnit))
            {
                ProvideUnit = internalDefault;
            }
        }

        private void RefreshSubCategoryOptions(bool preferDefaultForDataCategory)
        {
            IReadOnlyList<string> options = _owner.ResolveSubCategoryOptions(Item.MaterialCategory);
            string capturedSubCategory = Item.SubCategory?.Trim() ?? string.Empty;
            AvailableSubCategories.Clear();
            foreach (string option in options.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.Ordinal))
            {
                AvailableSubCategories.Add(option);
            }

            string preferred = preferDefaultForDataCategory
                && string.Equals(
                    Item.MaterialCategory,
                    ArchiveRegisterDomainValues.ElectronicMaterialCategoryData,
                    StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(capturedSubCategory) || !AvailableSubCategories.Contains(capturedSubCategory))
                    ? ArchiveRegisterDomainValues.DefaultStockDirectSubCategory
                    : capturedSubCategory;

            if (!string.IsNullOrWhiteSpace(preferred) && !AvailableSubCategories.Contains(preferred))
            {
                AvailableSubCategories.Insert(0, preferred);
            }

            string restored = !string.IsNullOrWhiteSpace(preferred) && AvailableSubCategories.Contains(preferred)
                ? preferred
                : AvailableSubCategories.FirstOrDefault() ?? string.Empty;
            if (!string.Equals(Item.SubCategory, restored, StringComparison.Ordinal))
            {
                Item.SubCategory = restored;
                OnPropertyChanged(nameof(SubCategory));
            }
            else if (!string.Equals(SubCategory, restored, StringComparison.Ordinal))
            {
                OnPropertyChanged(nameof(SubCategory));
            }
        }
    }
}
