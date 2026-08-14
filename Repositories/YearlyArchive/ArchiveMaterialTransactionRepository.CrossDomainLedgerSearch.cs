using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.YearlyArchive
{
    public sealed partial class ArchiveMaterialTransactionRepository
    {
        public async Task<IReadOnlyList<CrossDomainTransferLedgerRow>> SearchCrossDomainTransferLedgerAsync(
            CrossDomainTransferLedgerSearchCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);

            var query = BuildCrossDomainTransferLedgerQuery(criteria);
            var rows = await query
                .OrderByDescending(item => item.tx.OperatedAt)
                .ThenByDescending(item => item.tx.Id)
                .Take(CrossDomainTransferLedgerSearchSupport.DefaultMaxResults)
                .ToListAsync();

            return rows
                .Select(item => CrossDomainTransferLedgerSearchSupport.MapRow(
                    item.tx,
                    item.fact,
                    item.item,
                    item.asset))
                .ToList();
        }

        private IQueryable<CrossDomainTransferLedgerQueryRow> BuildCrossDomainTransferLedgerQuery(
            CrossDomainTransferLedgerSearchCriteria criteria)
        {
            string keyword = CrossDomainTransferLedgerSearchSupport.NormalizeKeyword(criteria.Keyword);
            string businessNo = CrossDomainTransferLedgerSearchSupport.NormalizeKeyword(criteria.BusinessNo);
            string operatorName = CrossDomainTransferLedgerSearchSupport.NormalizeKeyword(criteria.OperatorName);
            string mediaKind = criteria.MediaKind?.Trim() ?? string.Empty;
            string transactionType = criteria.TransactionType?.Trim() ?? string.Empty;

            var allowedTypes = new[]
            {
                MaterialTransactionDomainValues.TypeNetworkInboundCopy
            };

            var query =
                from tx in _dbContext.YearlyArchiveMaterialTransactions.AsNoTracking()
                join fact in _dbContext.YearlyArchiveFilingFacts.AsNoTracking() on tx.FilingFactId equals fact.Id
                join item in _dbContext.NetworkInboundItems.AsNoTracking() on tx.SourceId equals item.Id
                join asset in _dbContext.NetworkOnNetAssets.AsNoTracking()
                    on item.OnNetAssetId equals asset.Id into assets
                from asset in assets.DefaultIfEmpty()
                where allowedTypes.Contains(tx.TransactionType)
                      && tx.SourceKind == MaterialTransactionDomainValues.SourceNetworkInboundItem
                select new CrossDomainTransferLedgerQueryRow
                {
                    tx = tx,
                    fact = fact,
                    item = item,
                    asset = asset
                };

            if (criteria.OperatedFrom.HasValue)
            {
                DateTime from = criteria.OperatedFrom.Value.Date;
                query = query.Where(item => item.tx.OperatedAt >= from);
            }

            if (criteria.OperatedTo.HasValue)
            {
                DateTime to = criteria.OperatedTo.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(item => item.tx.OperatedAt <= to);
            }

            if (!string.IsNullOrWhiteSpace(transactionType))
            {
                query = query.Where(item => item.tx.TransactionType == transactionType);
            }

            if (!string.IsNullOrWhiteSpace(mediaKind))
            {
                query = query.Where(item => item.fact.MediaKind == mediaKind);
            }

            if (!string.IsNullOrWhiteSpace(businessNo))
            {
                query = query.Where(item => item.tx.BusinessNo.Contains(businessNo));
            }

            if (!string.IsNullOrWhiteSpace(operatorName))
            {
                query = query.Where(item => item.tx.OperatorName.Contains(operatorName));
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(item =>
                    item.fact.FilingFactNo.Contains(keyword)
                    || item.fact.FormNo.Contains(keyword)
                    || item.fact.MaterialName.Contains(keyword)
                    || item.fact.ItemName.Contains(keyword)
                    || item.fact.ProjectName.Contains(keyword)
                    || item.tx.Summary.Contains(keyword)
                    || item.tx.Remark.Contains(keyword)
                    || item.item.TargetServerPath.Contains(keyword)
                    || item.item.StorageLocation.Contains(keyword)
                    || (item.asset != null && item.asset.AssetNo.Contains(keyword))
                    || (item.asset != null && item.asset.ServerPath.Contains(keyword)));
            }

            return query;
        }

        public async Task<IReadOnlyList<string>> GetCrossDomainTransferBusinessNoOptionsAsync(int maxCount = 50)
        {
            int take = maxCount <= 0 ? 50 : maxCount;

            return await _dbContext.YearlyArchiveMaterialTransactions
                .AsNoTracking()
                .Where(tx => tx.TransactionType == MaterialTransactionDomainValues.TypeNetworkInboundCopy
                             && tx.SourceKind == MaterialTransactionDomainValues.SourceNetworkInboundItem
                             && tx.BusinessNo != string.Empty)
                .GroupBy(tx => tx.BusinessNo)
                .Select(group => new
                {
                    BusinessNo = group.Key,
                    LastOperatedAt = group.Max(item => item.OperatedAt)
                })
                .OrderByDescending(item => item.LastOperatedAt)
                .ThenByDescending(item => item.BusinessNo)
                .Take(take)
                .Select(item => item.BusinessNo)
                .ToListAsync();
        }

        private sealed class CrossDomainTransferLedgerQueryRow
        {
            public YearlyArchiveMaterialTransaction tx { get; init; } = null!;

            public YearlyArchiveFilingFact fact { get; init; } = null!;

            public NetworkInboundItem item { get; init; } = null!;

            public NetworkOnNetAsset? asset { get; init; }
        }
    }
}
