using System.IO;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出入网资料相对路径统一拼装。
/// 部门专用：出网|入网\年度\项目；
/// 共用路径：部门\出网|入网\年度\项目。
/// 每一份资料名称是该路径下的子目录，子项名称再作为资料名称下的子目录。
/// </summary>
public static class NetworkMaterialPathSupport
{
    public const string OutboundFolderName = "出网";
    public const string InboundFolderName = "入网";

    /// <summary>
    /// 是否为所有部门共用的服务器路径。
    /// </summary>
    public static bool IsPublicSharedServerPath(ServerPathSetting? serverPath) =>
        string.Equals(
            serverPath?.DepartmentName?.Trim(),
            ServerPathSettingDomainValues.PublicDepartment,
            StringComparison.Ordinal);

    /// <summary>
    /// 公共前缀：出网|入网\年度\项目。未选服务器路径时返回空。资料名称不放进这一段。
    /// </summary>
    public static string BuildMaterialPathPrefix(
        ServerPathSetting? serverPath,
        string? applicantDept,
        string? year,
        string? projectName,
        string businessFolderName)
    {
        if (serverPath == null)
        {
            return string.Empty;
        }

        var segments = new List<string>();
        if (IsPublicSharedServerPath(serverPath))
        {
            segments.Add(SanitizePathSegment(applicantDept, "未知部门"));
        }

        segments.Add(SanitizePathSegment(businessFolderName, "未知业务"));
        segments.Add(SanitizePathSegment(NormalizeYear(year), "未知年度"));
        segments.Add(SanitizePathSegment(projectName, "未知项目"));
        return string.Join("\\", segments);
    }

    /// <summary>
    /// 按所选服务器路径与一份资料名称生成相对路径；未选服务器路径时返回空。
    /// </summary>
    public static string BuildMaterialPath(
        ServerPathSetting? serverPath,
        string? applicantDept,
        string? year,
        string? projectName,
        string? materialName,
        string businessFolderName)
    {
        string prefix = BuildMaterialPathPrefix(serverPath, applicantDept, year, projectName, businessFolderName);
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return string.Empty;
        }

        return prefix + "\\" + SanitizePathSegment(materialName, "未命名资料");
    }

    /// <summary>
    /// 规范化年度：空白或「全部」视为未指定。
    /// </summary>
    public static string? NormalizeYear(string? year)
    {
        string trimmed = year?.Trim() ?? string.Empty;
        return string.Equals(trimmed, "全部", StringComparison.Ordinal) ? string.Empty : trimmed;
    }

    /// <summary>
    /// 清洗单个路径段，去掉非法文件名字符。
    /// </summary>
    public static string SanitizePathSegment(string? value, string fallback)
    {
        string segment = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        segment = segment.Replace('/', '\\');

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            segment = segment.Replace(invalidChar, '_');
        }

        segment = segment.Replace(':', '_').Trim('\\', '.', ' ');
        return string.IsNullOrWhiteSpace(segment) ? fallback : segment;
    }
}
