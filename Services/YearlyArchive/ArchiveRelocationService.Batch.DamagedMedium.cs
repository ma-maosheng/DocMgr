using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.HardDiskMedia;

namespace DocMgr.Services.YearlyArchive
{
    public sealed partial class ArchiveRelocationService
    {
        public Task<ArchiveRelocationPreview> PreviewBatchDamagedHardDiskSlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            EnsureArchiveAdmin();
            return BuildBatchDamagedHardDiskSlotPreviewAsync(request);
        }

        public async Task<ArchiveRelocationResult> ExecuteBatchDamagedHardDiskSlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            EnsureArchiveAdmin();
            var preview = await BuildBatchDamagedHardDiskSlotPreviewAsync(request);
            if (!preview.CanExecute)
            {
                return ArchiveRelocationResult.Fail(preview.BlockReason);
            }

            await using var transaction = await _relocationRepository.BeginTransactionAsync();
            try
            {
                var sourceMedia = await LoadValidatedSourceDamagedHardDisksForBatchMoveAsync(request);
                string sourceSlotKey = BuildSourceSlotKey(request);
                string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                    request.TargetCabinetName,
                    request.TargetFace,
                    request.TargetRow,
                    request.TargetColumn);
                string targetLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(targetSlotKey);
                DateTime operatedAt = DateTime.Now;
                string operatorName = ResolveOperatorName();
                string remark = $"档口批量搬迁：由 [{sourceSlotKey}] 迁至 [{targetSlotKey}]。";

                foreach (var medium in sourceMedia)
                {
                    RelocateDamagedHardDiskLedger(medium, targetLocation, operatedAt, operatorName, remark, "档口批量搬迁");
                }

