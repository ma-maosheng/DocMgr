using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

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
        IReadOnlyDictionary<string, ServerPathSetting> serverPathsByName,
        IReadOnlyDictionary<int, YearlyArchiveFilingFact> filingFacts,
        IReadOnlyList<NetworkOnNetElectronicMediaSnapshot> electronicSnapshots)
    {
        Dictionary<int, NetworkOnNetElectronicMediaSnapshot> snapshotByMediaItemId = electronicSnapshots
            .GroupBy(item => item.MediaItemId)
            .ToDictionary(group => group.Key, group => group.First());
        ILookup<int, NetworkOnNetElectronicMediaSnapshot> snapshotsByInboundRecordId = electronicSnapshots
            .Where(item => item.InboundRecordId is > 0)
            .ToLookup(item => item.InboundRecordId!.Value);
        ILookup<int, NetworkOnNetElectronicMediaSnapshot> snapshotsByOutboundRecordId = electronicSnapshots
            .Where(item => item.OutboundRecordId is > 0)
            .ToLookup(item => item.OutboundRecordId!.Value);

        foreach (NetworkOnNetAsset asset in assets)
        {
            ClearDisplayFields(asset);

            NetworkInboundItem? inboundItem = null;
            NetworkInboundRecord? inbound = null;
            NetworkOutboundItem? outboundItem = null;
            NetworkOutboundRecord? outbound = null;

            if (asset.OriginInboundItemId is int inboundItemId
                && inboundItems.TryGetValue(inboundItemId, out inboundItem))
            {
                inbound = inboundItem.InboundRecord
                    ?? (inboundRecords.TryGetValue(inboundItem.InboundRecordId, out NetworkInboundRecord? record)
                        ? record
                        : null);
                if (inbound != null)
                {
                    asset.ApplicationNo = inbound.InboundNo?.Trim() ?? string.Empty;
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
                     && outboundItems.TryGetValue(outboundItemId, out outboundItem))
            {
                outbound = outboundItem.OutboundRecord
                    ?? (outboundRecords.TryGetValue(outboundItem.OutboundRecordId, out NetworkOutboundRecord? record)
                        ? record
                        : null);
                if (outbound != null)
                {
                    asset.ApplicationNo = outbound.OutboundNo?.Trim() ?? string.Empty;
                    asset.MaterialPath = outbound.MaterialPath?.Trim() ?? string.Empty;
                    asset.MaterialName = ResolveMaterialName(
                        outbound.MaterialName,
                        outboundItem.AssetName,
                        outboundItem.ItemName,
                        asset.AssetName);
                    asset.ApplicantDept = outbound.ApplicantDept?.Trim() ?? string.Empty;
                }
            }

            ApplyElectronicSnapshot(
                asset,
                inboundItem,
                inbound,
                outboundItem,
                outbound,
                filingFacts,
                snapshotByMediaItemId,
                snapshotsByInboundRecordId,
                snapshotsByOutboundRecordId);

            string serverPath = asset.ServerPath?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(serverPath)
                && serverPathsByName.TryGetValue(serverPath, out ServerPathSetting? setting))
            {
                asset.DepartmentName = setting.DepartmentName?.Trim() ?? string.Empty;
                asset.PhysicalPath = setting.PhysicalPath?.Trim() ?? string.Empty;
            }

            asset.LifecycleStatus = NetworkTransferDomainValues.NormalizeLifecycleStatus(asset.LifecycleStatus);
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
        asset.ApplicationNo = string.Empty;
        asset.DataOrganizationForm = string.Empty;
        asset.EntryCountDisplay = string.Empty;
        asset.ElectronicMediaItemId = null;
    }

    private static void ApplyElectronicSnapshot(
        NetworkOnNetAsset asset,
        NetworkInboundItem? inboundItem,
        NetworkInboundRecord? inbound,
        NetworkOutboundItem? outboundItem,
        NetworkOutboundRecord? outbound,
        IReadOnlyDictionary<int, YearlyArchiveFilingFact> filingFacts,
        IReadOnlyDictionary<int, NetworkOnNetElectronicMediaSnapshot> snapshotByMediaItemId,
        ILookup<int, NetworkOnNetElectronicMediaSnapshot> snapshotsByInboundRecordId,
        ILookup<int, NetworkOnNetElectronicMediaSnapshot> snapshotsByOutboundRecordId)
    {
        int? filingFactId = asset.SourceFilingFactId is > 0
            ? asset.SourceFilingFactId
            : inboundItem?.SourceFilingFactId;
        if (filingFactId is > 0
            && filingFacts.TryGetValue(filingFactId.Value, out YearlyArchiveFilingFact? fact)
            && snapshotByMediaItemId.TryGetValue(fact.MediaItemId, out NetworkOnNetElectronicMediaSnapshot? fromFact))
        {
            ApplySnapshot(asset, fromFact);
            return;
        }

        if (inbound != null)
        {
            NetworkOnNetElectronicMediaSnapshot? matched = MatchSnapshot(
                snapshotsByInboundRecordId[inbound.Id],
                asset.AssetName,
                inboundItem?.ItemName,
                inboundItem?.AssetName,
                inboundItem?.MaterialName);
            if (matched != null)
            {
                ApplySnapshot(asset, matched);
                return;
            }
        }

        if (outbound != null)
        {
            NetworkOnNetElectronicMediaSnapshot? matched = MatchSnapshot(
                snapshotsByOutboundRecordId[outbound.Id],
                asset.AssetName,
                outboundItem?.ItemName,
                outboundItem?.AssetName);
            if (matched != null)
            {
                ApplySnapshot(asset, matched);
            }
        }
    }

    private static NetworkOnNetElectronicMediaSnapshot? MatchSnapshot(
        IEnumerable<NetworkOnNetElectronicMediaSnapshot> snapshots,
        params string?[] names)
    {
        HashSet<string> keys = names
            .Select(item => item?.Trim() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.Ordinal);
        List<NetworkOnNetElectronicMediaSnapshot> list = snapshots.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        if (keys.Count == 0)
        {
            return list.Count == 1 ? list[0] : null;
        }

        return list.FirstOrDefault(item => keys.Contains(item.ContentDesc?.Trim() ?? string.Empty));
    }

    private static void ApplySnapshot(NetworkOnNetAsset asset, NetworkOnNetElectronicMediaSnapshot snapshot)
    {
        asset.ElectronicMediaItemId = snapshot.MediaItemId > 0 ? snapshot.MediaItemId : null;
        asset.DataOrganizationForm = snapshot.DataOrganizationForm?.Trim() ?? string.Empty;
        asset.EntryCountDisplay = string.IsNullOrWhiteSpace(asset.DataOrganizationForm) && snapshot.EntryCount <= 0
            ? string.Empty
            : snapshot.EntryCount.ToString();
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
