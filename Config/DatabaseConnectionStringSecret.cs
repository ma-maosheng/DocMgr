using System.Text;
using Microsoft.Data.SqlClient;

namespace DocMgr.Config;

/// <summary>
/// 从连接串中分离 SQL 密码，避免把整段连接串重排后再写回配置文件。
/// </summary>
internal static class DatabaseConnectionStringSecret
{
    /// <summary>
    /// 读取连接串中的明文密码，并返回去掉 <c>Password</c>/<c>Pwd</c> 后的连接串。
    /// 没有密码时返回 false，<paramref name="connectionStringWithoutPassword"/> 与输入相同。
    /// </summary>
    public static bool TryTakePlaintextPassword(
        string connectionString,
        out string password,
        out string connectionStringWithoutPassword)
    {
        password = string.Empty;
        connectionStringWithoutPassword = connectionString;

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"无法解析数据库连接字符串：{ex.Message}", ex);
        }

        if (string.IsNullOrEmpty(builder.Password))
        {
            return false;
        }

        password = builder.Password;
        connectionStringWithoutPassword = RemovePasswordSegments(connectionString);
        try
        {
            builder = new SqlConnectionStringBuilder(connectionStringWithoutPassword);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"无法解析数据库连接字符串：{ex.Message}", ex);
        }

        if (!string.IsNullOrEmpty(builder.Password))
        {
            throw new InvalidOperationException("无法从连接串中去掉 Password。配置文件未改写。");
        }

        return true;
    }

    /// <summary>
    /// 拼出仅在内存中使用的连接串。密码始终加引号，且不把 Persist Security Info 写进去。
    /// </summary>
    public static string AppendPassword(string connectionStringWithoutPassword, string password)
    {
        string baseString = connectionStringWithoutPassword.Trim().TrimEnd(';');
        return baseString + ";Password=" + QuoteValue(password) + ";";
    }

    private static string RemovePasswordSegments(string connectionString)
    {
        var kept = new List<string>();
        foreach (string segment in SplitSegments(connectionString))
        {
            if (IsPasswordSegment(segment))
            {
                continue;
            }

            kept.Add(segment);
        }

        return string.Join(';', kept);
    }

    private static bool IsPasswordSegment(string segment)
    {
        int separator = segment.IndexOf('=');
        if (separator < 0)
        {
            return false;
        }

        string key = segment[..separator].Trim();
        return key.Equals("Password", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Pwd", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> SplitSegments(string connectionString)
    {
        var segments = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        char quote = '\0';

        for (int index = 0; index < connectionString.Length; index++)
        {
            char currentChar = connectionString[index];
            if (inQuotes)
            {
                current.Append(currentChar);
                if (currentChar != quote)
                {
                    continue;
                }

                if (index + 1 < connectionString.Length && connectionString[index + 1] == quote)
                {
                    current.Append(connectionString[index + 1]);
                    index++;
                    continue;
                }

                inQuotes = false;
                continue;
            }

            if (currentChar is '"' or '\'')
            {
                inQuotes = true;
                quote = currentChar;
                current.Append(currentChar);
                continue;
            }

            if (currentChar == ';')
            {
                segments.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(currentChar);
        }

        if (current.Length > 0)
        {
            segments.Add(current.ToString());
        }

        return segments;
    }

    private static string QuoteValue(string value)
    {
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
