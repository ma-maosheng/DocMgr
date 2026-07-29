using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.HardDiskMedia;

namespace DocMgr.Services.YearlyArchive
{
    public sealed partial class ArchiveRelocationService
    {
        private async Task<ArchiveRelocationPreview> BuildInteractiveBlankHardDiskPreviewAsync(InteractiveItemPhysicalMoveRequest request)
        {
            if (request.SourceMediumId <= 0)
            {
                return Blocked("未指定源空白硬盘。");
            }

            HardDiskMedium sourceMedium;
            try
            {
                sourceMedium = await LoadValidatedInteractiveBlankHardDiskSourceAsync(request.SourceMediumId);
            }
            catch (InvalidOperationException ex)
            {
                return Blocked(ex.Message);
            }

            string sourceSlotKey = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(sourceMedium.Ledger!.StorageLocation);
            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);

            if (string.Equals(sourceSlotKey, targetSlotKey, StringComparison.OrdinalIgnoreCase))
            {
                return Blocked("新位置与当前位置相同，无需迁移。");
            }

            var targetRequest = new BatchSimulatedSlotPhysicalMoveRequest
            {
                TargetCabinetName = request.TargetCabinetName,
                TargetFace = request.TargetFace,
                TargetRow = request.TargetRow,
                TargetColumn = request.TargetColumn
            };
            string? targetIssue = await ValidateTargetBlankHardDiskSlotForBatchMoveAsync(targetRequest, 1);
            if (!string.IsNullOrWhiteSpace(targetIssue))
            {
                return Blocked(targetIssue);
            }

            int occupiedCount = (await _hardDiskMediaRepository.GetInStockBlankHardDisksInSlotAsync(
                targetSlotKey,
                unlockedOnly: false)).Count;
            int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(
                CabinetHardDiskSlotCategoryAssignment.CategoryBlank);
            string slotPurposeText = ResolveMagneticSlotCategoryDisplay(
                CabinetHardDiskSlotCategoryAssignment.CategoryBlank);
            string slotSpaceText = $"迁入后 {occupiedCount + 1} 盘 / 档口容量 {slotCapacity} 盘";

            return Ready(
                $"【单件物理迁档】空白硬盘 [{sourceMedium.DiskCode}] 将从 [{sourceSlotKey}] 迁至 [{targetSlotKey}]；硬盘台账存放位置将同步更新。\n档口用途：{slotPurposeText}\n档口空间：{slotSpaceText}",
                1);
        }

        private async Task<ArchiveRelocationResult> ExecuteInteractiveBlankHardDiskPhysicalMoveAsync(
            InteractiveItemPhysicalMoveRequest request)
        {
            var sourceMedium = await LoadValidatedInteractiveBlankHardDiskSourceAsync(request.SourceMediumId);
            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            string targetLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(targetSlotKey);
            DateTime operatedAt = DateTime.Now;
            string operatorName = ResolveOperatorName();
            string sourceSlotKey = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(sourceMedium.Ledger!.StorageLocation);
            string remark = $"单件迁档：由 [{sourceSlotKey}] 迁至 [{targetSlotKey}]。";

            await using var transaction = await _relocationRepository.BeginTransactionAsync();
            try
            {
                RelocateBlankHardDiskLedger(sourceMedium, targetLocation, operatedAt, operatorName, remark, "单件迁档");
                await _relocationRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                return ArchiveRelocationResult.Ok(
                    string.Empty,
                    $"空白硬盘 [{sourceMedium.DiskCode}] 已迁至 [{targetSlotKey}]。");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<HardDiskMedium> LoadValidatedInteractiveBlankHardDiskSourceAsync(int mediumId)
        {
            var medium = await _filingRepository.GetHardDiskMediumByIdWithLedgerAsync(mediumId)
                ?? throw new InvalidOperationException("未找到源空白硬盘。");

            if (medium.RegisterLock != null)
            {
                throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 正被业务占用，暂不可迁档。");
            }

            if (!string.Equals(medium.Ledger?.MediaStatus, HardDiskMedium.StatusInStockBlank, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 不是在库空白盘，无法迁档。");
            }

            string sourceSlotKey = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(medium.Ledger!.StorageLocation);
            if (!ArchiveSlotLocationSupport.TryParseSlotLocation(
                    sourceSlotKey,
                    out string sourceCabinetName,
                    out string sourceFace,
                    out int sourceRow,
                    out int sourceColumn))
            {
                throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 的存放位置无法解析为档口。");
            }

            var sourceCabinet = await _filingRepository.GetMagneticDiskCabinetByNameAsync(sourceCabinetName)
                ?? throw new InvalidOperationException($"未找到源防磁磁盘柜 [{sourceCabinetName}]。");

            string? sourceCategory = await _filingRepository.GetMagneticDiskSlotCategoryNameAsync(
                sourceCabinet.Id,
                sourceFace.Trim(),
                $"{sourceRow}-{sourceColumn}");
            string normalizedSourceCategory = CabinetHardDiskSlotCategoryAssignment.NormalizeCategoryName(sourceCategory);
            if (!IsBlankHardDiskMagneticDiskSlotCategory(normalizedSourceCategory))
            {
                throw new InvalidOperationException("源档口须为空白硬盘专用档口。");
            }

            return medium;
        }
    }
}
