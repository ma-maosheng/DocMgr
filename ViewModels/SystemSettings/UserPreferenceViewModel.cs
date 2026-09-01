using System;
using System.Threading.Tasks;
using System.Windows.Input;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    public class UserPreferenceViewModel : ViewModelBase
    {
        private readonly IUserPreferenceService _preferenceService;
        private readonly IUserContextService _userContextService;
        private readonly IToDoCenterService _toDoCenterService;
        private readonly IDialogService _dialogService;

        private UserPreference? _preference;
        private bool _isInitialized;

        private bool _enableToDoPopup;
        private bool _enableToDoBadge;
        private int _toDoRefreshSeconds;
        private int _toDoTopN;
        private bool _markAllAsReadOnAcknowledge;

        public UserPreferenceViewModel(
            IUserPreferenceService preferenceService,
            IUserContextService userContextService,
            IToDoCenterService toDoCenterService,
            IDialogService dialogService)
        {
            _preferenceService = preferenceService;
            _userContextService = userContextService;
            _toDoCenterService = toDoCenterService;
            _dialogService = dialogService;

            var defaults = _preferenceService.CreateDefaultTemplate();
            ApplyToView(defaults);

            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            ResetDefaultCommand = new RelayCommand(_ => ResetDefault());
            ChangePasswordCommand = new RelayCommand(_ => ChangePassword());
        }

        public bool EnableToDoPopup
        {
            get => _enableToDoPopup;
            set => SetProperty(ref _enableToDoPopup, value);
        }

        public bool EnableToDoBadge
        {
            get => _enableToDoBadge;
            set => SetProperty(ref _enableToDoBadge, value);
        }

        public int ToDoRefreshSeconds
        {
            get => _toDoRefreshSeconds;
            set => SetProperty(ref _toDoRefreshSeconds, value);
        }

        public int ToDoTopN
        {
            get => _toDoTopN;
            set => SetProperty(ref _toDoTopN, value);
        }

        public bool MarkAllAsReadOnAcknowledge
        {
            get => _markAllAsReadOnAcknowledge;
            set => SetProperty(ref _markAllAsReadOnAcknowledge, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand ResetDefaultCommand { get; }
        public ICommand ChangePasswordCommand { get; }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                _isInitialized = true;
                await LoadAsync();
            }
            catch (Exception ex)
            {
                _isInitialized = false;
                _dialogService.ShowError($"偏好初始化失败：{ex.Message}");
            }
        }

        private async Task LoadAsync()
        {
            var preference = await EnsurePreferenceAsync();
            if (preference == null) return;

            ApplyToView(preference);
        }

        private async Task<UserPreference?> EnsurePreferenceAsync()
        {
            if (_preference != null) return _preference;

            var user = _userContextService.CurrentUser;
            if (user == null) return null;

            _preference = await _preferenceService.GetOrCreateAsync(user.Id);
            return _preference;
        }

        private void ResetDefault()
        {
            var defaults = _preferenceService.CreateDefaultTemplate();
            ApplyToView(defaults);
        }

        private void ChangePassword()
        {
            if (_userContextService.CurrentUser == null)
            {
                _dialogService.ShowError("当前登录已失效，请重新登录。");
                return;
            }

            _dialogService.ShowChangePasswordDialog();
        }

        private async Task SaveAsync()
        {
            var preference = await EnsurePreferenceAsync();
            if (preference == null)
            {
                _dialogService.ShowError("偏好未初始化。请重新登录后重试。");
                return;
            }

            ApplyToEntity(preference);

            if (!_preferenceService.TryValidate(preference, out var errorMessage))
            {
                _dialogService.ShowMessage(errorMessage);
                return;
            }

            await _preferenceService.SaveAsync(preference);
            await _toDoCenterService.ApplyPreferenceAsync(preference);

            _dialogService.ShowMessage("个人偏好已保存。");
        }

        private void ApplyToView(UserPreference source)
        {
            EnableToDoPopup = source.EnableToDoPopup;
            EnableToDoBadge = source.EnableToDoBadge;
            ToDoRefreshSeconds = source.ToDoRefreshSeconds;
            ToDoTopN = source.ToDoTopN;
            MarkAllAsReadOnAcknowledge = source.MarkAllAsReadOnAcknowledge;
        }

        private void ApplyToEntity(UserPreference target)
        {
            target.EnableToDoPopup = EnableToDoPopup;
            target.EnableToDoBadge = EnableToDoBadge;
            target.ToDoRefreshSeconds = ToDoRefreshSeconds;
            target.ToDoTopN = ToDoTopN;
            target.MarkAllAsReadOnAcknowledge = MarkAllAsReadOnAcknowledge;
        }
    }
}