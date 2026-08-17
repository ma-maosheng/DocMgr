using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 在网台账列表展示字段补全（关联出入网登记信息与服务器路径设置）。
/// </summary>
internal static class NetworkOnNetAssetDisplaySupport
{
    internal static void EnrichListItems(
        IReadOnlyList<NetworkOnNetAsset> assets,
        IReadOnlyDictionary<int, NetworkInboundItem> inboundItems,
        IReadOnlyDictionary<int, NetworkInboundRecord> inboundRecords,
        IReadOnlyDictionary<int, NetworkOutboundItem> outboundItems,
        IReadOnlyDictionary<int, NetworkOutboundRecord> outboundRecords,
        IReadOnlyDictionary<string, ServerPathSetting> serverPathsByName)
    {
        foreach (NetworkOnNetAsset asset in assets)
        {
            ClearDisplayFields(asset);

            if (asset.OriginInboundItemId is int inboundItemId
                && inboundItems.TryGetValue(inboundItemId, out NetworkInboundItem? inboundItem))
            {
                NetworkInboundRecord? inbound = inboundItem.InboundRecord
                    ?? (inboundRecords.TryGetValue(inboundItem.InboundRecordId, out NetworkInboundRecord? record)
                        ? record
                        : null);
                if (inbound != null)
                {
                    asset.MaterialPath = inbound.MaterialPath?.Trim() ?? string.Empty;
                    asset.MaterialName = ResolveMaterialName(
                        inbound.MaterialName,
                        inboundItem.MaterialName,
                        inboundItem.ItemName,
                        asset.AssetName);
                    asset.ProvideUnit = inbound.ProvideUnit?.Trim() ?? string.Empty;
                    asset.ApplicantDept = inbound.ApplicantDept?.Trim() ?? string.Empty;
                }
            }
            else if (asset.OriginOutboundItemId is int outboundItemId
                     && outboundItems.TryGetValue(outboundItemId, out NetworkOutboundItem? outboundItem))
            {
                NetworkOutboundRecord? outbound = outboundItem.OutboundRecord
                    ?? (outboundRecords.TryGetValue(outboundItem.OutboundRecordId, out NetworkOutboundRecord? record)
                        ? record
                        : null);
                if (outbound != null)
                {
                    asset.MaterialPath = outbound.MaterialPath?.Trim() ?? string.Empty;
                    asset.MaterialName = ResolveMaterialName(
                        outbound.MaterialName,
                        outboundItem.AssetName,
                        outboundItem.ItemName,
                        asset.AssetName);
                    asset.ApplicantDept = outbound.ApplicantDept?.Trim() ?? string.Empty;
                }
            }

            string serverPath = asset.ServerPath?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(serverPath)
                && serverPathsByName.TryGetValue(serverPath, out ServerPathSetting? setting))
            {
                asset.DepartmentName = setting.DepartmentName?.Trim() ?? string.Empty;
                asset.PhysicalPath = setting.PhysicalPath?.Trim() ?? string.Empty;
            }

            asset.FullStorageAddress = ComposeFullStorageAddress(
                asset.PhysicalPath,
                asset.ServerPath,
                asset.MaterialPath);
        }
    }

    internal static string ComposeFullStorageAddress(
        string? physicalPath,
        string? serverPath,
        string? materialPath)
    {
        var parts = new List<string>(3);
        AddSegment(parts, physicalPath);
        AddSegment(parts, serverPath);
        AddSegment(parts, materialPath);
        return parts.Count == 0 ? string.Empty : string.Join(" · ", parts);
    }

    internal static IReadOnlyList<string> ResolveServerPathNamesForDepartment(
        IReadOnlyList<ServerPathSetting> settings,
        string? departmentName)
    {
        if (string.IsNullOrWhiteSpace(departmentName))
        {
            return [];
        }

        string department = departmentName.Trim();
        return settings
            .Where(item => string.Equals(item.DepartmentName?.Trim(), department, StringComparison.Ordinal))
            .Select(item => item.PathName?.Trim() ?? string.Empty)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void ClearDisplayFields(NetworkOnNetAsset asset)
    {
        asset.MaterialPath = string.Empty;
        asset.MaterialName = string.Empty;
        asset.ProvideUnit = string.Empty;
        asset.ApplicantDept = string.Empty;
        asset.DepartmentName = string.Empty;
        asset.PhysicalPath = string.Empty;
        asset.FullStorageAddress = string.Empty;
    }

    private static string ResolveMaterialName(
        string? headerMaterialName,
        string? itemMaterialName,
        string? itemName,
        string? assetName)
    {
        foreach (string? candidate in new[] { headerMaterialName, itemMaterialName, itemName, assetName })
        {
            string trimmed = candidate?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                return trimmed;
            }
        }

        return string.Empty;
    }

    private static void AddSegment(List<string> parts, string? value)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            parts.Add(trimmed);
        }
    }
}