                await _relocationRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                return ArchiveRelocationResult.Ok(
                    string.Empty,
                    $"档口批量搬迁完成，共迁移 {sourceMedia.Count} 块损坏硬盘。");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public Task<ArchiveRelocationPreview> PreviewBatchDamagedOpticalDiscSlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            EnsureArchiveAdmin();
            return BuildBatchDamagedOpticalDiscSlotPreviewAsync(request);
        }

        public async Task<ArchiveRelocationResult> ExecuteBatchDamagedOpticalDiscSlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            EnsureArchiveAdmin();
            var preview = await BuildBatchDamagedOpticalDiscSlotPreviewAsync(request);
            if (!preview.CanExecute)
            {
                return ArchiveRelocationResult.Fail(preview.BlockReason);
            }

            await using var transaction = await _relocationRepository.BeginTransactionAsync();
            try
            {
                var sourceMedia = await LoadValidatedSourceDamagedOpticalDiscsForBatchMoveAsync(request);
                string sourceSlotKey = BuildSourceSlotKey(request);
                string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                    request.TargetCabinetName,
                    request.TargetFace,
                    request.TargetRow,
                    request.TargetColumn);
                string targetLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(targetSlotKey);
                DateTime operatedAt = DateTime.Now;
                string operatorName = ResolveOperatorName();
                string remark = $"档口批量搬迁：由 [{sourceSlotKey}] 迁至 [{targetSlotKey}]。";

                foreach (var medium in sourceMedia)
                {
                    RelocateDamagedOpticalDiscLedger(medium, targetLocation, operatedAt, operatorName, remark);
                }

                await _relocationRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                return ArchiveRelocationResult.Ok(
                    string.Empty,
                    $"档口批量搬迁完成，共迁移 {sourceMedia.Count} 张损坏光盘。");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<ArchiveRelocationPreview> BuildBatchDamagedHardDiskSlotPreviewAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            if (!HasCompleteBatchEndpoints(request))
            {
                return Blocked("请提供完整的源档口与目标档口信息。");
            }

            string sourceSlotKey = BuildSourceSlotKey(request);
            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            if (string.Equals(sourceSlotKey, targetSlotKey, StringComparison.OrdinalIgnoreCase))
            {
                return Blocked("源档口与目标档口相同，无需搬迁。");
            }

            List<HardDiskMedium> sourceMedia;
            try
            {
                sourceMedia = await LoadValidatedSourceDamagedHardDisksForBatchMoveAsync(request);
            }
            catch (InvalidOperationException ex)
            {
                return Blocked(ex.Message);
            }

            var targetRequest = new InteractiveItemsPhysicalMoveRequest
            {
                TargetCabinetName = request.TargetCabinetName,
                TargetFace = request.TargetFace,
                TargetRow = request.TargetRow,
                TargetColumn = request.TargetColumn
            };
            string? targetIssue = await ValidateTargetDamagedHardDiskSlotAsync(
                targetRequest,
                sourceMedia.Count,
                excludeMediumIds: sourceMedia.Select(m => m.Id).ToHashSet());
            if (!string.IsNullOrWhiteSpace(targetIssue))
            {
                return Blocked(targetIssue);
            }

            return Ready(
                $"【档口批量物理搬迁】将源档口 [{sourceSlotKey}] 内 {sourceMedia.Count} 块损坏硬盘整体迁至档口 [{targetSlotKey}]；硬盘台账存放位置将同步更新。",
                sourceMedia.Count);
        }

        private async Task<ArchiveRelocationPreview> BuildBatchDamagedOpticalDiscSlotPreviewAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            if (!HasCompleteBatchEndpoints(request))
            {
                return Blocked("请提供完整的源档口与目标档口信息。");
            }

            string sourceSlotKey = BuildSourceSlotKey(request);
            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            if (string.Equals(sourceSlotKey, targetSlotKey, StringComparison.OrdinalIgnoreCase))
            {
                return Blocked("源档口与目标档口相同，无需搬迁。");
            }

            List<OpticalDiscMedium> sourceMedia;
            try
            {
                sourceMedia = await LoadValidatedSourceDamagedOpticalDiscsForBatchMoveAsync(request);
            }
            catch (InvalidOperationException ex)
            {
                return Blocked(ex.Message);
            }

            var targetRequest = new InteractiveItemsPhysicalMoveRequest
            {
                TargetCabinetName = request.TargetCabinetName,
                TargetFace = request.TargetFace,
                TargetRow = request.TargetRow,
                TargetColumn = request.TargetColumn
            };
            string? targetIssue = await ValidateTargetDamagedOpticalDiscSlotAsync(
                targetRequest,
                sourceMedia.Count,
                excludeMediumIds: sourceMedia.Select(m => m.Id).ToHashSet());
            if (!string.IsNullOrWhiteSpace(targetIssue))
            {
                return Blocked(targetIssue);
            }

            return Ready(
                $"【档口批量物理搬迁】将源档口 [{sourceSlotKey}] 内 {sourceMedia.Count} 张损坏光盘整体迁至档口 [{targetSlotKey}]；光盘台账存放位置将同步更新。",
                sourceMedia.Count);
        }

        private async Task<List<HardDiskMedium>> LoadValidatedSourceDamagedHardDisksForBatchMoveAsync(
            BatchSimulatedSlotPhysicalMoveRequest request)
        {
            var sourceCabinet = await _filingRepository.GetMagneticDiskCabinetByNameAsync(request.SourceCabinetName)
                ?? throw new InvalidOperationException($"未找到源防磁磁盘柜 [{request.SourceCabinetName}]。");

            string sourceSlotCode = $"{request.SourceRow}-{request.SourceColumn}";
            string? sourceCategory = await _filingRepository.GetMagneticDiskSlotCategoryNameAsync(
                sourceCabinet.Id,
                request.SourceFace.Trim(),
                sourceSlotCode);
            if (!CabinetHardDiskSlotCategoryAssignment.MatchesCategory(
                    sourceCategory,
                    CabinetHardDiskSlotCategoryAssignment.CategoryDamaged))
            {
                throw new InvalidOperationException("源档口须为损坏硬盘专用档口。");
            }

            string sourceSlotKey = BuildSourceSlotKey(request);
            var media = await _hardDiskMediaRepository.GetInStockDamagedHardDisksInSlotAsync(sourceSlotKey);
            if (media.Count == 0)
            {
                throw new InvalidOperationException("源档口内没有在库损坏硬盘。");
            }

            return media;
        }

        private async Task<List<OpticalDiscMedium>> LoadValidatedSourceDamagedOpticalDiscsForBatchMoveAsync(
            BatchSimulatedSlotPhysicalMoveRequest request)
        {
            var sourceCabinet = await _filingRepository.GetMagneticDiskCabinetByNameAsync(request.SourceCabinetName)
                ?? throw new InvalidOperationException($"未找到源防磁磁盘柜 [{request.SourceCabinetName}]。");

            string sourceSlotCode = $"{request.SourceRow}-{request.SourceColumn}";
            string? sourceCategory = await _filingRepository.GetMagneticDiskSlotCategoryNameAsync(
                sourceCabinet.Id,
                request.SourceFace.Trim(),
                sourceSlotCode);
            if (!CabinetHardDiskSlotCategoryAssignment.MatchesCategory(
                    sourceCategory,
                    CabinetHardDiskSlotCategoryAssignment.CategoryDamagedOpticalDisc))
            {
                throw new InvalidOperationException("源档口须为损坏光盘专用档口。");
            }

            string sourceSlotKey = BuildSourceSlotKey(request);
            var media = await _hardDiskMediaRepository.GetInStockDamagedOpticalDiscsInSlotAsync(sourceSlotKey);
            if (media.Count == 0)
            {
                throw new InvalidOperationException("源档口内没有在库损坏光盘。");
            }

            return media;
        }

        private static bool HasCompleteBatchEndpoints(BatchSimulatedSlotPhysicalMoveRequest request)
            => !string.IsNullOrWhiteSpace(request.SourceCabinetName)
                && !string.IsNullOrWhiteSpace(request.SourceFace)
                && !string.IsNullOrWhiteSpace(request.TargetCabinetName)
                && !string.IsNullOrWhiteSpace(request.TargetFace)
                && request.SourceRow > 0
                && request.SourceColumn > 0
                && request.TargetRow > 0
                && request.TargetColumn > 0;
    }
}
