using DocMgr.Data;
using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 空盒/空袋历史数据纠偏：容器已非在用但立档事实仍标「在库」时，对齐生命周期与当前位置。
    /// </summary>
    public sealed class ArchiveEmptiedContainerLegacyRepairService : IArchiveEmptiedContainerLegacyRepairService
    {
        private readonly AppDbContext _dbContext;
        private readonly IArchiveOutboundRepository _outboundRepository;

        public ArchiveEmptiedContainerLegacyRepairService(
            AppDbContext dbContext,
            IArchiveOutboundRepository outboundRepository)
        {
            _dbContext = dbContext;
            _outboundRepository = outboundRepository;
        }

        public async Task<int> RepairAsync(CancellationToken cancellationToken = default)
        {
            int repaired = 0;
            DateTime now = DateTime.Now;

            var inactiveBoxes = await _dbContext.YearlyArchiveBoxes
                .Where(box => box.ContainerLifecycleStatus != ArchiveContainerLifecycleStatus.InUse)
                .ToListAsync(cancellationToken);

            foreach (var box in inactiveBoxes)
            {
                var rows = await _outboundRepository.GetYearlyArchiveBoxMediaItemRowsForSyncAsync(box);
                foreach (var row in rows)
                {
                    if (!string.Equals(
                            row.Fact.MediaKind,
                            ArchiveRegisterDomainValues.MediaKindSimulated,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (TryRepairFact(row.Fact, row.PendingReturnCopyCount, row.NoReturnCopyCount, row.LostCopyCount, now))
                    {
                        repaired++;
                    }
                }
            }

            var inactiveUnits = await _dbContext.YearlyElectronicArchiveUnits
                .Where(unit => unit.UnitLifecycleStatus != ArchiveContainerLifecycleStatus.InUse)
                .ToListAsync(cancellationToken);

            foreach (var unit in inactiveUnits)
            {
                var rows = await _outboundRepository.GetElectronicArchiveUnitMediaItemRowsForSyncAsync(unit);
                foreach (var row in rows)
                {
                    if (!string.Equals(
                            row.Fact.MediaKind,
                            ArchiveRegisterDomainValues.MediaKindElectronic,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (TryRepairFact(row.Fact, row.PendingReturnCopyCount, row.NoReturnCopyCount, row.LostCopyCount, now))
                    {
                        repaired++;
                    }
                }
            }

            if (repaired > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return repaired;
        }

        private static bool TryRepairFact(
            YearlyArchiveFilingFact fact,
            int pendingReturnCopyCount,
            int noReturnCopyCount,
            int lostCopyCount,
            DateTime operatedAt)
        {
            // 仅纠偏仍误标为在库/借出中的事实；终态保持不动。
            if (!string.Equals(fact.LifecycleStatus, FilingFactLifecycleStatus.InArchive, StringComparison.Ordinal)
                && !string.Equals(fact.LifecycleStatus, FilingFactLifecycleStatus.Borrowed, StringComparison.Ordinal))
            {
                return false;
            }

            var breakdown = ArchiveBoxMediaItemCopyCountSupport.Resolve(
                fact,
                pendingReturnCopyCount,
                noReturnCopyCount,
                lostCopyCount);

            if (breakdown.CurrentInArchiveCopyCount > 0 || breakdown.PendingReturnCopyCount > 0)
            {
                return false;
            }

            string beforeStatus = fact.LifecycleStatus;
            string beforeLocation = fact.CurrentStorageLocation ?? string.Empty;

            ArchiveEmptiedContainerFactLifecycleSupport.ApplyOnContainerEmptied(fact, breakdown, operatedAt);

            // 份数侧无法判定终态时：容器已离柜且无库内/待还，兜底为已转移。
            if (string.Equals(fact.LifecycleStatus, FilingFactLifecycleStatus.InArchive, StringComparison.Ordinal)
                || string.Equals(fact.LifecycleStatus, FilingFactLifecycleStatus.Borrowed, StringComparison.Ordinal))
            {
                fact.LifecycleStatus = FilingFactLifecycleStatus.Transferred;
                fact.CurrentStorageLocation = string.Empty;
                fact.BorrowHintLevel = FilingFactBorrowHintLevel.None;
                fact.BorrowHintText = string.Empty;
                fact.BorrowHintUpdatedAt = operatedAt;
                fact.LifecycleUpdatedAt = operatedAt;
                fact.LifecycleRemark = "空盒/空袋历史纠偏：容器已离柜，立档事实对齐为已转移";
            }

            return !string.Equals(beforeStatus, fact.LifecycleStatus, StringComparison.Ordinal)
                || !string.Equals(beforeLocation, fact.CurrentStorageLocation ?? string.Empty, StringComparison.Ordinal);
        }
    }
}
