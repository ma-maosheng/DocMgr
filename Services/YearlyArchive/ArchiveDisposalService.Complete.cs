using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.HardDiskMedia;

namespace DocMgr.Services.YearlyArchive;

/// <summary>
/// 资料离库处置：办结清账。
/// </summary>
public sealed partial class ArchiveDisposalService
{
    /// <inheritdoc />
    public async Task CompleteAsync(
        int recordId,
        User currentUser,
        bool physicalRemovalConfirmed,
        bool formatRetainedConfirmed,
        IReadOnlyDictionary<int, string>? formatRetainBlankSlotsByItemId = null)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetRecordByIdForUpdateAsync(recordId)
            ?? throw new InvalidOperationException("未找到资料离库处置单。");

        if (existing.Status != YearlyArchiveDisposalRecord.StatusSignedUploaded)
        {
            throw new InvalidOperationException("请先确认可上传签批单后再办结。");
        }

        var attachments = await _repository.GetAttachmentsAsync(existing.DisposalNo);
        bool hasSignedForm = attachments.Any(item =>
            string.Equals(item.FileCategory, ArchiveDisposalDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal));
        if (!hasSignedForm)
        {
            throw new InvalidOperationException("办结前须上传签批单附件。");
        }

        bool needsScenePhoto = ArchiveDisposalDomainValues.RequiresScenePhoto(
            existing.Items.Select(item => item.DispositionMethod));
        if (needsScenePhoto)
        {
            bool hasScenePhoto = attachments.Any(item =>
                ArchiveDisposalDomainValues.IsScenePhotoCategory(item.FileCategory));
            if (!hasScenePhoto)
            {
                throw new InvalidOperationException("处置方式含「离库销毁」时，办结前须上传处置资料照片。");
            }
        }

        bool needsFormatConfirm = ArchiveDisposalDomainValues.HasFormatRetainMethod(
            existing.Items.Select(item => item.DispositionMethod));
        if (needsFormatConfirm && !formatRetainedConfirmed)
        {
            throw new InvalidOperationException("本单含低格留盘，办结前须确认已完成低级格式化。");
        }

        bool needsPhysicalRemoval = await WillDisposeAnyContainerAsync(existing);
        if (needsPhysicalRemoval && !physicalRemovalConfirmed)
        {
            throw new InvalidOperationException("本单办结将释档空档案盒/介质袋，须确认已完成物理移除。");
        }

        if (formatRetainBlankSlotsByItemId != null)
        {
            foreach (var item in existing.Items.Where(i =>
                         ArchiveDisposalDomainValues.IsFormatRetainMethod(i.DispositionMethod)))
            {
                if (formatRetainBlankSlotsByItemId.TryGetValue(item.Id, out string? slot)
                    && !string.IsNullOrWhiteSpace(slot))
                {
                    item.TargetBlankSlotLocation = slot.Trim();
                }
            }
        }

        ValidateBlankSlotsForFormatRetain(existing);

        DateTime now = DateTime.Now;
        string operatorName = ResolveUserDisplayName(currentUser);

        if (existing.IsSimulated)
        {
            await CompleteSimulatedAsync(existing, operatorName, now);
        }
        else
        {
            await CompleteElectronicAsync(existing, operatorName, now);
        }

        if (needsPhysicalRemoval)
        {
            existing.PhysicalRemovalConfirmed = true;
            existing.PhysicalRemovalConfirmedAt = now;
            existing.PhysicalRemovalConfirmedBy = operatorName;
        }

        if (needsFormatConfirm)
        {
            existing.FormatRetainedConfirmed = true;
            existing.FormatRetainedConfirmedAt = now;
            existing.FormatRetainedConfirmedBy = operatorName;
        }

