using System.Security.Cryptography;
using System.Text;

namespace DocMgr.Services.SystemSettings
{
    /// <summary>
    /// 登录口令哈希与强度校验。新口令使用带盐 PBKDF2；仍可校验历史无盐 SHA-256 十六进制哈希。
    /// </summary>
    public static class PasswordHashingSupport
    {
        /// <summary>口令最短长度。</summary>
        public const int MinLength = 8;

        private const string Pbkdf2Prefix = "pbkdf2-sha256";
        private const int Pbkdf2Iterations = 100_000;
        private const int SaltSizeBytes = 16;
        private const int KeySizeBytes = 32;
        private const int LegacySha256HexLength = 64;

        /// <summary>
        /// 生成带盐 PBKDF2 存储串。
        /// </summary>
        public static string Hash(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("密码不能为空。", nameof(password));
            }

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Pbkdf2Iterations,
                HashAlgorithmName.SHA256,
                KeySizeBytes);

            return $"{Pbkdf2Prefix}${Pbkdf2Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
        }

        /// <summary>
        /// 校验明文是否与库中存储串匹配（支持 PBKDF2 与历史 SHA-256）。
        /// </summary>
        public static bool Verify(string password, string? storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            string stored = storedHash.Trim();
            if (IsPbkdf2(stored))
            {
                return VerifyPbkdf2(password, stored);
            }

            return VerifyLegacySha256(password, stored);
        }

        /// <summary>
        /// 存储串是否需要升级为当前 PBKDF2 格式。
        /// </summary>
        public static bool NeedsRehash(string? storedHash)
        {
            if (string.IsNullOrWhiteSpace(storedHash))
            {
                return true;
            }

            string stored = storedHash.Trim();
            if (!IsPbkdf2(stored))
            {
                return true;
            }

            string[] parts = stored.Split('$');
            if (parts.Length != 4)
            {
                return true;
            }

            return !int.TryParse(parts[1], out int iterations) || iterations < Pbkdf2Iterations;
        }

        /// <summary>
        /// 校验口令策略。通过返回 null，否则返回中文原因。
        /// </summary>
        public static string? ValidatePolicy(string? password, string? loginName = null)
        {
            if (string.IsNullOrEmpty(password))
            {
                return "密码不能为空。";
            }

            if (password.Length < MinLength)
            {
                return $"密码长度不能少于 {MinLength} 位。";
            }

            string trimmedLogin = (loginName ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(trimmedLogin)
                && string.Equals(password, trimmedLogin, StringComparison.OrdinalIgnoreCase))
            {
                return "密码不能与登录账号相同。";
            }

            return null;
        }

        private static bool IsPbkdf2(string stored)
        {
            return stored.StartsWith(Pbkdf2Prefix + "$", StringComparison.Ordinal);
        }

        private static bool VerifyPbkdf2(string password, string stored)
        {
            string[] parts = stored.Split('$');
            if (parts.Length != 4
                || !string.Equals(parts[0], Pbkdf2Prefix, StringComparison.Ordinal)
                || !int.TryParse(parts[1], out int iterations)
                || iterations <= 0)
            {
                return false;
            }

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            if (salt.Length == 0 || expected.Length == 0)
            {
                return false;
            }

            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        private static bool VerifyLegacySha256(string password, string stored)
        {
            if (stored.Length != LegacySha256HexLength)
            {
                return false;
            }

            string actualHex = ComputeLegacySha256Hex(password);
            byte[] actualBytes = Encoding.UTF8.GetBytes(actualHex);
            byte[] expectedBytes = Encoding.UTF8.GetBytes(stored.ToLowerInvariant());
            if (actualBytes.Length != expectedBytes.Length)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
        }

        private static string ComputeLegacySha256Hex(string password)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
