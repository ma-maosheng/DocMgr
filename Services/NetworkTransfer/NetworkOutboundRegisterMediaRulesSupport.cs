using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出网申请电子介质规则（目的地决定类型与处置方式约束）。
/// </summary>
internal static class NetworkOutboundRegisterMediaRulesSupport
{
    private static readonly string[] ArchiveFilingElectronicMediaTypes =
    [
        ArchiveRegisterDomainValues.ElectronicMediaTypeOpticalDisc,
        ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk
    ];

    private static readonly string[] ExternalOfflineElectronicMediaTypes =
    [
        ArchiveRegisterDomainValues.ElectronicMediaTypeOpticalDisc,
        ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk,
        ArchiveRegisterDomainValues.ElectronicMediaTypeUsbDrive
    ];

    /// <summary>
    /// 出网：资料室立档仅光盘/硬盘；外部离线另允许 U 盘。
    /// </summary>
    internal static IReadOnlyList<string> GetAllowedElectronicMediaTypes(
        string? destinationKind,
        IReadOnlyCollection<string> allMediaTypeOptions)
    {
        string[] allowedTypes = ResolveAllowedMediaTypes(destinationKind);
        if (allMediaTypeOptions == null || allMediaTypeOptions.Count == 0)
        {
            return allowedTypes.ToList();
        }

        var filtered = allMediaTypeOptions
            .Where(option => allowedTypes.Any(allowed =>
                string.Equals(option?.Trim(), allowed, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (string allowed in allowedTypes)
        {
            if (filtered.Any(option =>
                    string.Equals(option?.Trim(), allowed, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            filtered.Add(allowed);
        }

        return filtered;
    }

    /// <summary>
    /// 出网：资料室立档→介质留存；外部离线（含 U 盘）→介质带走。
    /// </summary>
    internal static IReadOnlyList<string> GetAllowedElectronicDispositions(
        string? destinationKind,
        string? mediaType,
        IReadOnlyCollection<string> allDispositionOptions)
    {
        _ = mediaType;
        string requiredDisposition = ResolveRequiredDisposition(destinationKind);
        if (string.IsNullOrWhiteSpace(requiredDisposition))
        {
            return allDispositionOptions?.Count > 0
                ? allDispositionOptions.ToList()
                : Array.Empty<string>();
        }

        return [requiredDisposition];
    }

    internal static string ResolveRequiredDisposition(string? destinationKind)
    {
        if (NetworkTransferDomainValues.IsArchiveFilingDestination(destinationKind))
        {
            return ArchiveRegisterDomainValues.ElectronicDispositionRetain;
        }

        if (NetworkTransferDomainValues.IsExternalOfflineDestination(destinationKind))
        {
            return NetworkTransferDomainValues.OutboundElectronicDispositionTakeAway;
        }

        return string.Empty;
    }

    internal static bool IsAllowedOutboundElectronicMediaType(string? destinationKind, string? mediaType)
    {
        string[] allowedTypes = ResolveAllowedMediaTypes(destinationKind);
        return allowedTypes.Any(allowed =>
            string.Equals(mediaType?.Trim(), allowed, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsDispositionAllowedForDestination(string? destinationKind, string? disposition)
    {
        string required = ResolveRequiredDisposition(destinationKind);
        if (string.IsNullOrWhiteSpace(required))
        {
            return true;
        }

        if (NetworkTransferDomainValues.IsExternalOfflineDestination(destinationKind))
        {
            return NetworkTransferDomainValues.IsOutboundTakeAwayDisposition(disposition);
        }

        return string.Equals(disposition?.Trim(), required, StringComparison.OrdinalIgnoreCase);
    }

    internal static string FormatAllowedMediaTypesHint(string? destinationKind)
    {
        string[] allowedTypes = ResolveAllowedMediaTypes(destinationKind);
        return string.Join("、", allowedTypes);
    }

    private static string[] ResolveAllowedMediaTypes(string? destinationKind)
    {
        if (NetworkTransferDomainValues.IsExternalOfflineDestination(destinationKind))
        {
            return ExternalOfflineElectronicMediaTypes;
        }

        return ArchiveFilingElectronicMediaTypes;
    }
}
