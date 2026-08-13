using System.IO;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 入网申请资料路径默认值生成。
/// </summary>
public static class NetworkInboundMaterialPathSupport
{
    /// <summary>
    /// 是否为所有部门共用的服务器路径。
    /// </summary>
    public static bool IsPublicSharedServerPath(ServerPathSetting? serverPath) =>
        string.Equals(
            serverPath?.DepartmentName?.Trim(),
            ServerPathSettingDomainValues.PublicDepartment,
            StringComparison.Ordinal);

    /// <summary>
    /// 生成默认资料路径：共用路径为「部门\年度\项目\档外资料|立档资料\资料名称（单号后缀）」；
    /// 部门专用为「\年度\项目\档外资料|立档资料\资料名称（单号后缀）」。
    /// 档外资料/立档资料段按数据来源区分；单号后缀取入网单号按「-」分割后的后两段，形如「2026-0001」。
    /// </summary>
    public static string BuildDefaultMaterialPath(
        ServerPathSetting? serverPath,
        string? applicantDept,
        string? year,
        string? projectName,
        string? materialName,
        string? inboundNo,
        string? sourceKind)
    {
        string suffixedMaterialName = BuildSuffixedMaterialName(materialName, inboundNo);
        var segments = new List<string>();

        if (IsPublicSharedServerPath(serverPath))
        {
            segments.Add(SanitizePathSegment(applicantDept, "未知部门"));
        }

        segments.Add(SanitizePathSegment(year, "未知年度"));
        segments.Add(SanitizePathSegment(projectName, "未知项目"));
        segments.Add(ResolveSourceKindPathFolder(sourceKind));
        segments.Add(SanitizePathSegment(suffixedMaterialName, "未命名资料"));

        string combined = string.Join("\\", segments);
        return IsPublicSharedServerPath(serverPath)
            ? combined
            : '\\' + combined;
    }

    /// <summary>
    /// 从入网单号提取后两段后缀，如「网-入-申-2026-0001」→「2026-0001」。
    /// </summary>
    public static bool TryExtractInboundNoSuffix(string? inboundNo, out string suffix)
    {
        suffix = string.Empty;
        string trimmed = inboundNo?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        string[] parts = trimmed.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        suffix = $"{parts[^2]}-{parts[^1]}";
        return !string.IsNullOrWhiteSpace(suffix);
    }

    /// <summary>
    /// 按数据来源解析资料路径中的来源文件夹名。
    /// </summary>
    public static string ResolveSourceKindPathFolder(string? sourceKind)
    {
        if (NetworkTransferDomainValues.IsArchivedElectronicSearchSource(sourceKind))
        {
            return "立档资料";
        }

        return "档外资料";
    }

    private static string BuildSuffixedMaterialName(string? materialName, string? inboundNo)
    {
        string name = SanitizePathSegment(materialName, "未命名资料");
        if (!TryExtractInboundNoSuffix(inboundNo, out string suffix))
        {
            suffix = "未知编号";
        }

        return $"{name}（{suffix}）";
    }

    private static string SanitizePathSegment(string? value, string fallback)
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
