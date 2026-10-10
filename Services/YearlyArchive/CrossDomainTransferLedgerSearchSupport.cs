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

        public static CrossDomainTransferLedgerRow MapInboundCopyRow(
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
                SourceStorageLocation = ResolveArchiveStorageLocation(transaction, fact, item.StorageLocation),
                TargetServerPath = ResolveInboundTargetServerPath(item, asset),
                OnNetAssetNo = asset?.AssetNo?.Trim() ?? string.Empty,
                OperatorName = transaction.OperatorName,
                Summary = transaction.Summary,
                Remark = transaction.Remark
            };
        }

        public static CrossDomainTransferLedgerRow MapOutboundFilingRow(
            YearlyArchiveMaterialTransaction transaction,
            YearlyArchiveFilingFact fact,
            NetworkOutboundRecord outbound)
        {
            string networkPath = ArchiveMaterialTransactionSupport.ResolveOutboundNetworkPath(
                new NetworkOutboundFilingOrigin
                {
                    OutboundRecordId = outbound.Id,
                    OutboundNo = outbound.OutboundNo,
                    ServerPath = outbound.ServerPath,
                    MaterialPath = outbound.MaterialPath
                });
            if (string.IsNullOrWhiteSpace(networkPath))
            {
                networkPath = transaction.BeforeStorageLocation?.Trim() ?? string.Empty;
            }

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
                SourceStorageLocation = ResolveArchiveStorageLocation(transaction, fact, storageHint: null),
                TargetServerPath = networkPath,
                OnNetAssetNo = string.Empty,
                OperatorName = transaction.OperatorName,
                Summary = transaction.Summary,
                Remark = transaction.Remark
            };
        }

        public static string NormalizeKeyword(string? value) => value?.Trim() ?? string.Empty;

        private static string ResolveArchiveStorageLocation(
            YearlyArchiveMaterialTransaction transaction,
            YearlyArchiveFilingFact fact,
            string? storageHint)
        {
            if (!string.IsNullOrWhiteSpace(storageHint))
            {
                return storageHint.Trim();
            }

            if (!string.IsNullOrWhiteSpace(fact.CurrentStorageLocation))
            {
                return fact.CurrentStorageLocation.Trim();
            }

            if (!string.IsNullOrWhiteSpace(transaction.AfterStorageLocation))
            {
                return transaction.AfterStorageLocation.Trim();
            }

            return fact.StorageLocation?.Trim() ?? string.Empty;
        }

        private static string ResolveInboundTargetServerPath(NetworkInboundItem item, NetworkOnNetAsset? asset)
        {
            if (!string.IsNullOrWhiteSpace(asset?.ServerPath))
            {
                return asset.ServerPath.Trim();
            }

            return item.TargetServerPath?.Trim() ?? string.Empty;
        }
    }
}
