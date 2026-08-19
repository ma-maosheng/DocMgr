using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.HardDiskMedia;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质编辑弹窗 ViewModel。
    /// </summary>
    public class HardDiskMediumEditDialogViewModel : ViewModelBase
    {
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly ICabinetService _cabinetService;
        private readonly IDialogService _dialogService;
        private readonly IUserContextService _userContextService;
        private readonly HardDiskMedium _sourceMedium;
        private readonly bool _persistOnConfirm;

        private string _diskCode = string.Empty;
        private string _serialNumber = string.Empty;
        private string _diskType = string.Empty;
        private string _brand = string.Empty;
        private string _capacityValue = string.Empty;
        private string _capacityUnit = ElectronicMediaCapacitySupport.DefaultCapacityUnit;
        private string _interfaceType = string.Empty;
        private string _registerPerson = string.Empty;
        private DateTime _registerDate;
        private DateTime? _factoryDate;
        private string _registrationMethod = string.Empty;
        private string _currentLocation = string.Empty;
        private HardDiskMediaReturnTargetLocationOption? _selectedBlankSlotLocationOption;
        private string _currentStatus = HardDiskMedium.StatusBlankInStock;
        private string _mediaNature = HardDiskMedium.NatureBlank;
        private string _currentHolder = "资料室";
        private bool _needReturn;
        private DateTime? _dataCarrierFormedDate;
        private string _dataDescription = string.Empty;
        private string _relatedBatch = string.Empty;
        private string _transferTarget = string.Empty;
        private DateTime? _transferDate;
        private string _remark = string.Empty;
        private bool _isInitialized;
        private bool _hasCommittedChanges;

        public HardDiskMediumEditDialogViewModel(
            IHardDiskMediaService hardDiskMediaService,
            ICabinetService cabinetService,
            IDialogService dialogService,
            IUserContextService userContextService,
            HardDiskMedium mediumToEdit,
            bool persistOnConfirm = true)
        {
            ArgumentNullException.ThrowIfNull(mediumToEdit);

            _hardDiskMediaService = hardDiskMediaService;
            _cabinetService = cabinetService;
            _dialogService = dialogService;
            _userContextService = userContextService;
            _sourceMedium = mediumToEdit;
            _persistOnConfirm = persistOnConfirm;

            Title = mediumToEdit.Id == 0 ? "硬盘参数交互式登记" : "硬盘参数修正";
            GenerateDiskCodeCommand = new RelayCommand(async _ => await GenerateDiskCodeAsync(), _ => IsNewMode);
            ReadLocalDiskCommand = new RelayCommand(async _ => await ReadLocalDiskAsync());
            RecommendBlankSlotLocationCommand = new RelayCommand(async _ => await RecommendBlankSlotLocationAsync(), _ => CanRecommendBlankSlotLocation);
            ShowBlankSlotSnapshotCommand = new RelayCommand(async _ => await ShowBlankSlotSnapshotAsync(), _ => CanShowBlankSlotSnapshot);
            ConfirmCommand = new RelayCommand(async _ => await ConfirmAsync());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public string Title { get; }

        public bool IsNewMode => _sourceMedium.Id == 0;

        public Visibility EditOnlyVisibility => IsNewMode ? Visibility.Collapsed : Visibility.Visible;

        public Visibility NewOnlyVisibility => IsNewMode ? Visibility.Visible : Visibility.Collapsed;

        public ObservableCollection<string> DiskTypeOptions { get; } = new();
        public ObservableCollection<string> BrandOptions { get; } = new();
        public ObservableCollection<string> InterfaceTypeOptions { get; } = new();
        public ObservableCollection<string> CapacityUnitOptions { get; } = new(ElectronicMediaCapacitySupport.CapacityUnits);
        public ObservableCollection<string> StatusOptions { get; } = new();
        public ObservableCollection<string> NatureOptions { get; } = new();
        public ObservableCollection<HardDiskMediaReturnTargetLocationOption> BlankSlotLocationOptions { get; } = new();

        public string BlankSlotLocationHintText =>
            "系统按防磁磁盘柜空白专用档口用途与容量（10盘/档口）推荐可用档口，可使用“推荐档口”和“档口快照”辅助确认。";

        public bool CanRecommendBlankSlotLocation => IsNewMode;

        public bool CanShowBlankSlotSnapshot =>
            IsNewMode && TryParseCabinetLocation(CurrentLocation, out _, out _, out _);

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

        public string InterfaceType
        {
            get => _interfaceType;
            set => SetProperty(ref _interfaceType, value);
        }

        public string RegisterPerson
        {
            get => _registerPerson;
            set => SetProperty(ref _registerPerson, value);
        }

        public DateTime RegisterDate
        {
            get => _registerDate;
            set => SetProperty(ref _registerDate, value);
        }

        public DateTime? FactoryDate
        {
            get => _factoryDate;
            set => SetProperty(ref _factoryDate, value);
        }

        public string RegistrationMethod
        {
            get => _registrationMethod;
            set => SetProperty(ref _registrationMethod, value);
        }

        public string CurrentLocation
        {
            get => _currentLocation;
            set
            {
                if (SetProperty(ref _currentLocation, value))
                {
                    OnPropertyChanged(nameof(CanShowBlankSlotSnapshot));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public HardDiskMediaReturnTargetLocationOption? SelectedBlankSlotLocationOption
        {
            get => _selectedBlankSlotLocationOption;
            set
            {
                if (SetProperty(ref _selectedBlankSlotLocationOption, value))
                {
                    CurrentLocation = value?.Location ?? string.Empty;
                }
            }
        }

        public string CurrentStatus
        {
            get => _currentStatus;
            set => SetProperty(ref _currentStatus, value);
        }

        public string MediaNature
        {
            get => _mediaNature;
            set => SetProperty(ref _mediaNature, value);
        }

        public string CurrentHolder
        {
            get => _currentHolder;
            set => SetProperty(ref _currentHolder, value);
        }

        public bool NeedReturn
        {
            get => _needReturn;
            set => SetProperty(ref _needReturn, value);
        }

        public DateTime? DataCarrierFormedDate
        {
            get => _dataCarrierFormedDate;
            set => SetProperty(ref _dataCarrierFormedDate, value);
        }

        public string DataDescription
        {
            get => _dataDescription;
            set => SetProperty(ref _dataDescription, value);
        }

        public string RelatedBatch
        {
            get => _relatedBatch;
            set => SetProperty(ref _relatedBatch, value);
        }

        public string TransferTarget
        {
            get => _transferTarget;
            set => SetProperty(ref _transferTarget, value);
        }

        public DateTime? TransferDate
        {
            get => _transferDate;
            set => SetProperty(ref _transferDate, value);
        }

        public string Remark
        {
            get => _remark;
            set => SetProperty(ref _remark, value);
        }

        public bool HasCommittedChanges
        {
            get => _hasCommittedChanges;
            private set => SetProperty(ref _hasCommittedChanges, value);
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand GenerateDiskCodeCommand { get; }
        public ICommand ReadLocalDiskCommand { get; }
        public ICommand RecommendBlankSlotLocationCommand { get; }
        public ICommand ShowBlankSlotSnapshotCommand { get; }

        public event Action<bool?>? RequestClose;

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            await LoadOptionsAsync();
            LoadMedium();
            if (IsNewMode)
            {
                await LoadBlankSlotLocationOptionsAsync(autoSelectRecommended: true);
            }

            _isInitialized = true;
        }

        private async Task LoadBlankSlotLocationOptionsAsync(bool autoSelectRecommended)
        {
            BlankSlotLocationOptions.Clear();

            var options = await _hardDiskMediaService.GetOrderedBlankDedicatedSlotLocationOptionsAsync();
            foreach (var option in options)
            {
                BlankSlotLocationOptions.Add(option);
            }

            string? recommended = await _hardDiskMediaService.RecommendBlankDedicatedSlotLocationAsync();
            string normalizedCurrent = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(CurrentLocation);

            if (autoSelectRecommended)
            {
                SelectedBlankSlotLocationOption =
                    BlankSlotLocationOptions.FirstOrDefault(item => string.Equals(item.Location, recommended, StringComparison.OrdinalIgnoreCase))
                    ?? BlankSlotLocationOptions.FirstOrDefault(item => string.Equals(item.Location, normalizedCurrent, StringComparison.OrdinalIgnoreCase))
                    ?? BlankSlotLocationOptions.FirstOrDefault();
            }
            else
            {
                SelectedBlankSlotLocationOption =
                    BlankSlotLocationOptions.FirstOrDefault(item => string.Equals(item.Location, normalizedCurrent, StringComparison.OrdinalIgnoreCase))
                    ?? BlankSlotLocationOptions.FirstOrDefault(item => string.Equals(item.Location, recommended, StringComparison.OrdinalIgnoreCase))
                    ?? BlankSlotLocationOptions.FirstOrDefault();
            }

            CurrentLocation = SelectedBlankSlotLocationOption?.Location ?? normalizedCurrent;
        }

        private async Task RecommendBlankSlotLocationAsync()
        {
            await LoadBlankSlotLocationOptionsAsync(autoSelectRecommended: true);
            if (SelectedBlankSlotLocationOption == null)
            {
                _dialogService.ShowMessage("当前未找到可用的空白硬盘专用档口，请确认防磁磁盘柜档口用途已配置。", "提示");
                return;
            }

            _dialogService.ShowMessage($"已推荐{SelectedBlankSlotLocationOption.DisplayText}", "推荐档口");
        }

        private async Task ShowBlankSlotSnapshotAsync()
        {
            if (!CanShowBlankSlotSnapshot)
            {
                return;
            }

            if (!TryParseCabinetLocation(CurrentLocation, out string cabinetName, out CabinetFace face, out string slotCode))
            {
                _dialogService.ShowMessage("当前档口无法解析，请重新选择后再查看快照。", "提示");
                return;
            }

            var cabinet = (await _cabinetService.GetAllCabinetsAsync())
                .FirstOrDefault(item => item.Type == CabinetType.MagneticDisk && string.Equals(item.Name, cabinetName, StringComparison.OrdinalIgnoreCase));
            if (cabinet == null)
            {
                _dialogService.ShowMessage($"未找到柜号 [{cabinetName}] 对应的防磁磁盘柜。", "提示");
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

        private async Task LoadOptionsAsync()
        {
            var diskTypes = await _hardDiskMediaService.GetDomainOptionLabelsAsync(nameof(HardDiskMedium), nameof(HardDiskMedium.DiskType));
            var brands = await _hardDiskMediaService.GetDomainOptionLabelsAsync(nameof(HardDiskMedium), nameof(HardDiskMedium.Brand));
            var interfaceTypes = await _hardDiskMediaService.GetDomainOptionLabelsAsync(nameof(HardDiskMedium), nameof(HardDiskMedium.InterfaceType));
            var statuses = await _hardDiskMediaService.GetDomainOptionLabelsAsync(nameof(HardDiskLedger), nameof(HardDiskLedger.MediaStatus));
            var natures = await _hardDiskMediaService.GetDomainOptionLabelsAsync(nameof(HardDiskLedger), nameof(HardDiskLedger.MediaNature));

            ResetOptions(DiskTypeOptions, diskTypes);
            ResetOptions(BrandOptions, brands);
            ResetOptions(InterfaceTypeOptions, interfaceTypes);
            ResetOptions(StatusOptions, statuses);
            ResetOptions(NatureOptions, natures);
        }

        private void LoadMedium()
        {
            var ledger = _sourceMedium.Ledger;

            DiskCode = _sourceMedium.DiskCode;
            SerialNumber = _sourceMedium.SerialNumber;
            DiskType = !string.IsNullOrWhiteSpace(_sourceMedium.DiskType) ? _sourceMedium.DiskType : DiskTypeOptions.FirstOrDefault() ?? string.Empty;
            Brand = _sourceMedium.Brand;
            ElectronicMediaCapacitySupport.TrySplitCapacityText(_sourceMedium.Capacity, out var capacityValue, out var capacityUnit);
            CapacityValue = capacityValue;
            CapacityUnit = capacityUnit;
            InterfaceType = !string.IsNullOrWhiteSpace(_sourceMedium.InterfaceType) ? _sourceMedium.InterfaceType : InterfaceTypeOptions.FirstOrDefault() ?? string.Empty;
            EnsureOption(DiskTypeOptions, DiskType);
            EnsureOption(BrandOptions, Brand);
            EnsureOption(InterfaceTypeOptions, InterfaceType);
            RegisterPerson = !string.IsNullOrWhiteSpace(_sourceMedium.RegisterPerson) ? _sourceMedium.RegisterPerson : _userContextService.CurrentUser?.RealName ?? string.Empty;
            RegisterDate = _sourceMedium.RegisterDate == default ? DateTime.Today : _sourceMedium.RegisterDate;
            FactoryDate = _sourceMedium.FactoryDate;
            RegistrationMethod = _sourceMedium.RegistrationMethod;
            CurrentLocation = ledger?.StorageLocation ?? string.Empty;
            CurrentStatus = !string.IsNullOrWhiteSpace(ledger?.MediaStatus) ? ledger.MediaStatus : HardDiskMedium.StatusBlankInStock;
            MediaNature = !string.IsNullOrWhiteSpace(ledger?.MediaNature) ? ledger.MediaNature : HardDiskMedium.NatureBlank;
            CurrentHolder = !string.IsNullOrWhiteSpace(ledger?.HolderOrOrganization) ? ledger.HolderOrOrganization : "资料室";
            NeedReturn = ledger?.NeedReturn ?? false;
            DataCarrierFormedDate = null;
            DataDescription = string.Empty;
            RelatedBatch = string.Empty;
            TransferTarget = string.Empty;
            TransferDate = null;
            Remark = _sourceMedium.Remark;
        }

        private async Task ConfirmAsync()
        {
            if (!TryValidateIdentityCompleteness(out string completenessError))
            {
                _dialogService.ShowMessage(completenessError);
                return;
            }

            try
            {
                HardDiskMedium? registered = await _hardDiskMediaService.FindRegisteredMediumBySerialNumberAsync(
                    _sourceMedium.Id,
                    SerialNumber);
                if (registered != null)
                {
                    _dialogService.ShowMessage(HardDiskMediumSerialRegistrationSupport.BuildAlreadyRegisteredMessage(registered));
                    return;
                }

                var medium = new HardDiskMedium
                {
                    Id = _sourceMedium.Id,
                    DiskCode = DiskCode.Trim(),
                    SerialNumber = SerialNumber.Trim(),
                    DiskType = DiskType.Trim(),
                    Brand = Brand.Trim(),
                    Capacity = ElectronicMediaCapacitySupport.CombineCapacityText(CapacityValue, CapacityUnit),
                    InterfaceType = InterfaceType.Trim(),
                    RegisterPerson = RegisterPerson.Trim(),
                    RegisterDate = RegisterDate,
                    FactoryDate = FactoryDate,
                    RegistrationMethod = RegistrationMethod.Trim(),
                    Ledger = new HardDiskLedger
                    {
                        MediaStatus = CurrentStatus.Trim(),
                        MediaNature = MediaNature.Trim(),
                        StorageLocation = CurrentLocation.Trim(),
                        HolderOrOrganization = CurrentHolder.Trim(),
                        NeedReturn = NeedReturn,
                        RegisterPerson = RegisterPerson.Trim(),
                        RegisterDate = RegisterDate,
                        Remark = Remark.Trim()
                    },
                    Remark = Remark.Trim()
                };

                if (_persistOnConfirm)
                {
                    await _hardDiskMediaService.SaveMediumAsync(medium, _userContextService.CurrentUser);
                }
                else
                {
                    await _hardDiskMediaService.EnsureMediumIdentityDomainOptionsAsync(
                        medium.DiskType,
                        medium.Brand,
                        medium.InterfaceType);
                }

                CopySavedMedium(medium);
                HasCommittedChanges = true;

                string locationSummary = string.IsNullOrWhiteSpace(medium.Ledger?.StorageLocation)
                    ? string.Empty
                    : $"\n存放档口：{medium.Ledger!.StorageLocation}";
                _dialogService.ShowMessage($"硬盘信息已保存。{locationSummary}\n请资料室管理员前往【硬盘台账】核对并完成后续入库业务操作。");
                RequestClose?.Invoke(true);
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            catch (ArgumentException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
        }

        private bool TryValidateIdentityCompleteness(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(DiskCode))
            {
                errorMessage = "请输入硬盘编号。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(SerialNumber))
            {
                errorMessage = "请输入序列号。保存前须以序列号核验是否已登记。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(DiskType))
            {
                errorMessage = "请选择或填写硬盘类型。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Brand))
            {
                errorMessage = "请选择或填写品牌。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(CapacityValue))
            {
                errorMessage = $"序列号 [{SerialNumber.Trim()}] 对应的容量尚未填写，请补全后再保存。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(InterfaceType))
            {
                errorMessage = $"序列号 [{SerialNumber.Trim()}] 对应的接口类型尚未选择或填写，请补全后再保存。";
                return false;
            }

            if (!FactoryDate.HasValue)
            {
                errorMessage = $"序列号 [{SerialNumber.Trim()}] 对应的出厂日期尚未填写，请补全后再保存。";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static void ResetOptions(ObservableCollection<string> target, IReadOnlyList<string> values)
        {
            target.Clear();
            foreach (var value in values)
            {
                target.Add(value);
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

        private async Task GenerateDiskCodeAsync()
        {
            try
            {
                _dialogService.SetBusyState(true);
                DiskCode = await _hardDiskMediaService.GenerateNextDiskCodeAsync();
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                _dialogService.SetBusyState(false);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task ReadLocalDiskAsync()
        {
            LocalPhysicalDiskInfo? selectedDisk;
            try
            {
                selectedDisk = _dialogService.ShowLocalPhysicalDiskPickerDialog();
            }
            catch (InvalidOperationException ex)
            {
                _dialogService.ShowError(ex.Message);
                return;
            }

            if (selectedDisk == null)
            {
                return;
            }

            if (HasFilledHardwareFields()
                && !_dialogService.ShowConfirm("将用本机硬盘信息覆盖当前序列号、品牌、容量、硬盘类型、接口类型和出厂日期（仅当能解析制造年份时），是否继续？"))
            {
                return;
            }

            ApplyLocalDisk(selectedDisk);

            if (string.IsNullOrWhiteSpace(selectedDisk.SerialNumber))
            {
                _dialogService.ShowMessage("已回填可识别字段，但该硬盘未读到序列号，请手工补录后再保存。");
                return;
            }

            HardDiskMedium? registered = await _hardDiskMediaService.FindRegisteredMediumBySerialNumberAsync(
                _sourceMedium.Id,
                selectedDisk.SerialNumber);
            if (registered != null)
            {
                _dialogService.ShowMessage(HardDiskMediumSerialRegistrationSupport.BuildAlreadyRegisteredMessage(registered));
                return;
            }

            string factoryDateSummary = selectedDisk.FactoryDate.HasValue
                ? $"出厂日期已按制造年份回填为 {selectedDisk.FactoryDate:yyyy-MM-dd}。"
                : "出厂日期未能从硬件解析到制造年份，请手工填写。";
            _dialogService.ShowMessage($"已回填本机硬盘信息，请核对后保存。{factoryDateSummary}硬盘编号和存放档口仍需手工确认。");
        }

        private bool HasFilledHardwareFields()
        {
            return !string.IsNullOrWhiteSpace(SerialNumber)
                || !string.IsNullOrWhiteSpace(Brand)
                || !string.IsNullOrWhiteSpace(CapacityValue)
                || FactoryDate.HasValue;
        }

        private void ApplyLocalDisk(LocalPhysicalDiskInfo disk)
        {
            if (!string.IsNullOrWhiteSpace(disk.SerialNumber))
            {
                SerialNumber = disk.SerialNumber;
            }

            if (!string.IsNullOrWhiteSpace(disk.Brand))
            {
                Brand = disk.Brand;
            }
            else if (!string.IsNullOrWhiteSpace(disk.Model) && string.IsNullOrWhiteSpace(Brand))
            {
                Brand = disk.Model.Trim();
            }

            EnsureOption(BrandOptions, Brand);

            if (!string.IsNullOrWhiteSpace(disk.CapacityValue))
            {
                CapacityValue = disk.CapacityValue;
                CapacityUnit = string.IsNullOrWhiteSpace(disk.CapacityUnit)
                    ? ElectronicMediaCapacitySupport.DefaultCapacityUnit
                    : disk.CapacityUnit;
            }

            DiskType = LocalPhysicalDiskHardwareSupport.MatchDomainOption(DiskTypeOptions, disk.DiskType);
            EnsureOption(DiskTypeOptions, DiskType);
            InterfaceType = LocalPhysicalDiskHardwareSupport.MatchDomainOption(InterfaceTypeOptions, disk.InterfaceType);
            EnsureOption(InterfaceTypeOptions, InterfaceType);

            if (disk.FactoryDate.HasValue)
            {
                FactoryDate = disk.FactoryDate;
            }
        }

        private void CopySavedMedium(HardDiskMedium medium)
        {
            _sourceMedium.Id = medium.Id;
            _sourceMedium.DiskCode = medium.DiskCode;
            _sourceMedium.SerialNumber = medium.SerialNumber;
            _sourceMedium.DiskType = medium.DiskType;
            _sourceMedium.Brand = medium.Brand;
            _sourceMedium.Capacity = medium.Capacity;
            _sourceMedium.InterfaceType = medium.InterfaceType;
            _sourceMedium.RegisterPerson = medium.RegisterPerson;
            _sourceMedium.RegisterDate = medium.RegisterDate;
            _sourceMedium.FactoryDate = medium.FactoryDate;
            _sourceMedium.RegistrationMethod = medium.RegistrationMethod;
            _sourceMedium.Remark = medium.Remark;

            if (medium.Ledger != null)
            {
                _sourceMedium.Ledger ??= new HardDiskLedger();
                _sourceMedium.Ledger.DiskCode = medium.DiskCode;
                _sourceMedium.Ledger.MediaStatus = medium.Ledger.MediaStatus;
                _sourceMedium.Ledger.MediaNature = medium.Ledger.MediaNature;
                _sourceMedium.Ledger.StorageLocation = medium.Ledger.StorageLocation;
                _sourceMedium.Ledger.HolderOrOrganization = medium.Ledger.HolderOrOrganization;
                _sourceMedium.Ledger.NeedReturn = medium.Ledger.NeedReturn;
                _sourceMedium.Ledger.RegisterPerson = medium.Ledger.RegisterPerson;
                _sourceMedium.Ledger.RegisterDate = medium.Ledger.RegisterDate;
                _sourceMedium.Ledger.Remark = medium.Ledger.Remark;
            }
        }
    }
}
