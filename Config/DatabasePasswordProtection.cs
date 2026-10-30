using System.Security.Cryptography;
using System.Text;

namespace DocMgr.Config;

/// <summary>
/// 用 Windows DPAPI（本机范围）保护 SQL 登录密码。
/// 密文拷到其他电脑无法解密；同一台电脑上能读取配置文件的进程仍可解开，因此安装目录仍须限制 NTFS 权限。
/// </summary>
internal static class DatabasePasswordProtection
{
    /// <summary>附加熵只放在程序里，避免通用 DPAPI 工具直接解开；不能代替本机绑定。</summary>
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DocMgr.SqlPassword.v1");

    /// <summary>把密码保护成可写入配置文件的 Base64 密文。</summary>
    public static string Protect(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        byte[] plain = Encoding.UTF8.GetBytes(password);
        try
        {
            byte[] cipher = ProtectedData.Protect(plain, Entropy, DataProtectionScope.LocalMachine);
            return Convert.ToBase64String(cipher);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <summary>解出本机保护的密码。密文无效或来自其他电脑时抛出 <see cref="InvalidOperationException"/>。</summary>
    public static string Unprotect(string protectedPassword)
    {
        if (string.IsNullOrWhiteSpace(protectedPassword))
        {
            throw new InvalidOperationException("appsettings.json 中的 Database:ProtectedPassword 为空。");
        }

        byte[] cipher;
        try
        {
            cipher = Convert.FromBase64String(protectedPassword.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "appsettings.json 中的 Database:ProtectedPassword 不是有效密文。" +
                "请删除该项，在 ConnectionString 中写入 Password=密码后重新启动。",
                ex);
        }

        byte[] plain;
        try
        {
            plain = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.LocalMachine);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                "无法解密本机保存的 SQL 密码。该密文只对保护它的这台电脑有效，从其他电脑复制的配置不能在这里解开。" +
                Environment.NewLine +
                "请删除 ProtectedPassword，在 ConnectionString 中写入 Password=密码后重新启动。",
                ex);
        }

        try
        {
            return Encoding.UTF8.GetString(plain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }
}
