using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.HardDiskMedia;

namespace DocMgr.Services.YearlyArchive
{
    public sealed partial class ArchiveRelocationService
    {
        private async Task<ArchiveRelocationPreview> BuildInteractiveDamagedHardDisksPreviewAsync(InteractiveItemsPhysicalMoveRequest request)
        {
            var mediumIds = NormalizePositiveIds(request.SourceMediumIds);
            if (mediumIds.Count == 0)
            {
                return Blocked("未指定源损坏硬盘。");
            }

            var media = new List<HardDiskMedium>();
            foreach (int mediumId in mediumIds)
            {
                try
                {
                    media.Add(await LoadValidatedInteractiveDamagedHardDiskSourceAsync(mediumId));
                }
                catch (InvalidOperationException ex)
                {
                    return Blocked(ex.Message);
                }
            }

            string sourceSlotKey = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(media[0].Ledger!.StorageLocation);
            if (media.Any(item => !string.Equals(
                    HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(item.Ledger!.StorageLocation),
                    sourceSlotKey,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return Blocked("所选损坏硬盘不在同一档口，无法一次迁档。");
            }

            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            if (string.Equals(sourceSlotKey, targetSlotKey, StringComparison.OrdinalIgnoreCase))
            {
                return Blocked("新位置与当前位置相同，无需迁移。");
            }

            string? targetIssue = await ValidateTargetDamagedHardDiskSlotAsync(request, media.Count, excludeMediumIds: media.Select(m => m.Id).ToHashSet());
            if (!string.IsNullOrWhiteSpace(targetIssue))
            {
                return Blocked(targetIssue);
            }

            int occupiedCount = (await _hardDiskMediaRepository.GetInStockDamagedHardDisksInSlotAsync(
                targetSlotKey,
                unlockedOnly: false)).Count;
            int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(
                CabinetHardDiskSlotCategoryAssignment.CategoryDamaged);
            string label = media.Count == 1 ? $"损坏硬盘 [{media[0].DiskCode}]" : $"{media.Count} 块损坏硬盘";
            return Ready(
                $"【交互式物理迁档】{label} 将从 [{sourceSlotKey}] 迁至 [{targetSlotKey}]；硬盘台账存放位置将同步更新。\n档口用途：{ResolveMagneticSlotCategoryDisplay(CabinetHardDiskSlotCategoryAssignment.CategoryDamaged)}\n档口空间：迁入后 {occupiedCount + media.Count} 盘 / 档口容量 {slotCapacity} 盘",
                media.Count);
        }

        private async Task<ArchiveRelocationPreview> BuildInteractiveDamagedOpticalDiscsPreviewAsync(InteractiveItemsPhysicalMoveRequest request)
        {
            var mediumIds = NormalizePositiveIds(request.SourceMediumIds);
            if (mediumIds.Count == 0)
            {
                return Blocked("未指定源损坏光盘。");
            }

            var media = new List<OpticalDiscMedium>();
            foreach (int mediumId in mediumIds)
            {
                try
                {
                    media.Add(await LoadValidatedInteractiveDamagedOpticalDiscSourceAsync(mediumId));
                }
                catch (InvalidOperationException ex)
                {
                    return Blocked(ex.Message);
                }
            }

            string sourceSlotKey = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(media[0].Ledger!.StorageLocation);
            if (media.Any(item => !string.Equals(
                    HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(item.Ledger!.StorageLocation),
                    sourceSlotKey,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return Blocked("所选损坏光盘不在同一档口，无法一次迁档。");
            }

            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            if (string.Equals(sourceSlotKey, targetSlotKey, StringComparison.OrdinalIgnoreCase))
            {
                return Blocked("新位置与当前位置相同，无需迁移。");
            }

            string? targetIssue = await ValidateTargetDamagedOpticalDiscSlotAsync(request, media.Count, excludeMediumIds: media.Select(m => m.Id).ToHashSet());
            if (!string.IsNullOrWhiteSpace(targetIssue))
            {
                return Blocked(targetIssue);
            }

            int occupiedCount = (await _hardDiskMediaRepository.GetInStockDamagedOpticalDiscsInSlotAsync(targetSlotKey)).Count;
            int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(
                CabinetHardDiskSlotCategoryAssignment.CategoryDamagedOpticalDisc);
            string label = media.Count == 1 ? $"损坏光盘 [{media[0].DiscCode}]" : $"{media.Count} 张损坏光盘";
            return Ready(
                $"【交互式物理迁档】{label} 将从 [{sourceSlotKey}] 迁至 [{targetSlotKey}]；光盘台账存放位置将同步更新。\n档口用途：{ResolveMagneticSlotCategoryDisplay(CabinetHardDiskSlotCategoryAssignment.CategoryDamagedOpticalDisc)}\n档口空间：迁入后 {occupiedCount + media.Count} 盘 / 档口容量 {slotCapacity} 盘",
                media.Count);
        }

        private async Task<ArchiveRelocationResult> ExecuteInteractiveDamagedHardDisksPhysicalMoveAsync(InteractiveItemsPhysicalMoveRequest request)
        {
            var mediumIds = NormalizePositiveIds(request.SourceMediumIds);
            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            string targetLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(targetSlotKey);
            DateTime operatedAt = DateTime.Now;
            string operatorName = ResolveOperatorName();

            await using var transaction = await _relocationRepository.BeginTransactionAsync();
            try
            {
                foreach (int mediumId in mediumIds)
                {
                    var medium = await LoadValidatedInteractiveDamagedHardDiskSourceAsync(mediumId);
                    string sourceSlotKey = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(medium.Ledger!.StorageLocation);
                    string remark = $"交互式迁档：由 [{sourceSlotKey}] 迁至 [{targetSlotKey}]。";
                    RelocateDamagedHardDiskLedger(medium, targetLocation, operatedAt, operatorName, remark, "交互式迁档");
                }

                await _relocationRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                return ArchiveRelocationResult.Ok(
                    string.Empty,
                    mediumIds.Count == 1
                        ? $"损坏硬盘已迁至 [{targetSlotKey}]。"
                        : $"已将 {mediumIds.Count} 块损坏硬盘迁至 [{targetSlotKey}]。");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<ArchiveRelocationResult> ExecuteInteractiveDamagedOpticalDiscsPhysicalMoveAsync(InteractiveItemsPhysicalMoveRequest request)
        {
            var mediumIds = NormalizePositiveIds(request.SourceMediumIds);
            string targetSlotKey = ArchiveSlotLocationSupport.BuildSlotKey(
                request.TargetCabinetName,
                request.TargetFace,
                request.TargetRow,
                request.TargetColumn);
            string targetLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(targetSlotKey);
            DateTime operatedAt = DateTime.Now;
            string operatorName = ResolveOperatorName();

            await using var transaction = await _relocationRepository.BeginTransactionAsync();
            try
            {
                foreach (int mediumId in mediumIds)
                {
                    var medium = await LoadValidatedInteractiveDamagedOpticalDiscSourceAsync(mediumId);
                    string sourceSlotKey = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(medium.Ledger!.StorageLocation);
                    string remark = $"交互式迁档：由 [{sourceSlotKey}] 迁至 [{targetSlotKey}]。";
                    RelocateDamagedOpticalDiscLedger(medium, targetLocation, operatedAt, operatorName, remark);
                }

                await _relocationRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                return ArchiveRelocationResult.Ok(
                    string.Empty,
                    mediumIds.Count == 1
                        ? $"损坏光盘已迁至 [{targetSlotKey}]。"
                        : $"已将 {mediumIds.Count} 张损坏光盘迁至 [{targetSlotKey}]。");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<HardDiskMedium> LoadValidatedInteractiveDamagedHardDiskSourceAsync(int mediumId)
        {
            var medium = await _filingRepository.GetHardDiskMediumByIdWithLedgerAsync(mediumId)
                ?? throw new InvalidOperationException("未找到源损坏硬盘。");

            if (medium.RegisterLock != null)
            {
                throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 正被业务占用，暂不可迁档。");
            }

            if (!string.Equals(medium.Ledger?.MediaStatus, HardDiskMedium.StatusInStockDamaged, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 不是在库损坏盘，无法迁档。");
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
                sourceFace,
                $"{sourceRow}-{sourceColumn}");
            if (!CabinetHardDiskSlotCategoryAssignment.MatchesCategory(
                    sourceCategory,
                    CabinetHardDiskSlotCategoryAssignment.CategoryDamaged))
            {
                throw new InvalidOperationException($"硬盘 [{medium.DiskCode}] 不在损坏硬盘专用档口，无法按损坏盘迁档。");
            }

            return medium;
        }

        private async Task<OpticalDiscMedium> LoadValidatedInteractiveDamagedOpticalDiscSourceAsync(int mediumId)
        {
            var medium = await _filingRepository.GetOpticalDiscMediumByIdWithLedgerAsync(mediumId)
                ?? throw new InvalidOperationException("未找到源损坏光盘。");

            if (!string.Equals(medium.Ledger?.MediaStatus, OpticalDiscMedium.StatusDamaged, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"光盘 [{medium.DiscCode}] 不是在库损坏盘，无法迁档。");
            }

            string sourceSlotKey = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(medium.Ledger!.StorageLocation);
            if (!ArchiveSlotLocationSupport.TryParseSlotLocation(
                    sourceSlotKey,
                    out string sourceCabinetName,
                    out string sourceFace,
                    out int sourceRow,
                    out int sourceColumn))
            {
                throw new InvalidOperationException($"光盘 [{medium.DiscCode}] 的存放位置无法解析为档口。");
            }

            var sourceCabinet = await _filingRepository.GetMagneticDiskCabinetByNameAsync(sourceCabinetName)
                ?? throw new InvalidOperationException($"未找到源防磁磁盘柜 [{sourceCabinetName}]。");
            string? sourceCategory = await _filingRepository.GetMagneticDiskSlotCategoryNameAsync(
                sourceCabinet.Id,
                sourceFace,
                $"{sourceRow}-{sourceColumn}");
            if (!CabinetHardDiskSlotCategoryAssignment.MatchesCategory(
                    sourceCategory,
                    CabinetHardDiskSlotCategoryAssignment.CategoryDamagedOpticalDisc))
            {
                throw new InvalidOperationException($"光盘 [{medium.DiscCode}] 不在损坏光盘专用档口，无法按损坏光盘迁档。");
            }

            return medium;
        }

        private async Task<string?> ValidateTargetDamagedHardDiskSlotAsync(
            InteractiveItemsPhysicalMoveRequest request,
            int incomingCount,
            IReadOnlySet<int> excludeMediumIds)
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
            if (!CabinetHardDiskSlotCategoryAssignment.MatchesCategory(
                    targetCategory,
                    CabinetHardDiskSlotCategoryAssignment.CategoryDamaged))
            {
                return "目标档口须为损坏硬盘专用档口。";
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
                return "目标档口已有电子介质袋占用，请选择损坏硬盘专用档口。";
            }

            var opticalLocations = await _hardDiskMediaRepository.GetInStockOpticalDiscStorageLocationsInSlotAsync(targetSlotKey);
            if (opticalLocations.Count > 0)
            {
                return "目标档口已有光盘占用，请选择损坏硬盘专用档口。";
            }

            var damagedInTarget = await _hardDiskMediaRepository.GetInStockDamagedHardDisksInSlotAsync(
                targetSlotKey,
                unlockedOnly: false);
            var allHardDiskLocations = await _hardDiskMediaRepository.GetInStockHardDiskStorageLocationsInSlotAsync(targetSlotKey);
            int otherHardDiskCount = allHardDiskLocations.Count - damagedInTarget.Count;
            if (otherHardDiskCount > 0)
            {
                return "目标档口存在非损坏硬盘占用，请选择损坏硬盘专用档口。";
            }

            int occupiedExcludingSources = damagedInTarget.Count(item => !excludeMediumIds.Contains(item.Id));
            int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(
                CabinetHardDiskSlotCategoryAssignment.CategoryDamaged);
            if (occupiedExcludingSources + incomingCount > slotCapacity)
            {
                return $"目标档口容量不足（迁入后需 {occupiedExcludingSources + incomingCount} 盘，档口容量 {slotCapacity} 盘）。";
            }

            return null;
        }

        private async Task<string?> ValidateTargetDamagedOpticalDiscSlotAsync(
            InteractiveItemsPhysicalMoveRequest request,
            int incomingCount,
            IReadOnlySet<int> excludeMediumIds)
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
            if (!CabinetHardDiskSlotCategoryAssignment.MatchesCategory(
                    targetCategory,
                    CabinetHardDiskSlotCategoryAssignment.CategoryDamagedOpticalDisc))
            {
                return "目标档口须为损坏光盘专用档口。";
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
                return "目标档口已有电子介质袋占用，请选择损坏光盘专用档口。";
            }

            var hardDiskLocations = await _hardDiskMediaRepository.GetInStockHardDiskStorageLocationsInSlotAsync(targetSlotKey);
            if (hardDiskLocations.Count > 0)
            {
                return "目标档口已有硬盘占用，请选择损坏光盘专用档口。";
            }

            var damagedInTarget = await _hardDiskMediaRepository.GetInStockDamagedOpticalDiscsInSlotAsync(targetSlotKey);
            int occupiedExcludingSources = damagedInTarget.Count(item => !excludeMediumIds.Contains(item.Id));
            int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(
                CabinetHardDiskSlotCategoryAssignment.CategoryDamagedOpticalDisc);
            if (occupiedExcludingSources + incomingCount > slotCapacity)
            {
                return $"目标档口容量不足（迁入后需 {occupiedExcludingSources + incomingCount} 盘，档口容量 {slotCapacity} 盘）。";
            }

            return null;
        }

        private void RelocateDamagedHardDiskLedger(
            HardDiskMedium medium,
            string targetLocation,
            DateTime operatedAt,
            string operatorName,
            string remark,
            string description)
        {
            // 损坏盘位置编码与空白盘相同：仅档口键。
            RelocateBlankHardDiskLedger(medium, targetLocation, operatedAt, operatorName, remark, description);
        }

        private void RelocateDamagedOpticalDiscLedger(
            OpticalDiscMedium medium,
            string targetLocation,
            DateTime operatedAt,
            string operatorName,
            string remark)
        {
            var ledger = medium.Ledger
                ?? throw new InvalidOperationException($"光盘 [{medium.DiscCode}] 缺少台账信息。");

            string normalizedTargetLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(targetLocation);
            var before = OpticalDiscLedgerSyncSupport.CaptureSnapshot(medium);
            if (HardDiskLedgerSyncSupport.IsSameFullLocation(before.Location, normalizedTargetLocation))
            {
                return;
            }

            ledger.StorageLocation = normalizedTargetLocation;
            ledger.UpdatedTime = operatedAt;
            medium.UpdatedTime = operatedAt;

            if (OpticalDiscLedgerSyncSupport.HasLedgerMaterialChange(before, ledger))
            {
                medium.Transactions.Add(new OpticalDiscMediaTransaction
                {
                    Medium = medium,
                    TransactionType = OpticalDiscMediaTransaction.TypeRelocate,
                    BusinessNo = medium.DiscCode.Trim(),
                    BeforeStatus = before.Status,
                    AfterStatus = ledger.MediaStatus?.Trim() ?? string.Empty,
                    BeforeLocation = before.Location,
                    AfterLocation = normalizedTargetLocation,
                    OperatorName = operatorName,
                    OperateTime = operatedAt,
                    RelatedBatch = medium.DiscCode.Trim(),
                    RelatedArchiveTitle = medium.DiscCode.Trim(),
                    Description = "交互式迁档：同步更新损坏光盘台账存放位置",
                    Remark = remark
                });
            }
        }
    }
}
