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

    internal static NetworkOnNetAsset CreateOnNetAsset(
        NetworkInboundRecord inbound,
        YearlyArchiveRegisterMediaItem mediaItem,
        string assetNo,
        string operatorName,
        DateTime now)
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
