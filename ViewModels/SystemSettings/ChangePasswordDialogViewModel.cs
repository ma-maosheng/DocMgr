using System;
using System.Windows.Input;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    /// <summary>
    /// 当前登录用户修改本人密码。
    /// </summary>
    public class ChangePasswordDialogViewModel : ViewModelBase
    {
        private readonly IUserService _userService;
        private readonly IUserContextService _userContextService;
        private readonly IDialogService _dialogService;
        private readonly bool _isMandatory;

        private string _currentPassword = string.Empty;
        private string _newPassword = string.Empty;
        private string _confirmPassword = string.Empty;

        public ChangePasswordDialogViewModel(
            IUserService userService,
            IUserContextService userContextService,
            IDialogService dialogService,
            bool isMandatory)
        {
            _userService = userService;
            _userContextService = userContextService;
            _dialogService = dialogService;
            _isMandatory = isMandatory;

            Title = isMandatory ? "请修改初始密码" : "修改登录密码";
            HintText = isMandatory
                ? $"当前账号须先修改密码后才能进入系统。新密码至少 {PasswordHashingSupport.MinLength} 位，且不能与登录账号相同。"
                : $"新密码至少 {PasswordHashingSupport.MinLength} 位，且不能与登录账号相同。";

            ConfirmCommand = new RelayCommand(_ => Confirm());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke(false));
        }

        public string Title { get; }

        public string HintText { get; }

        public bool IsMandatory => _isMandatory;

        public string CurrentPassword
        {
            get => _currentPassword;
            set => SetProperty(ref _currentPassword, value);
        }

        public string NewPassword
        {
            get => _newPassword;
            set => SetProperty(ref _newPassword, value);
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
        }

        public ICommand ConfirmCommand { get; }

        public ICommand CancelCommand { get; }

        public event Action<bool?>? RequestClose;

        private void Confirm()
        {
            var user = _userContextService.CurrentUser;
            if (user == null)
            {
                _dialogService.ShowError("当前登录已失效，请重新登录。");
                RequestClose?.Invoke(false);
                return;
            }

            if (string.IsNullOrEmpty(CurrentPassword))
            {
                _dialogService.ShowMessage("请输入当前密码。", "提示");
                return;
            }

            if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
            {
                _dialogService.ShowMessage("两次输入的新密码不一致。", "提示");
                return;
            }

            string? policyError = PasswordHashingSupport.ValidatePolicy(NewPassword, user.LoginName);
            if (policyError != null)
            {
                _dialogService.ShowMessage(policyError, "提示");
                return;
            }

            try
            {
                var result = _userService.ChangeOwnPassword(user.Id, CurrentPassword, NewPassword);
                if (!result.IsSuccess)
                {
                    _dialogService.ShowError(result.Message);
                    return;
                }

                user.MustChangePassword = false;
                _dialogService.ShowMessage(result.Message);
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"修改密码失败：{ex.Message}");
            }
        }
    }
}