        existing.Status = YearlyArchiveDisposalRecord.StatusCompleted;
        existing.CompletedAt = now;
        existing.CompletedBy = operatorName;
        existing.SignedAttachmentUploaded = true;
        if (needsScenePhoto)
        {
            existing.ScenePhotoUploaded = true;
        }

        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    private async Task CompleteSimulatedAsync(
        YearlyArchiveDisposalRecord existing,
        string operatorName,
        DateTime now)
    {
        var factIds = existing.Items.Select(item => item.FilingFactId).Distinct().ToList();
        var facts = await _repository.GetFilingFactsByIdsAsync(factIds);
        if (facts.Count != factIds.Count)
        {
            throw new InvalidOperationException("部分立档事实不存在，无法办结。");
        }

        foreach (var item in existing.Items.OrderBy(i => i.SortOrder))
        {
            var fact = facts.First(f => f.Id == item.FilingFactId);
            string beforeLifecycle = fact.LifecycleStatus ?? string.Empty;
            string beforeLocation = string.IsNullOrWhiteSpace(fact.CurrentStorageLocation)
                ? (fact.StorageLocation ?? string.Empty)
                : fact.CurrentStorageLocation;
            string beforeContainer = string.IsNullOrWhiteSpace(fact.CurrentContainerCode)
                ? (fact.ContainerCode ?? string.Empty)
                : fact.CurrentContainerCode;

            fact.LifecycleStatus = FilingFactLifecycleStatus.Disposed;
            fact.LifecycleUpdatedAt = now;
            fact.LifecycleRemark = AppendRemark(
                fact.LifecycleRemark,
                $"资料离库处置 [{existing.DisposalNo}] {item.DisposalReason}/{item.DispositionMethod}");
            fact.BorrowHintLevel = FilingFactBorrowHintLevel.None;
            fact.BorrowHintText = string.Empty;
            fact.BorrowHintUpdatedAt = now;
            fact.CurrentStorageLocation = string.Empty;

            _repository.AddMaterialTransaction(new YearlyArchiveMaterialTransaction
            {
                FilingFactId = fact.Id,
                TransactionType = MaterialTransactionDomainValues.TypeDisposal,
                BusinessNo = existing.DisposalNo,
                SourceKind = MaterialTransactionDomainValues.SourceDisposalItem,
                SourceId = item.Id,
                DedupKey = $"Disposal:{existing.DisposalNo}:Fact:{fact.Id}",
                BeforeLifecycleStatus = beforeLifecycle,
                AfterLifecycleStatus = FilingFactLifecycleStatus.Disposed,
                BeforeContainerCode = beforeContainer,
                AfterContainerCode = beforeContainer,
                BeforeStorageLocation = beforeLocation,
                AfterStorageLocation = string.Empty,
                Summary = $"资料离库处置：{item.DisposalReason} / {item.DispositionMethod}",
                Remark = existing.Remark,
                OperatorName = operatorName,
                OperatedAt = now,
                CreatedAt = now
            });
        }

        var boxIds = existing.Items.Select(item => item.ContainerId).Where(id => id > 0).Distinct().ToList();
        var boxes = await _repository.GetBoxesByIdsAsync(boxIds);
        foreach (var box in boxes)
        {
            if (await ShouldDisposeSimulatedBoxAsync(box.Id, disposedFactIds: factIds.ToHashSet()))
            {
                DisposeSimulatedBox(box, operatorName, now);
            }
        }
    }

    private async Task<bool> ShouldDisposeSimulatedBoxAsync(int boxId, HashSet<int> disposedFactIds)
    {
        var facts = await _repository.GetFilingFactsByContainerIdAsync(
            boxId,
            ArchiveRegisterDomainValues.MediaKindSimulated);

        foreach (var fact in facts)
        {
            string status = disposedFactIds.Contains(fact.Id)
                ? FilingFactLifecycleStatus.Disposed
                : (fact.LifecycleStatus ?? string.Empty);

            if (string.Equals(status, FilingFactLifecycleStatus.InArchive, StringComparison.Ordinal)
                || string.Equals(status, FilingFactLifecycleStatus.Borrowed, StringComparison.Ordinal))
            {
                return false;
            }

            // Destroyed / Transferred / Disposed 均视为不再占用盒位业务
            if (!string.Equals(status, FilingFactLifecycleStatus.Disposed, StringComparison.Ordinal)
                && !string.Equals(status, FilingFactLifecycleStatus.Destroyed, StringComparison.Ordinal)
                && !string.Equals(status, FilingFactLifecycleStatus.Transferred, StringComparison.Ordinal))
            {
                // 未知状态保守不释档
                if (!string.IsNullOrWhiteSpace(status))
                {
                    return false;
                }
            }
        }

        // 盒内须全部已处置（本单处置或历史已处置）；若仅 Destroyed 而无本单/历史 Disposed，盘库中间态仍占档——本单应已将相关事实置 Disposed
        bool anyDisposed = facts.Any(f =>
            disposedFactIds.Contains(f.Id)
            || string.Equals(f.LifecycleStatus, FilingFactLifecycleStatus.Disposed, StringComparison.Ordinal));
        return anyDisposed && facts.All(f =>
        {
            string status = disposedFactIds.Contains(f.Id)
                ? FilingFactLifecycleStatus.Disposed
                : (f.LifecycleStatus ?? string.Empty);
            return string.Equals(status, FilingFactLifecycleStatus.Disposed, StringComparison.Ordinal)
                || string.Equals(status, FilingFactLifecycleStatus.Destroyed, StringComparison.Ordinal)
                || string.Equals(status, FilingFactLifecycleStatus.Transferred, StringComparison.Ordinal);
        });
    }

