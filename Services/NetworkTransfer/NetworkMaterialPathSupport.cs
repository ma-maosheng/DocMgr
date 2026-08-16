using System.IO;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出入网资料相对路径统一拼装。
/// 部门专用：出网|入网\年度\项目\资料名称；
/// 共用路径：部门\出网|入网\年度\项目\资料名称。
/// 子项名称作为该路径下的子目录，由调用方另行拼接。
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
    /// 按所选服务器路径与申请信息生成相对路径；未选服务器路径时返回空。
    /// </summary>
    public static string BuildMaterialPath(
        ServerPathSetting? serverPath,
        string? applicantDept,
        string? year,
        string? projectName,
        string? materialName,
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
        segments.Add(SanitizePathSegment(materialName, "未命名资料"));
        return string.Join("\\", segments);
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
