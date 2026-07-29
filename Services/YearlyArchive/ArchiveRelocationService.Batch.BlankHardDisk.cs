using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.HardDiskMedia;

namespace DocMgr.Services.YearlyArchive
{
    public sealed partial class ArchiveRelocationService
    {
        public Task<ArchiveRelocationPreview> PreviewBatchBlankHardDiskSlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            EnsureArchiveAdmin();
            return BuildBatchBlankHardDiskSlotPreviewAsync(request);
        }

        public Task<string?> GetBatchBlankHardDiskPendingReturnConfirmMessageAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            EnsureArchiveAdmin();
            return BuildBatchBlankHardDiskPendingReturnConfirmMessageAsync(request);
        }

        public async Task<ArchiveRelocationResult> ExecuteBatchBlankHardDiskSlotPhysicalMoveAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            EnsureArchiveAdmin();
            var preview = await BuildBatchBlankHardDiskSlotPreviewAsync(request);
            if (!preview.CanExecute)
            {
                return ArchiveRelocationResult.Fail(preview.BlockReason);
            }

            await using var transaction = await _relocationRepository.BeginTransactionAsync();
            try
            {
                var sourceMedia = await LoadValidatedSourceBlankHardDisksForBatchMoveAsync(request);
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
                    RelocateBlankHardDiskLedger(medium, targetLocation, operatedAt, operatorName, remark, "档口批量搬迁");
                }

                int pendingReturnCount = 0;
                if (request.IncludePendingReturnBlankHardDisks)
                {
                    pendingReturnCount = await RelocatePendingReturnBlankHardDiskSlotReferencesAsync(
                        request,
                        sourceSlotKey,
                        targetSlotKey,
                        operatedAt,
                        remark);
                }

                await _relocationRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                string message = pendingReturnCount > 0
                    ? $"档口批量搬迁完成，共迁移 {sourceMedia.Count} 块在库空白硬盘，并同步更新 {pendingReturnCount} 块待归还硬盘的归属档口。"
                    : $"档口批量搬迁完成，共迁移 {sourceMedia.Count} 块空白硬盘。";

                return ArchiveRelocationResult.Ok(string.Empty, message);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<ArchiveRelocationPreview> BuildBatchBlankHardDiskSlotPreviewAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SourceCabinetName)
                || string.IsNullOrWhiteSpace(request.SourceFace)
                || string.IsNullOrWhiteSpace(request.TargetCabinetName)
                || string.IsNullOrWhiteSpace(request.TargetFace)
                || request.SourceRow <= 0
                || request.SourceColumn <= 0
                || request.TargetRow <= 0
                || request.TargetColumn <= 0)
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
                sourceMedia = await LoadValidatedSourceBlankHardDisksForBatchMoveAsync(request);
            }
            catch (InvalidOperationException ex)
            {
                return Blocked(ex.Message);
            }

            string? targetIssue = await ValidateTargetBlankHardDiskSlotForBatchMoveAsync(request, sourceMedia.Count);
            if (!string.IsNullOrWhiteSpace(targetIssue))
            {
                return Blocked(targetIssue);
            }

            return Ready(
                $"【档口批量物理搬迁】将源档口 [{sourceSlotKey}] 内 {sourceMedia.Count} 块在库空白硬盘整体迁至档口 [{targetSlotKey}]；硬盘台账存放位置将同步更新。",
                sourceMedia.Count);
        }

        private async Task<string?> BuildBatchBlankHardDiskPendingReturnConfirmMessageAsync(BatchSimulatedSlotPhysicalMoveRequest request)
        {
            string sourceSlotKey = BuildSourceSlotKey(request);
            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            int pendingReturnCount = await _hardDiskMediaRepository.CountPendingReturnBlankHardDisksInSlotAsync(sourceSlotKey);
            if (pendingReturnCount <= 0)
            {
                return null;
            }

            return $"源档口 [{sourceSlotKey}] 尚有 {pendingReturnCount} 块待归还空白硬盘（借出未还）。\n\n"
                + $"是否将这些待归还硬盘的归属档口一并迁至 [{targetSlotKey}]？\n"
                + "选择「是」：开柜界面中这些硬盘将随源档口一并迁出，归还登记时将入位至目标档口。\n"
                + "选择「否」：仅迁移在库空白硬盘，待归还硬盘仍关联原档口。";
        }

        private async Task<int> RelocatePendingReturnBlankHardDiskSlotReferencesAsync(
            BatchSimulatedSlotPhysicalMoveRequest request,
            string sourceSlotKey,
            string targetSlotKey,
            DateTime operatedAt,
            string remark)
        {
            string normalizedSourceSlotKey = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(sourceSlotKey);
            string normalizedTargetSlotKey = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(targetSlotKey);
            var pendingMedia = await _hardDiskMediaRepository.LoadPendingReturnBlankHardDisksInSlotForRelocationAsync(sourceSlotKey);
            if (pendingMedia.Count == 0)
            {
                return 0;
            }

            var mediumIds = pendingMedia.Select(item => item.Id).ToList();
            var outboundApplications = await _hardDiskMediaRepository.GetCompletedOutboundApplicationsByMediumIdsAsync(mediumIds);
            var outboundApplicationsByMediumId = outboundApplications
                .GroupBy(item => item.MediumId)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderByDescending(item => item.ExecutedTime ?? item.UpdatedTime).ThenByDescending(item => item.Id).First());

            int updatedCount = 0;
            foreach (var medium in pendingMedia)
            {
                bool updated = UpdatePendingReturnBlankHardDiskSlotReference(
                    medium,
                    normalizedSourceSlotKey,
                    normalizedTargetSlotKey,
                    outboundApplicationsByMediumId,
                    operatedAt);

                var activeReturnRegistration = await _hardDiskMediaRepository.GetActiveReturnRegistrationByMediumIdForUpdateAsync(medium.Id);
                if (activeReturnRegistration != null
                    && string.Equals(activeReturnRegistration.ApplicationType, HardDiskMediaApplication.TypeReturnBlankRegistration, StringComparison.Ordinal)
                    && string.Equals(
                        HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(activeReturnRegistration.TargetLocation),
                        normalizedSourceSlotKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    activeReturnRegistration.TargetLocation = normalizedTargetSlotKey;
                    activeReturnRegistration.UpdatedTime = operatedAt;
                    updated = true;
                }

                if (updated)
                {
                    updatedCount++;
                }
            }

            return updatedCount;
        }

        private static bool UpdatePendingReturnBlankHardDiskSlotReference(
            HardDiskMedium medium,
            string normalizedSourceSlotKey,
            string normalizedTargetSlotKey,
            IReadOnlyDictionary<int, HardDiskMediaApplication> outboundApplicationsByMediumId,
            DateTime operatedAt)
        {
            bool updated = false;
            var latestTransaction = medium.Transactions
                .OrderByDescending(item => item.OperateTime)
                .ThenByDescending(item => item.Id)
                .FirstOrDefault();
            if (latestTransaction != null
                && string.Equals(
                    HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(latestTransaction.BeforeLocation),
                    normalizedSourceSlotKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                latestTransaction.BeforeLocation = normalizedTargetSlotKey;
                updated = true;
            }

            if (outboundApplicationsByMediumId.TryGetValue(medium.Id, out var outboundApplication)
                && string.Equals(
                    HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(outboundApplication.CurrentLocation),
                    normalizedSourceSlotKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                outboundApplication.CurrentLocation = normalizedTargetSlotKey;
                outboundApplication.UpdatedTime = operatedAt;
                updated = true;
            }

            return updated;
        }

        private async Task<List<HardDiskMedium>> LoadValidatedSourceBlankHardDisksForBatchMoveAsync(
            BatchSimulatedSlotPhysicalMoveRequest request)
        {
            var sourceCabinet = await _filingRepository.GetMagneticDiskCabinetByNameAsync(request.SourceCabinetName)
                ?? throw new InvalidOperationException($"未找到源防磁磁盘柜 [{request.SourceCabinetName}]。");

            string sourceSlotCode = $"{request.SourceRow}-{request.SourceColumn}";
            string? sourceCategory = await _filingRepository.GetMagneticDiskSlotCategoryNameAsync(
                sourceCabinet.Id,
                request.SourceFace.Trim(),
                sourceSlotCode);
            string normalizedSourceCategory = CabinetHardDiskSlotCategoryAssignment.NormalizeCategoryName(sourceCategory);
            if (!IsBlankHardDiskMagneticDiskSlotCategory(normalizedSourceCategory))
            {
                throw new InvalidOperationException("源档口须为空白硬盘专用档口。");
            }

            string sourceSlotKey = BuildSourceSlotKey(request);
            var media = await _hardDiskMediaRepository.GetInStockBlankHardDisksInSlotAsync(sourceSlotKey);
            if (media.Count == 0)
            {
                throw new InvalidOperationException("源档口内没有在库空白硬盘。");
            }

            return media;
        }

        private async Task<string?> ValidateTargetBlankHardDiskSlotForBatchMoveAsync(
            BatchSimulatedSlotPhysicalMoveRequest request,
            int incomingCount)
        {
            var targetCabinet = await _filingRepository.GetMagneticDiskCabinetByNameAsync(request.TargetCabinetName);
            if (targetCabinet == null)
            {
                return $"未找到目标防磁磁盘柜 [{request.TargetCabinetName}]。";
            }

            string targetSlotCode = $"{request.TargetRow}-{request.TargetColumn}";
            string? targetCategory = await _filingRepository.GetMagneticDiskSlotCategoryNameAsync(
                targetCabinet.Id,
                request.TargetFace.Trim(),
                targetSlotCode);
            string normalizedTargetCategory = CabinetHardDiskSlotCategoryAssignment.NormalizeCategoryName(targetCategory);
            if (!IsBlankHardDiskMagneticDiskSlotCategory(normalizedTargetCategory))
            {
                return "目标档口须为空白硬盘专用档口。";
            }

            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);

            var electronicUnits = await _relocationRepository.GetInUseElectronicArchiveUnitsInSlotForRelocationAsync(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            if (electronicUnits.Count > 0)
            {
                return "目标档口已有电子介质袋占用，请选择仅存放空白硬盘的档口。";
            }

            var opticalLocations = await _hardDiskMediaRepository.GetInStockOpticalDiscStorageLocationsInSlotAsync(targetSlotKey);
            if (opticalLocations.Count > 0)
            {
                return "目标档口已有光盘占用，请选择仅存放空白硬盘的档口。";
            }

            var allHardDiskLocations = await _hardDiskMediaRepository.GetInStockHardDiskStorageLocationsInSlotAsync(targetSlotKey);
            // 目标档口物理占用须含征用锁盘；否则在库被征用空白盘会被误判为「非空白占用」。
            var targetBlankMedia = await _hardDiskMediaRepository.GetInStockBlankHardDisksInSlotAsync(
                targetSlotKey,
                unlockedOnly: false);
            if (allHardDiskLocations.Count != targetBlankMedia.Count)
            {
                return "目标档口存在非空白硬盘占用，请选择仅存放空白硬盘的档口。";
            }

            int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(
                CabinetHardDiskSlotCategoryAssignment.CategoryBlank);
            if (targetBlankMedia.Count + incomingCount > slotCapacity)
            {
                return $"目标档口容量不足（迁入后需 {targetBlankMedia.Count + incomingCount} 盘，档口容量 {slotCapacity} 盘）。";
            }

            return null;
        }

        private void RelocateBlankHardDiskLedger(
            HardDiskMedium medium,
            string targetLocation,
            DateTime operatedAt,
            string operatorName,
            string remark,
            string description)
        {
            var before = HardDiskLedgerSyncSupport.CaptureSnapshot(medium);
            var ledger = medium.Ledger
                ?? throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 缺少台账信息。");

            string normalizedTargetLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(targetLocation);
            if (HardDiskLedgerSyncSupport.IsSameFullLocation(before.Location, normalizedTargetLocation))
            {
                return;
            }

            ledger.StorageLocation = normalizedTargetLocation;
            ledger.UpdatedTime = operatedAt;
            medium.UpdatedTime = operatedAt;
            medium.Remark = string.Join("；",
                new[] { medium.Remark?.Trim(), remark }.Where(value => !string.IsNullOrWhiteSpace(value)));

            if (HardDiskLedgerSyncSupport.HasLedgerMaterialChange(before, ledger))
            {
                _filingRepository.AddHardDiskMediaTransaction(
                    HardDiskLedgerSyncSupport.BuildSyncTransaction(
                        medium,
                        before,
                        operatorName,
                        operatedAt,
                        remark,
                        description,
                        medium.DiskCode.Trim(),
                        medium.DiskCode.Trim()));
            }
        }

        private static string BuildSourceSlotKey(BatchSimulatedSlotPhysicalMoveRequest request)
            => ArchiveSlotLocationSupport.BuildSlotKey(
                request.SourceCabinetName,
                request.SourceFace,
                request.SourceRow,
                request.SourceColumn);

        private static bool IsBlankHardDiskMagneticDiskSlotCategory(string? categoryName)
            => CabinetHardDiskSlotCategoryAssignment.MatchesCategory(
                categoryName,
                CabinetHardDiskSlotCategoryAssignment.CategoryBlank);
    }
}
