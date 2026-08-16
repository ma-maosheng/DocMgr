using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出网申请电子介质子项存储目录与服务器完整路径提示。
/// </summary>
public static class NetworkOutboundItemStoragePathSupport
{
    public const string DefaultStoragePathLabel = "存储目录：";

    /// <summary>
    /// 出网子项存储目录生成所需的表头快照。
    /// </summary>
    public sealed class HeaderSnapshot
    {
        public string? Year { get; init; }
        public string? ProjectName { get; init; }
        public string? MaterialName { get; init; }
        public string? MaterialPath { get; init; }
        public string? ServerPhysicalPath { get; init; }
        public string? DestinationKind { get; init; }
        public string? MediaType { get; init; }
        public bool CanEditForm { get; init; }
    }

    public static string ResolveStoragePathLabel(string? mediaType)
    {
        string type = mediaType?.Trim() ?? string.Empty;
        if (string.Equals(type, ArchiveRegisterDomainValues.ElectronicMediaTypeUsbDrive, StringComparison.Ordinal)
            || string.Equals(type, ArchiveRegisterDomainValues.ElectronicMediaTypeOpticalDisc, StringComparison.Ordinal)
            || string.Equals(type, ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk, StringComparison.Ordinal))
        {
            return $"{type}存储目录：";
        }

        return string.IsNullOrWhiteSpace(type) ? DefaultStoragePathLabel : $"{type}存储目录：";
    }

    public static bool IsStoragePathEditable(string? destinationKind, bool canEditForm) =>
        canEditForm && NetworkTransferDomainValues.IsExternalOfflineDestination(destinationKind);

    public static bool ShouldForceGeneratedStoragePath(string? destinationKind) =>
        NetworkTransferDomainValues.IsArchiveFilingDestination(destinationKind);

    /// <summary>
    /// 介质盘内相对目录：年度\项目\资料名称\子项名称。
    /// </summary>
    public static string BuildMediaStoragePath(
        string? year,
        string? projectName,
        string? materialName,
        string? itemName)
    {
        return string.Join(
            "\\",
            NetworkMaterialPathSupport.SanitizePathSegment(
                NetworkMaterialPathSupport.NormalizeYear(year),
                "未知年度"),
            NetworkMaterialPathSupport.SanitizePathSegment(projectName, "未知项目"),
            NetworkMaterialPathSupport.SanitizePathSegment(materialName, "未命名资料"),
            NetworkMaterialPathSupport.SanitizePathSegment(itemName, "未命名子项"));
    }

    /// <summary>
    /// 服务器完整路径提示：物理地址\资料路径\子项名称\
    /// </summary>
    public static string BuildServerFullPathHint(
        string? serverPhysicalPath,
        string? materialPath,
        string? itemName)
    {
        var segments = new List<string>();
        AddPathSegments(segments, serverPhysicalPath);
        AddPathSegments(segments, materialPath);
        AddPathSegments(segments, itemName);
        string combined = string.Join("\\", segments);
        if (string.IsNullOrWhiteSpace(combined))
        {
            return "该子项资料当前所在服务器的完整路径将随服务器路径、资料路径与子项名称自动生成。";
        }

        return $"该子项资料当前所在服务器的完整路径为：{combined}\\";
    }

    public static string ResolveServerPhysicalPath(ServerPathSetting? serverPath, string? fallbackPathName)
    {
        string physical = serverPath?.PhysicalPath?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(physical))
        {
            return physical;
        }

        string pathName = serverPath?.PathName?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(pathName)
            ? fallbackPathName?.Trim() ?? string.Empty
            : pathName;
    }

    public static bool StoragePathsEqual(string? left, string? right)
    {
        return string.Equals(NormalizeComparePath(left), NormalizeComparePath(right), StringComparison.OrdinalIgnoreCase);
    }

    private static void AddPathSegments(List<string> segments, string? value)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        foreach (string part in trimmed.Replace('/', '\\').Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            string sanitized = NetworkMaterialPathSupport.SanitizePathSegment(part, string.Empty);
            if (!string.IsNullOrWhiteSpace(sanitized))
            {
                segments.Add(sanitized);
            }
        }
    }

    private static string NormalizeComparePath(string? value)
    {
        return (value?.Trim() ?? string.Empty).Replace('/', '\\').Trim('\\');
    }
}
