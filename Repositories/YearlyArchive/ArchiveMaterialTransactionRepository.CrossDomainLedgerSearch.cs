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

            var allowedTypes = ResolveAllowedCrossDomainTypes(criteria.TransactionType);
            var rows = new List<CrossDomainTransferLedgerRow>();

            if (allowedTypes.Contains(MaterialTransactionDomainValues.TypeNetworkInboundCopy))
            {
                rows.AddRange(await SearchInboundCopyLedgerAsync(criteria));
            }

            if (allowedTypes.Contains(MaterialTransactionDomainValues.TypeNetworkOutboundFiling))
            {
                rows.AddRange(await SearchOutboundFilingLedgerAsync(criteria));
            }

            return rows
                .OrderByDescending(item => item.OperatedAt)
                .ThenByDescending(item => item.TransactionId)
                .Take(CrossDomainTransferLedgerSearchSupport.DefaultMaxResults)
                .ToList();
        }

        public async Task<IReadOnlyList<string>> GetCrossDomainTransferBusinessNoOptionsAsync(int maxCount = 50)
        {
            int take = maxCount <= 0 ? 50 : maxCount;

            return await _dbContext.YearlyArchiveMaterialTransactions
                .AsNoTracking()
                .Where(tx => (tx.TransactionType == MaterialTransactionDomainValues.TypeNetworkInboundCopy
                              || tx.TransactionType == MaterialTransactionDomainValues.TypeNetworkOutboundFiling)
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

        public async Task<IReadOnlyDictionary<int, NetworkOutboundFilingOrigin>> GetOutboundFilingOriginsByRegisterRecordIdsAsync(
            IReadOnlyCollection<int> registerRecordIds)
        {
            if (registerRecordIds == null || registerRecordIds.Count == 0)
            {
                return new Dictionary<int, NetworkOutboundFilingOrigin>();
            }

            List<int> ids = registerRecordIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<int, NetworkOutboundFilingOrigin>();
            }

            var rows = await (
                from register in _dbContext.YearlyArchiveRegisterRecords.AsNoTracking()
                where ids.Contains(register.Id)
                      && register.SourceNetworkOutboundRecordId != null
                      && register.SourceNetworkOutboundRecordId > 0
                join outbound in _dbContext.NetworkOutboundRecords.AsNoTracking()
                    on register.SourceNetworkOutboundRecordId equals outbound.Id into outboundGroup
                from outbound in outboundGroup.DefaultIfEmpty()
                select new
                {
                    RegisterRecordId = register.Id,
                    OutboundRecordId = register.SourceNetworkOutboundRecordId!.Value,
                    OutboundNo = outbound != null && outbound.OutboundNo != string.Empty
                        ? outbound.OutboundNo
                        : register.SourceNetworkOutboundNo,
                    ServerPath = outbound != null ? outbound.ServerPath : string.Empty,
                    MaterialPath = outbound != null ? outbound.MaterialPath : string.Empty
                }).ToListAsync();

            return rows.ToDictionary(
                row => row.RegisterRecordId,
                row => new NetworkOutboundFilingOrigin
                {
                    OutboundRecordId = row.OutboundRecordId,
                    OutboundNo = row.OutboundNo?.Trim() ?? string.Empty,
                    ServerPath = row.ServerPath?.Trim() ?? string.Empty,
                    MaterialPath = row.MaterialPath?.Trim() ?? string.Empty
                });
        }

        private async Task<List<CrossDomainTransferLedgerRow>> SearchInboundCopyLedgerAsync(
            CrossDomainTransferLedgerSearchCriteria criteria)
        {
            string keyword = CrossDomainTransferLedgerSearchSupport.NormalizeKeyword(criteria.Keyword);
            string businessNo = CrossDomainTransferLedgerSearchSupport.NormalizeKeyword(criteria.BusinessNo);
            string operatorName = CrossDomainTransferLedgerSearchSupport.NormalizeKeyword(criteria.OperatorName);
            string mediaKind = criteria.MediaKind?.Trim() ?? string.Empty;

            var query =
                from tx in _dbContext.YearlyArchiveMaterialTransactions.AsNoTracking()
                join fact in _dbContext.YearlyArchiveFilingFacts.AsNoTracking() on tx.FilingFactId equals fact.Id
                join item in _dbContext.NetworkInboundItems.AsNoTracking() on tx.SourceId equals item.Id
                join asset in _dbContext.NetworkOnNetAssets.AsNoTracking()
                    on item.OnNetAssetId equals asset.Id into assets
                from asset in assets.DefaultIfEmpty()
                where tx.TransactionType == MaterialTransactionDomainValues.TypeNetworkInboundCopy
                      && tx.SourceKind == MaterialTransactionDomainValues.SourceNetworkInboundItem
                select new { tx, fact, item, asset };

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

            var rows = await query
                .OrderByDescending(item => item.tx.OperatedAt)
                .ThenByDescending(item => item.tx.Id)
                .Take(CrossDomainTransferLedgerSearchSupport.DefaultMaxResults)
                .ToListAsync();

            return rows
                .Select(item => CrossDomainTransferLedgerSearchSupport.MapInboundCopyRow(
                    item.tx,
                    item.fact,
                    item.item,
                    item.asset))
                .ToList();
        }

        private async Task<List<CrossDomainTransferLedgerRow>> SearchOutboundFilingLedgerAsync(
            CrossDomainTransferLedgerSearchCriteria criteria)
        {
            string keyword = CrossDomainTransferLedgerSearchSupport.NormalizeKeyword(criteria.Keyword);
            string businessNo = CrossDomainTransferLedgerSearchSupport.NormalizeKeyword(criteria.BusinessNo);
            string operatorName = CrossDomainTransferLedgerSearchSupport.NormalizeKeyword(criteria.OperatorName);
            string mediaKind = criteria.MediaKind?.Trim() ?? string.Empty;

            var query =
                from tx in _dbContext.YearlyArchiveMaterialTransactions.AsNoTracking()
                join fact in _dbContext.YearlyArchiveFilingFacts.AsNoTracking() on tx.FilingFactId equals fact.Id
                join outbound in _dbContext.NetworkOutboundRecords.AsNoTracking() on tx.SourceId equals outbound.Id
                where tx.TransactionType == MaterialTransactionDomainValues.TypeNetworkOutboundFiling
                      && tx.SourceKind == MaterialTransactionDomainValues.SourceNetworkOutboundRecord
                select new { tx, fact, outbound };

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
                    || item.outbound.OutboundNo.Contains(keyword)
                    || item.outbound.ServerPath.Contains(keyword)
                    || item.outbound.MaterialPath.Contains(keyword)
                    || item.fact.StorageLocation.Contains(keyword)
                    || item.fact.CurrentStorageLocation.Contains(keyword));
            }

            var rows = await query
                .OrderByDescending(item => item.tx.OperatedAt)
                .ThenByDescending(item => item.tx.Id)
                .Take(CrossDomainTransferLedgerSearchSupport.DefaultMaxResults)
                .ToListAsync();

            return rows
                .Select(item => CrossDomainTransferLedgerSearchSupport.MapOutboundFilingRow(
                    item.tx,
                    item.fact,
                    item.outbound))
                .ToList();
        }

        private static HashSet<string> ResolveAllowedCrossDomainTypes(string? transactionType)
        {
            string trimmed = transactionType?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return new HashSet<string>(
                    MaterialTransactionDomainValues.CrossDomainTransferTypes,
                    StringComparer.Ordinal);
            }

            if (MaterialTransactionDomainValues.IsCrossDomainTransferType(trimmed))
            {
                return new HashSet<string>(StringComparer.Ordinal) { trimmed };
            }

            return new HashSet<string>(StringComparer.Ordinal);
        }
    }
}
