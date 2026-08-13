using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 入网申请档外资料介质规则（与 YA 登记规则存在差异处集中于此）。
/// </summary>
internal static class NetworkInboundRegisterMediaRulesSupport
{
    private static readonly Dictionary<string, string[]> InboundElectronicDispositionRules = new(StringComparer.Ordinal)
    {
        [ArchiveRegisterDomainValues.ElectronicMediaTypeUsbDrive] = [ArchiveRegisterDomainValues.ElectronicDispositionReturn],
        [ArchiveRegisterDomainValues.ElectronicMediaTypeOpticalDisc] = [ArchiveRegisterDomainValues.ElectronicDispositionReturn],
        [ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk] =
        [
            ArchiveRegisterDomainValues.ElectronicDispositionRetain,
            ArchiveRegisterDomainValues.ElectronicDispositionReturn
        ]
    };

    /// <summary>
    /// 档外入网：获取指定电子介质类型允许的处置方式（光盘仅「介质带回」）。
    /// </summary>
    internal static IReadOnlyList<string> GetAllowedElectronicDispositions(
        string? mediaType,
        IReadOnlyCollection<string> allDispositionOptions)
    {
        string normalizedType = mediaType?.Trim() ?? string.Empty;
        if (InboundElectronicDispositionRules.TryGetValue(normalizedType, out string[]? rules))
        {
            return rules;
        }

        return allDispositionOptions?.Count > 0
            ? allDispositionOptions.ToList()
            : Array.Empty<string>();
    }
}
