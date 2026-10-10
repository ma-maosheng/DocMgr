using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 档外入网登记介质子项 → 在网台账字段映射。
/// </summary>
internal static class NetworkInboundOnNetAssetMappingSupport
{
    internal static IEnumerable<YearlyArchiveRegisterMediaItem> EnumerateElectronicMediaItems(
        IEnumerable<YearlyArchiveRegisterMedia> mediaEntries)
    {
        foreach (YearlyArchiveRegisterMedia media in mediaEntries)
        {
            if (!RegisterMediaTreeSupport.IsElectronicMediaEntity(media))
            {
                continue;
            }

            foreach (YearlyArchiveRegisterMediaItem item in media.Items.OrderBy(row => row.Id))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// 档外入网登记介质子项 → 入网明细快照（办结写台账时挂 <see cref="NetworkOnNetAsset.OriginInboundItemId"/>）。
    /// </summary>
    internal static List<NetworkInboundItem> BuildItemSnapshots(
        NetworkInboundRecord header,
        IEnumerable<YearlyArchiveRegisterMedia>? mediaEntries)
    {
        var result = new List<NetworkInboundItem>();
        int sort = 1;
        DateTime now = DateTime.Now;
        foreach (YearlyArchiveRegisterMediaItem mediaItem in EnumerateElectronicMediaItems(mediaEntries ?? []))
        {
            YearlyArchiveRegisterElectronicMediaItemDetail? detail = mediaItem.ElectronicDetail;
            string dataSizeText = detail == null || detail.DataSizeMb <= 0
                ? string.Empty
                : NetworkInboundItemDisplaySupport.ComposeDataSizeText(
                    detail.DataSizeMb,
                    NetworkInboundItemDisplaySupport.DefaultDataSizeUnit);

            result.Add(new NetworkInboundItem
            {
                SortOrder = sort++,
                AssetKind = ResolveAssetKind(detail?.MaterialCategory, detail?.SubCategory),
                AssetName = header.MaterialName?.Trim() ?? string.Empty,
                ItemName = mediaItem.ContentDesc?.Trim() ?? string.Empty,
                MaterialName = header.MaterialName?.Trim() ?? string.Empty,
                ConfidentialLevel = mediaItem.ConfidentialLevel?.Trim() ?? string.Empty,
                DataSizeText = dataSizeText,
                TargetServerPath = header.TargetServerPath?.Trim() ?? string.Empty,
                SourceKind = header.SourceKind?.Trim() ?? string.Empty,
                CreatedAt = now
            });
        }

        return result;
    }

    internal static NetworkOnNetAsset CreateOnNetAsset(
        NetworkInboundRecord inbound,
        YearlyArchiveRegisterMediaItem mediaItem,
        string assetNo,
        string operatorName,
        DateTime now,
        int? originInboundItemId = null)
    {
        YearlyArchiveRegisterElectronicMediaItemDetail? detail = mediaItem.ElectronicDetail;
        string assetKind = ResolveAssetKind(
            detail?.MaterialCategory,
            detail?.SubCategory);
        string dataSizeText = detail == null
            ? string.Empty
            : NetworkInboundItemDisplaySupport.ComposeDataSizeText(
                detail.DataSizeMb,
                NetworkInboundItemDisplaySupport.DefaultDataSizeUnit);

        return new NetworkOnNetAsset
        {
            AssetNo = assetNo,
            AssetKind = assetKind,
            AssetName = mediaItem.ContentDesc?.Trim() ?? string.Empty,
            ProjectName = inbound.ProjectName?.Trim() ?? string.Empty,
            Year = inbound.Year?.Trim() ?? string.Empty,
            ServerPath = inbound.TargetServerPath?.Trim() ?? string.Empty,
            ConfidentialLevel = mediaItem.ConfidentialLevel?.Trim() ?? string.Empty,
            DataSizeText = dataSizeText,
            OriginKind = NetworkTransferDomainValues.OriginKindInbound,
            OriginInboundItemId = originInboundItemId is > 0 ? originInboundItemId : null,
            LifecycleStatus = NetworkTransferDomainValues.LifecycleOnNet,
            RegisteredBy = operatorName,
            RegisteredAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    internal static string ResolveAssetKind(string? materialCategory, string? subCategory)
    {
        string category = materialCategory?.Trim() ?? string.Empty;
        if (string.Equals(category, ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocument, StringComparison.Ordinal))
        {
            return NetworkTransferDomainValues.AssetKindDocument;
        }

        if (string.Equals(category, ArchiveRegisterDomainValues.ElectronicMaterialCategorySoftware, StringComparison.Ordinal))
        {
            string sub = subCategory?.Trim() ?? string.Empty;
            if (sub.Contains("安全", StringComparison.Ordinal))
            {
                return NetworkTransferDomainValues.AssetKindSecuritySoftware;
            }

            return NetworkTransferDomainValues.AssetKindJobSoftware;
        }

        return NetworkTransferDomainValues.AssetKindJobData;
    }
}