    private void DisposeSimulatedBox(YearlyArchiveBox box, string operatorName, DateTime now)
    {
        string lastLocation = box.BoxLocationCode?.Trim() ?? string.Empty;
        box.LastStorageLocation = lastLocation;
        box.ContainerLifecycleStatus = ArchiveContainerLifecycleStatus.Disposed;
        box.RetiredAt = now;
        box.RetiredBy = operatorName;
        box.BoxLocationCode = string.Empty;
        box.CabinetName = string.Empty;
        box.Side = string.Empty;
        box.Row = 0;
        box.Column = 0;
        box.BoxIndex = 0;

        if (!string.IsNullOrWhiteSpace(box.ArchiveSequenceNo))
        {
            _repository.RemoveArchiveBoxPlacementByBoxCode(box.ArchiveSequenceNo);
        }
    }

    private async Task CompleteElectronicAsync(
        YearlyArchiveDisposalRecord existing,
        string operatorName,
        DateTime now)
    {
        var hdItems = existing.Items
            .Where(i => string.Equals(i.MediumKind, ArchiveInventoryRegisterDomainValues.MediumKindHardDisk, StringComparison.Ordinal))
            .ToList();
        var odItems = existing.Items
            .Where(i => string.Equals(i.MediumKind, ArchiveInventoryRegisterDomainValues.MediumKindOpticalDisc, StringComparison.Ordinal))
            .ToList();

        var hardDisks = await _repository.GetHardDiskMediaWithLedgerByIdsAsync(hdItems.Select(i => i.MediumId).ToList());
        var opticalDiscs = await _repository.GetOpticalDiscMediaWithLedgerByIdsAsync(odItems.Select(i => i.MediumId).ToList());

        foreach (var item in hdItems.OrderBy(i => i.SortOrder))
        {
            var medium = hardDisks.FirstOrDefault(m => m.Id == item.MediumId)
                ?? throw new InvalidOperationException($"未找到硬盘「{item.MediumCode}」。");
            var ledger = medium.Ledger
                ?? throw new InvalidOperationException($"硬盘「{medium.DiskCode}」缺少台账。");

            string beforeStatus = ledger.MediaStatus?.Trim() ?? string.Empty;
            string beforeLocation = ledger.StorageLocation?.Trim() ?? item.BeforeStorageLocation;
            string method = item.DispositionMethod?.Trim() ?? string.Empty;
            string holder = ArchiveDisposalDomainValues.ResolveHolderAfterComplete(method);

            if (ArchiveDisposalDomainValues.IsFormatRetainMethod(method))
            {
                string target = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(item.TargetBlankSlotLocation);
                if (string.IsNullOrWhiteSpace(target))
                {
                    throw new InvalidOperationException($"硬盘「{medium.DiskCode}」低格留盘缺少目标空盘档口。");
                }

                medium.UpdatedTime = now;
                ledger.UpdatedTime = now;
                ledger.MediaStatus = HardDiskMedium.StatusInStockBlank;
                ledger.MediaNature = HardDiskMedium.NatureBlank;
                ledger.StorageLocation = target;
                ledger.NeedReturn = false;
                ledger.HolderOrOrganization = holder;
                ledger.Remark = AppendRemark(
                    ledger.Remark,
                    $"资料离库处置 [{existing.DisposalNo}] 低格留盘→{target}");

                _repository.AddHardDiskTransaction(new HardDiskMediaTransaction
                {
                    MediumId = medium.Id,
                    TransactionType = HardDiskMediaTransaction.TypeDisposal,
                    BeforeStatus = beforeStatus,
                    AfterStatus = HardDiskMedium.StatusInStockBlank,
                    BeforeLocation = beforeLocation,
                    AfterLocation = target,
                    OperatorName = operatorName,
                    OperateTime = now,
                    RelatedPerson = existing.ApplicantName,
                    TargetOrganization = holder,
                    NeedReturn = false,
                    RelatedBatch = existing.DisposalNo,
                    Description = "资料离库处置：低格留盘至空白硬盘专用档口",
                    Remark = existing.Remark
                });
            }
            else
            {
                // 离库销毁 / 库内注销 → 离库(处置)
                medium.UpdatedTime = now;
                ledger.UpdatedTime = now;
                ledger.MediaStatus = HardDiskMedium.StatusDisposed;
                ledger.StorageLocation = string.Empty;
                ledger.NeedReturn = false;
                ledger.HolderOrOrganization = holder;
                ledger.Remark = AppendRemark(
                    ledger.Remark,
                    $"资料离库处置 [{existing.DisposalNo}] {item.DisposalReason}/{method}");

                _repository.AddHardDiskTransaction(new HardDiskMediaTransaction
                {
                    MediumId = medium.Id,
                    TransactionType = HardDiskMediaTransaction.TypeDisposal,
                    BeforeStatus = beforeStatus,
                    AfterStatus = HardDiskMedium.StatusDisposed,
                    BeforeLocation = beforeLocation,
                    AfterLocation = string.Empty,
                    OperatorName = operatorName,
                    OperateTime = now,
                    RelatedPerson = existing.ApplicantName,
                    TargetOrganization = holder,
                    NeedReturn = false,
                    RelatedBatch = existing.DisposalNo,
                    Description = $"资料离库处置：{item.DisposalReason}/{method}",
                    Remark = existing.Remark
                });
            }

            // 解除袋内关联
            var links = await _repository.GetHardDiskLinksByUnitIdAsync(item.ElectronicArchiveUnitId);
            foreach (var link in links.Where(l => l.HardDiskMediumId == medium.Id))
            {
                _repository.RemoveHardDiskMediumLink(link);
            }

            if (medium.RegisterLock != null
                && string.Equals(
                    medium.RegisterLock.BusinessType,
                    HardDiskRegisterLock.BusinessTypeArchiveDisposal,
                    StringComparison.Ordinal)
                && medium.RegisterLock.BusinessRecordId == existing.Id)
            {
                _repository.RemoveRegisterLock(medium.RegisterLock);
                medium.RegisterLock = null;
            }
        }

        foreach (var item in odItems.OrderBy(i => i.SortOrder))
        {
            var medium = opticalDiscs.FirstOrDefault(m => m.Id == item.MediumId)
                ?? throw new InvalidOperationException($"未找到光盘「{item.MediumCode}」。");
            var ledger = medium.Ledger
                ?? throw new InvalidOperationException($"光盘「{medium.DiscCode}」缺少台账。");

            string beforeStatus = ledger.MediaStatus?.Trim() ?? string.Empty;
            string beforeLocation = ledger.StorageLocation?.Trim() ?? item.BeforeStorageLocation;
            string method = item.DispositionMethod?.Trim() ?? string.Empty;
            string holder = ArchiveDisposalDomainValues.ResolveHolderAfterComplete(method);

            medium.UpdatedTime = now;
            ledger.UpdatedTime = now;
            ledger.MediaStatus = OpticalDiscMedium.StatusDestroyed;
            ledger.StorageLocation = string.Empty;
            ledger.NeedReturn = false;
            ledger.HolderOrOrganization = holder;
            ledger.Remark = AppendRemark(
                ledger.Remark,
                $"资料离库处置 [{existing.DisposalNo}] {item.DisposalReason}/{method}");

            _repository.AddOpticalDiscTransaction(new OpticalDiscMediaTransaction
            {
                MediumId = medium.Id,
                TransactionType = OpticalDiscMediaTransaction.TypeDestroy,
                BeforeStatus = beforeStatus,
                AfterStatus = OpticalDiscMedium.StatusDestroyed,
                BeforeLocation = beforeLocation,
                AfterLocation = string.Empty,
                OperatorName = operatorName,
                OperateTime = now,
                RelatedPerson = existing.ApplicantName,
                TargetOrganization = holder,
                NeedReturn = false,
                RelatedBatch = existing.DisposalNo,
                Description = $"资料离库处置：{item.DisposalReason}/{method}",
                Remark = existing.Remark
            });

            var links = await _repository.GetDiscLinksByUnitIdAsync(item.ElectronicArchiveUnitId);
            foreach (var link in links.Where(l => l.OpticalDiscMediumId == medium.Id))
            {
                _repository.RemoveDiscLink(link);
            }
        }

        // 关联事实 → Disposed
        var unitIds = existing.Items.Select(i => i.ElectronicArchiveUnitId).Where(id => id > 0).Distinct().ToList();
        foreach (int unitId in unitIds)
        {
            var facts = await _repository.GetFilingFactsByElectronicUnitIdAsync(unitId);
            foreach (var fact in facts.Where(f =>
                         !string.Equals(f.LifecycleStatus, FilingFactLifecycleStatus.Disposed, StringComparison.Ordinal)))
            {
                string beforeLifecycle = fact.LifecycleStatus ?? string.Empty;
                fact.LifecycleStatus = FilingFactLifecycleStatus.Disposed;
                fact.LifecycleUpdatedAt = now;
                fact.LifecycleRemark = AppendRemark(
                    fact.LifecycleRemark,
                    $"资料离库处置 [{existing.DisposalNo}] 关联介质清账");
                fact.BorrowHintLevel = FilingFactBorrowHintLevel.None;
                fact.BorrowHintText = string.Empty;
                fact.CurrentStorageLocation = string.Empty;

                _repository.AddMaterialTransaction(new YearlyArchiveMaterialTransaction
                {
                    FilingFactId = fact.Id,
                    TransactionType = MaterialTransactionDomainValues.TypeDisposal,
                    BusinessNo = existing.DisposalNo,
                    SourceKind = MaterialTransactionDomainValues.SourceDisposalItem,
                    SourceId = existing.Id,
                    DedupKey = $"Disposal:{existing.DisposalNo}:Fact:{fact.Id}",
                    BeforeLifecycleStatus = beforeLifecycle,
                    AfterLifecycleStatus = FilingFactLifecycleStatus.Disposed,
                    BeforeContainerCode = fact.CurrentContainerCode ?? fact.ContainerCode ?? string.Empty,
                    AfterContainerCode = fact.ContainerCode ?? string.Empty,
                    BeforeStorageLocation = fact.StorageLocation ?? string.Empty,
                    AfterStorageLocation = string.Empty,
                    Summary = "资料离库处置：电子介质清账联动立档事实处置",
                    Remark = existing.Remark,
                    OperatorName = operatorName,
                    OperatedAt = now,
                    CreatedAt = now
                });
            }
        }

        var units = await _repository.GetElectronicUnitsByIdsAsync(unitIds);
        foreach (var unit in units)
        {
            var remainingHd = await _repository.GetHardDiskLinksByUnitIdAsync(unit.Id);
            var remainingOd = await _repository.GetDiscLinksByUnitIdAsync(unit.Id);
            if (remainingHd.Count == 0 && remainingOd.Count == 0)
            {
                unit.UnitLifecycleStatus = ArchiveContainerLifecycleStatus.Disposed;
                unit.StorageLocation = string.Empty;
            }
        }
    }

