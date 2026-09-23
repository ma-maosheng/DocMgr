using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    public class BusinessLogicSettingsViewModel : ViewModelBase
    {
        private readonly IBusinessLogicSettingsService _businessLogicSettingsService;
        private readonly IUserContextService _userContextService;
        private readonly IDialogService _dialogService;

        private bool _isInitialized;
        private ApplicationOverdueOption? _selectedApplicationOverdueOption;

        public BusinessLogicSettingsViewModel(
            IBusinessLogicSettingsService businessLogicSettingsService,
            IUserContextService userContextService,
            IDialogService dialogService)
        {
            _businessLogicSettingsService = businessLogicSettingsService;
            _userContextService = userContextService;
            _dialogService = dialogService;
            CanMaintain = ArchiveRegisterBusinessRules.IsSystemAdministrator(_userContextService.CurrentUser);

            ApplicationOverdueOptions = new ObservableCollection<ApplicationOverdueOption>(
                _businessLogicSettingsService.GetApplicationOverdueOptions());

            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanMaintain);
            ResetDefaultCommand = new RelayCommand(_ => ResetDefault(), _ => CanMaintain);
        }

        /// <summary>系统管理员可保存；其余角色仅浏览当前设置。</summary>
        public bool CanMaintain { get; }

        public ObservableCollection<ApplicationOverdueOption> ApplicationOverdueOptions { get; }

        public ApplicationOverdueOption? SelectedApplicationOverdueOption
        {
            get => _selectedApplicationOverdueOption;
            set => SetProperty(ref _selectedApplicationOverdueOption, value);
        }

        public ICommand SaveCommand { get; }

        public ICommand ResetDefaultCommand { get; }

        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            try
            {
                _isInitialized = true;
                await LoadAsync();
            }
            catch (Exception ex)
            {
                _isInitialized = false;
                _dialogService.ShowError($"逾期设置初始化失败：{ex.Message}");
            }
        }

        private async Task LoadAsync()
        {
            string settingCode = await _businessLogicSettingsService.GetApplicationOverdueSettingCodeAsync();
            SelectedApplicationOverdueOption = ApplicationOverdueOptions
                .FirstOrDefault(option => string.Equals(option.Code, settingCode, StringComparison.Ordinal))
                ?? ApplicationOverdueOptions.FirstOrDefault();
        }

        private void ResetDefault()
        {
            if (!EnsureCanMaintain())
            {
                return;
            }

            SelectedApplicationOverdueOption = ApplicationOverdueOptions
                .FirstOrDefault(option => string.Equals(option.Code, ApplicationOverdueDomainValues.Default, StringComparison.Ordinal));
        }

        private async Task SaveAsync()
        {
            if (!EnsureCanMaintain())
            {
                return;
            }

            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                _dialogService.ShowError("当前未登录，无法保存逾期设置。");
                return;
            }

            if (SelectedApplicationOverdueOption == null)
            {
                _dialogService.ShowMessage("请选择申请单逾期设置。");
                return;
            }

            if (!_businessLogicSettingsService.TryValidateApplicationOverdueSetting(
                    SelectedApplicationOverdueOption.Code,
                    out string errorMessage))
            {
                _dialogService.ShowMessage(errorMessage);
                return;
            }

            try
            {
                await _businessLogicSettingsService.SaveApplicationOverdueSettingCodeAsync(
                    SelectedApplicationOverdueOption.Code,
                    user);
                _dialogService.ShowMessage("逾期设置已保存。");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"保存失败：{ex.Message}");
            }
        }

        private bool EnsureCanMaintain()
        {
            if (CanMaintain)
            {
                return true;
            }

            _dialogService.ShowError("仅系统管理员可修改逾期设置。");
            return false;
        }
    }
}
