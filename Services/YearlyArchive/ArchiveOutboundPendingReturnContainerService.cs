using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 待归还出库明细容器状态标记实现。
    /// </summary>
    public sealed class ArchiveOutboundPendingReturnContainerService : IArchiveOutboundPendingReturnContainerService
    {
        private readonly IArchiveOutboundRepository _outboundRepository;

        public ArchiveOutboundPendingReturnContainerService(IArchiveOutboundRepository outboundRepository)
        {
            _outboundRepository = outboundRepository;
        }

        public async Task<int> CountPendingReturnItemsForSimulatedBoxAsync(int boxId)
        {
            if (boxId <= 0)
            {
                return 0;
            }

            var items = await _outboundRepository.GetPendingReturnSimulatedOutboundItemsByBoxIdAsync(boxId);
            return items.Count;
        }

        public async Task<int> CountPendingReturnItemsForSimulatedBoxesAsync(IReadOnlyCollection<int> boxIds)
        {
            if (boxIds == null || boxIds.Count == 0)
            {
                return 0;
            }

            int total = 0;
            foreach (int boxId in boxIds.Where(id => id > 0).Distinct())
            {
                total += await CountPendingReturnItemsForSimulatedBoxAsync(boxId);
            }

            return total;
        }

        public async Task<string?> BuildPendingReturnConfirmMessageAsync(int boxId, string actionLabel)
        {
            int count = await CountPendingReturnItemsForSimulatedBoxAsync(boxId);
            if (count <= 0)
            {
                return null;
            }

            string action = string.IsNullOrWhiteSpace(actionLabel) ? "继续操作" : actionLabel.Trim();
            return $"该档案盒尚有 {count} 条已办结出库、待归还的提档明细。"
                + $"若{action}，将给对应出库明细打上「盒位已变/盒已失效」标记，归还时需按当前盒位或指定目标盒入库。"
                + $"\n\n是否继续{action}？";
        }

        public async Task<string?> BuildPendingReturnConfirmMessageForBoxesAsync(
            IReadOnlyCollection<int> boxIds,
            string actionLabel)
        {
            int count = await CountPendingReturnItemsForSimulatedBoxesAsync(boxIds);
            if (count <= 0)
            {
                return null;
            }

            string action = string.IsNullOrWhiteSpace(actionLabel) ? "继续操作" : actionLabel.Trim();
            return $"所选档口内档案盒合计尚有 {count} 条已办结出库、待归还的提档明细。"
                + $"若{action}，将给对应出库明细打上「盒位已变/盒已失效」标记。"
                + $"\n\n是否继续{action}？";
        }

        public async Task MarkPendingReturnsLocationChangedAsync(
            int boxId,
            string currentContainerCode,
            string currentStorageLocation)
        {
            if (boxId <= 0)
            {
                return;
            }

            var items = await _outboundRepository.GetPendingReturnSimulatedOutboundItemsByBoxIdAsync(boxId);
            if (items.Count == 0)
            {
                return;
            }

            string code = currentContainerCode?.Trim() ?? string.Empty;
            string location = currentStorageLocation?.Trim() ?? string.Empty;
            foreach (var item in items)
            {
                item.ContainerStatusHint = ArchiveOutboundDomainValues.ContainerStatusHintLocationChanged;
                if (!string.IsNullOrWhiteSpace(location))
                {
                    item.CurrentStorageLocation = location;
                }

                if (!string.IsNullOrWhiteSpace(code))
                {
                    // 盒号未变时仍保留出库快照 ContainerCode；当前位置写入 CurrentStorageLocation
                }
            }

            await _outboundRepository.SaveChangesAsync();
        }

        public async Task MarkPendingReturnsBoxInvalidAsync(
            IReadOnlyCollection<int> filingFactIds,
            string? currentContainerCode = null,
            string? currentStorageLocation = null)
        {
            if (filingFactIds == null || filingFactIds.Count == 0)
            {
                return;
            }

            var items = await _outboundRepository.GetPendingReturnSimulatedOutboundItemsByFilingFactIdsAsync(filingFactIds);
            if (items.Count == 0)
            {
                return;
            }

            string code = currentContainerCode?.Trim() ?? string.Empty;
            string location = currentStorageLocation?.Trim() ?? string.Empty;
            foreach (var item in items)
            {
                item.ContainerStatusHint = ArchiveOutboundDomainValues.ContainerStatusHintBoxInvalid;
                if (!string.IsNullOrWhiteSpace(location))
                {
                    item.CurrentStorageLocation = location;
                }
            }

            await _outboundRepository.SaveChangesAsync();
        }
    }
}