    private async Task<bool> WillDisposeAnyContainerAsync(YearlyArchiveDisposalRecord record)
    {
        if (record.IsSimulated)
        {
            var factIds = record.Items.Select(i => i.FilingFactId).ToHashSet();
            var boxIds = record.Items.Select(i => i.ContainerId).Where(id => id > 0).Distinct();
            foreach (int boxId in boxIds)
            {
                if (await ShouldDisposeSimulatedBoxAsync(boxId, factIds))
                {
                    return true;
                }
            }

            return false;
        }

        // 电子：若某袋在本单处置后将无剩余介质关联，则需要物理移除确认
        var byUnit = record.Items.GroupBy(i => i.ElectronicArchiveUnitId).Where(g => g.Key > 0);
        foreach (var group in byUnit)
        {
            var remainingHd = await _repository.GetHardDiskLinksByUnitIdAsync(group.Key);
            var remainingOd = await _repository.GetDiscLinksByUnitIdAsync(group.Key);
            var disposingHd = group
                .Where(i => string.Equals(i.MediumKind, ArchiveInventoryRegisterDomainValues.MediumKindHardDisk, StringComparison.Ordinal))
                .Select(i => i.MediumId)
                .ToHashSet();
            var disposingOd = group
                .Where(i => string.Equals(i.MediumKind, ArchiveInventoryRegisterDomainValues.MediumKindOpticalDisc, StringComparison.Ordinal))
                .Select(i => i.MediumId)
                .ToHashSet();

            bool hdLeft = remainingHd.Any(l => !disposingHd.Contains(l.HardDiskMediumId));
            bool odLeft = remainingOd.Any(l => !disposingOd.Contains(l.OpticalDiscMediumId));
            if (!hdLeft && !odLeft)
            {
                return true;
            }
        }

        return false;
    }

    private static string AppendRemark(string? existing, string addition)
    {
        string current = existing?.Trim() ?? string.Empty;
        string add = addition?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(add))
        {
            return current;
        }

        return string.IsNullOrWhiteSpace(current) ? add : $"{current}；{add}";
    }
}
