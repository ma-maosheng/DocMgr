using System;
using System.Windows;
using DocMgr.Views.Shared;

namespace DocMgr.Views.SystemSettings;

/// <summary>
/// 收集 docmgr_app 的登录密码。取消时不返回密码。
/// </summary>
public partial class SqlLoginPasswordDialog : Window
{
    public SqlLoginPasswordDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        TxtTitle.Text = title;
        TxtHint.Text = message;
        EnterAsTabNavigationSupport.Attach(this);
    }

    /// <summary>用户确认后的密码。取消时为空。</summary>
    public string Password { get; private set; } = string.Empty;

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        string password = PwdBox.Password ?? string.Empty;
        string confirm = PwdConfirmBox.Password ?? string.Empty;
        if (password.Length == 0)
        {
            MessageBox.Show(this, "请输入密码。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!string.Equals(password, confirm, StringComparison.Ordinal))
        {
            MessageBox.Show(this, "两次输入的密码不一致。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Password = password;
        DialogResult = true;
    }
}
