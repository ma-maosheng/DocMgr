using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 跨域流转台账：查询行映射与筛选辅助。
    /// </summary>
    internal static class CrossDomainTransferLedgerSearchSupport
    {
        public const int DefaultMaxResults = 5000;

        public static CrossDomainTransferLedgerRow MapRow(
            YearlyArchiveMaterialTransaction transaction,
            YearlyArchiveFilingFact fact,
            NetworkInboundItem item,
            NetworkOnNetAsset? asset)
        {
            return new CrossDomainTransferLedgerRow
            {
                TransactionId = transaction.Id,
                FilingFactId = fact.Id,
                OperatedAt = transaction.OperatedAt,
                TransactionType = transaction.TransactionType,
                BusinessNo = transaction.BusinessNo,
                FilingFactNo = fact.FilingFactNo,
                FormNo = fact.FormNo,
                MediaKind = fact.MediaKind,
                MaterialName = fact.MaterialName,
                ItemName = fact.ItemName,
                ProjectName = fact.ProjectName,
                SourceStorageLocation = ResolveSourceStorageLocation(transaction, fact, item),
                TargetServerPath = ResolveTargetServerPath(item, asset),
                OnNetAssetNo = asset?.AssetNo?.Trim() ?? string.Empty,
                OperatorName = transaction.OperatorName,
                Summary = transaction.Summary,
                Remark = transaction.Remark
            };
        }

        public static string NormalizeKeyword(string? value) => value?.Trim() ?? string.Empty;

        private static string ResolveSourceStorageLocation(
            YearlyArchiveMaterialTransaction transaction,
            YearlyArchiveFilingFact fact,
            NetworkInboundItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.StorageLocation))
            {
                return item.StorageLocation.Trim();
            }

            if (!string.IsNullOrWhiteSpace(fact.CurrentStorageLocation))
            {
                return fact.CurrentStorageLocation.Trim();
            }

            if (!string.IsNullOrWhiteSpace(transaction.BeforeStorageLocation))
            {
                return transaction.BeforeStorageLocation.Trim();
            }

            return fact.StorageLocation?.Trim() ?? string.Empty;
        }

        private static string ResolveTargetServerPath(NetworkInboundItem item, NetworkOnNetAsset? asset)
        {
            if (!string.IsNullOrWhiteSpace(asset?.ServerPath))
            {
                return asset.ServerPath.Trim();
            }

            return item.TargetServerPath?.Trim() ?? string.Empty;
        }
    }
}
