using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 出网申请电子介质规则（目的地决定类型与处置方式约束）。
/// </summary>
internal static class NetworkOutboundRegisterMediaRulesSupport
{
    /// <summary>
    /// 资料室存档：出网不定归档载体，建档/立档侧按「内网」拷贝型再选光盘、空盘或并档。
    /// </summary>
    private static readonly string[] ArchiveFilingElectronicMediaTypes =
    [
        ArchiveRegisterDomainValues.ElectronicMediaTypeInnerNetwork
    ];

    private static readonly string[] ExternalOfflineElectronicMediaTypes =
    [
        ArchiveRegisterDomainValues.ElectronicMediaTypeOpticalDisc,
        ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk,
        ArchiveRegisterDomainValues.ElectronicMediaTypeUsbDrive
    ];

    /// <summary>
    /// 出网：资料室存档固定「内网」（归档载体立档时再定）；外部离线允许光盘/硬盘/U 盘。
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

        // 按业务优先级（光盘→硬盘→U盘）排序，勿沿用域值种子顺序（U盘 在首）。
        var ordered = new List<string>();
        foreach (string allowed in allowedTypes)
        {
            string? matched = allMediaTypeOptions.FirstOrDefault(option =>
                string.Equals(option?.Trim(), allowed, StringComparison.OrdinalIgnoreCase));
            string candidate = string.IsNullOrWhiteSpace(matched) ? allowed : matched.Trim();
            if (ordered.Any(option => string.Equals(option, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            ordered.Add(candidate);
        }

        return ordered;
    }

    /// <summary>
    /// 出网：资料室存档→无需处置（载体待立档确定）；外部离线（含 U 盘）→介质带走。
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
            return ArchiveRegisterDomainValues.ElectronicDispositionNone;
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

    /// <summary>
    /// 资料室存档：将电子介质规范为「内网 + 无需处置」，归档载体由立档操作台选择。
    /// </summary>
    internal static void ApplyPendingFilingCarrier(IEnumerable<YearlyArchiveRegisterMedia>? mediaEntries)
    {
        foreach (YearlyArchiveRegisterMedia media in mediaEntries ?? [])
        {
            if (!RegisterMediaTreeSupport.IsElectronicMediaEntity(media))
            {
                continue;
            }

            media.MediaType = ArchiveRegisterDomainValues.ElectronicMediaTypeInnerNetwork;
            media.Disposition = ArchiveRegisterDomainValues.ElectronicDispositionNone;
        }
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
