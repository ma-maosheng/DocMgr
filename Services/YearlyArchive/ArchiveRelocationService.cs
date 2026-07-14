using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using DocMgr.Services.HardDiskMedia;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    public sealed partial class ArchiveRelocationService : IArchiveRelocationService
    {
        private readonly IArchiveRelocationRepository _relocationRepository;
        private readonly IArchiveFilingRepository _filingRepository;
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IUserContextService _userContextService;
        private readonly IFilingFactWriter _filingFactWriter;
        private readonly IArchiveMaterialTransactionWriter _materialTransactionWriter;
        private readonly IArchiveOutboundPendingReturnContainerService _pendingReturnContainerService;

        public ArchiveRelocationService(
            IArchiveRelocationRepository relocationRepository,
            IArchiveFilingRepository filingRepository,
            IArchiveRegisterService archiveRegisterService,
            IUserContextService userContextService,
            IFilingFactWriter filingFactWriter,
            IArchiveMaterialTransactionWriter materialTransactionWriter,
            IArchiveOutboundPendingReturnContainerService pendingReturnContainerService)
        {
            _relocationRepository = relocationRepository;
            _filingRepository = filingRepository;
            _archiveRegisterService = archiveRegisterService;
            _userContextService = userContextService;
            _filingFactWriter = filingFactWriter;
            _materialTransactionWriter = materialTransactionWriter;
            _pendingReturnContainerService = pendingReturnContainerService;
        }

        public async Task<ArchiveRelocationContainerSummary?> LoadSimulatedSourceAsync(string containerCode)
        {
            EnsureArchiveAdmin();
            var box = await _relocationRepository.GetArchiveBoxBySequenceNoAsync(containerCode);
            return box == null ? null : MapSimulatedSummary(box);
        }

        public async Task<ArchiveRelocationContainerSummary?> LoadSimulatedSourceByIdAsync(int boxId)
        {
            EnsureArchiveAdmin();
            var box = await _relocationRepository.GetArchiveBoxForRelocationAsync(boxId);
            return box == null ? null : MapSimulatedSummary(box);
        }

        public async Task<ArchiveRelocationContainerSummary?> LoadElectronicSourceAsync(string containerCode)
        {
            EnsureArchiveAdmin();
            var unit = await _relocationRepository.GetElectronicUnitByArchiveNoAsync(containerCode);
            return unit == null ? null : MapElectronicSummary(unit);
        }

        public async Task<ArchiveRelocationContainerSummary?> LoadElectronicSourceByIdAsync(int unitId)
        {
            EnsureArchiveAdmin();
            var unit = await _relocationRepository.GetElectronicUnitForRelocationAsync(unitId);
            return unit == null ? null : MapElectronicSummary(unit);
        }

        public async Task<IReadOnlyList<ArchiveRelocationSourceOption>> GetSimulatedSourceOptionsAsync(string projectName, string year)
        {
            EnsureArchiveAdmin();
            if (string.IsNullOrWhiteSpace(projectName) || string.IsNullOrWhiteSpace(year))
            {
                return Array.Empty<ArchiveRelocationSourceOption>();
            }

            var boxes = await _relocationRepository.GetSimulatedSourceCandidatesAsync(projectName.Trim(), year.Trim());
            return boxes
                .Select(box => new ArchiveRelocationSourceOption
                {
                    ContainerId = box.Id,
                    ContainerCode = box.ArchiveSequenceNo,
                    StorageLocation = box.BoxLocationCode,
                    ProjectName = box.ProjectName,
                    Year = box.Year,
                    ItemCount = box.MediaItemLinks.Count,
                    DisplayText = $"{box.ArchiveSequenceNo} | {box.BoxLocationCode} | {box.MediaItemLinks.Count} 项"
                })
                .ToList();
        }

        public async Task<IReadOnlyList<ArchiveRelocationSourceOption>> GetElectronicSourceOptionsAsync(string projectName, string year)
        {
            EnsureArchiveAdmin();
            if (string.IsNullOrWhiteSpace(projectName) || string.IsNullOrWhiteSpace(year))
            {
                return Array.Empty<ArchiveRelocationSourceOption>();
            }

            var units = await _relocationRepository.GetElectronicSourceCandidatesAsync(projectName.Trim(), year.Trim());
            return units
                .Select(unit => new ArchiveRelocationSourceOption
                {
                    ContainerId = unit.Id,
                    ContainerCode = unit.ElectronicArchiveNo,
                    StorageLocation = unit.StorageLocation,
                    ProjectName = unit.ProjectName,
                    Year = unit.Year,
                    ItemCount = unit.MediaItemLinks.Count,
                    ActiveLinkedMediumCode = ResolveActiveLinkedMediumCode(unit),
                    DisplayText = BuildElectronicSourceDisplayText(unit)
                })
                .ToList();
        }

        public async Task<IReadOnlyList<ArchiveRelocationTargetOption>> GetSimulatedTargetOptionsAsync(int sourceBoxId)
        {
            EnsureArchiveAdmin();
            var source = await _relocationRepository.GetArchiveBoxForRelocationAsync(sourceBoxId)
                ?? throw new InvalidOperationException("未找到源档案盒。");

            var boxes = await _relocationRepository.GetSimulatedTargetBoxesAsync(source.ProjectName, source.Year, sourceBoxId);
            return boxes
                .Select(box => new ArchiveRelocationTargetOption
                {
                    ContainerId = box.Id,
                    ContainerCode = box.ArchiveSequenceNo,
                    StorageLocation = box.BoxLocationCode,
                    IsEmpty = box.MediaItemLinks.Count == 0,
                    DisplayText = $"{box.ArchiveSequenceNo} | {box.BoxLocationCode} | {(box.MediaItemLinks.Count == 0 ? "空盒" : $"已用({box.MediaItemLinks.Count}项)")}"
                })
                .ToList();
        }

        public async Task<IReadOnlyList<ArchiveRelocationTargetOption>> GetElectronicTargetOptionsAsync(
            int sourceUnitId,
            bool hardDiskMergeTargetsOnly = false)
        {
            EnsureArchiveAdmin();
            var source = await _relocationRepository.GetElectronicUnitForRelocationAsync(sourceUnitId)
                ?? throw new InvalidOperationException("未找到源电子介质袋。");

            var units = await _relocationRepository.GetElectronicTargetUnitsAsync(source.ProjectName, source.Year, sourceUnitId);
            return units
                .Where(unit => !string.Equals(unit.UnitLifecycleStatus, ArchiveContainerLifecycleStatus.Disposed, StringComparison.Ordinal))
                .Where(unit => !hardDiskMergeTargetsOnly || IsHardDiskMergeTargetUnit(unit))
                .Select(unit => new ArchiveRelocationTargetOption
                {
                    ContainerId = unit.Id,
                    ContainerCode = unit.ElectronicArchiveNo,
                    StorageLocation = unit.StorageLocation,
                    IsEmpty = unit.MediaItemLinks.Count == 0,
                    DisplayText = BuildElectronicSourceDisplayText(unit)
                })
                .ToList();
        }

        public Task<ArchiveRelocationPreview> PreviewSimulatedRelocationAsync(SimulatedRelocationRequest request)
        {
            EnsureArchiveAdmin();
            return BuildSimulatedPreviewAsync(request);
        }

        public Task<ArchiveRelocationPreview> PreviewElectronicRelocationAsync(ElectronicRelocationRequest request)
        {
            EnsureArchiveAdmin();
            return BuildElectronicPreviewAsync(request);
        }

        public async Task<ArchiveRelocationResult> ExecuteSimulatedRelocationAsync(SimulatedRelocationRequest request)
        {
            EnsureArchiveAdmin();
            var preview = await BuildSimulatedPreviewAsync(request);
            if (!preview.CanExecute)
            {
                return ArchiveRelocationResult.Fail(preview.BlockReason);
            }

            await using var transaction = await _relocationRepository.BeginTransactionAsync();
            try
            {
                var source = await _relocationRepository.GetArchiveBoxForRelocationAsync(request.SourceBoxId)
                    ?? throw new InvalidOperationException("未找到源档案盒。");

                DateTime operatedAt = DateTime.Now;
                string operatorName = ResolveOperatorName();
                string relocationNo = await GenerateRelocationNoAsync(ArchiveRegisterDomainValues.MediaKindSimulated, operatedAt.Year);

                ArchiveRelocationExecutionContext context = request.RelocationMode switch
                {
                    ArchiveRelocationMode.PhysicalMove => await ExecuteSimulatedPhysicalMoveAsync(source, request, operatedAt),
                    ArchiveRelocationMode.MergeToExisting => await ExecuteSimulatedContainerMoveAsync(source, request, operatedAt, requireEmptyTarget: false),
                    ArchiveRelocationMode.MoveToEmpty => throw new InvalidOperationException("模拟介质迁档已不支持独立的「迁入空盒」模式，请在物理位置迁移下勾选「迁入空盒」。"),
                    _ => throw new InvalidOperationException($"不支持的迁档模式：{request.RelocationMode}")
                };

                var record = BuildRelocationRecord(
                    relocationNo,
                    ArchiveRegisterDomainValues.MediaKindSimulated,
                    request.RelocationMode,
                    source,
                    context,
                    operatorName,
                    operatedAt,
                    request.Remarks,
                    preview.SummaryText);

                record.Items = context.RelocationItems;
                _relocationRepository.AddRelocationRecord(record);
                await _materialTransactionWriter.AppendRelocationTransactionsAsync(record);
                await _relocationRepository.SaveChangesAsync();

                await MarkSimulatedPendingReturnsAfterRelocationAsync(
                    request.RelocationMode,
                    request.SourceBoxId,
                    context);

                await transaction.CommitAsync();

                return ArchiveRelocationResult.Ok(relocationNo, $"模拟介质迁档完成，共影响 {context.RelocationItems.Count} 条立档事实。");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task MarkSimulatedPendingReturnsAfterRelocationAsync(
            string relocationMode,
            int sourceBoxId,
            ArchiveRelocationExecutionContext context)
        {
            if (string.Equals(relocationMode, ArchiveRelocationMode.PhysicalMove, StringComparison.Ordinal)
                && !string.Equals(
                    context.SourceMediumDisposition,
                    ArchiveRelocationSourceDisposition.BoxRetired,
                    StringComparison.Ordinal))
            {
                await _pendingReturnContainerService.MarkPendingReturnsLocationChangedAsync(
                    sourceBoxId,
                    context.TargetContainerCode,
                    context.TargetStorageLocation);
                return;
            }

            var factIds = context.RelocationItems
                .Select(item => item.FilingFactId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (factIds.Count == 0)
            {
                return;
            }

            await _pendingReturnContainerService.MarkPendingReturnsBoxInvalidAsync(
                factIds,
                context.TargetContainerCode,
                context.TargetStorageLocation);
        }

        public async Task<ArchiveRelocationResult> ExecuteElectronicRelocationAsync(ElectronicRelocationRequest request)
        {
            EnsureArchiveAdmin();
            var preview = await BuildElectronicPreviewAsync(request);
            if (!preview.CanExecute)
            {
                return ArchiveRelocationResult.Fail(preview.BlockReason);
            }

            await using var transaction = await _relocationRepository.BeginTransactionAsync();
            try
            {
                var source = await _relocationRepository.GetElectronicUnitForRelocationAsync(request.SourceUnitId)
                    ?? throw new InvalidOperationException("未找到源电子介质袋。");

                DateTime operatedAt = DateTime.Now;
                string operatorName = ResolveOperatorName();
                string relocationNo = await GenerateRelocationNoAsync(ArchiveRegisterDomainValues.MediaKindElectronic, operatedAt.Year);

                ArchiveRelocationExecutionContext context = request.ExecuteBackupMechanism
                    ? request.RelocationMode switch
                    {
                        ArchiveRelocationMode.MoveToEmpty => await ExecuteElectronicBackupToEmptyAsync(source, request, operatedAt),
                        ArchiveRelocationMode.MergeToExisting => await ExecuteElectronicBackupMergeAsync(source, request, operatedAt),
                        _ => throw new InvalidOperationException($"备份机制不支持迁档模式：{request.RelocationMode}")
                    }
                    : request.RelocationMode switch
                    {
                        ArchiveRelocationMode.PhysicalMove => await ExecuteElectronicPhysicalMoveAsync(source, request, operatedAt),
                        ArchiveRelocationMode.MoveToEmpty => await ExecuteElectronicMoveToEmptyAsync(source, request, operatedAt),
                        ArchiveRelocationMode.MergeToExisting => await ExecuteElectronicContainerMoveAsync(source, request, operatedAt, requireEmptyTarget: false),
                        _ => throw new InvalidOperationException($"不支持的迁档模式：{request.RelocationMode}")
                    };

                var record = BuildRelocationRecord(
                    relocationNo,
                    ArchiveRegisterDomainValues.MediaKindElectronic,
                    request.RelocationMode,
                    source,
                    context,
                    operatorName,
                    operatedAt,
                    request.Remarks,
                    preview.SummaryText);

                record.Items = context.RelocationItems;
                _relocationRepository.AddRelocationRecord(record);
                await _materialTransactionWriter.AppendRelocationTransactionsAsync(record);
                await _relocationRepository.SaveChangesAsync();
                await transaction.CommitAsync();

                return ArchiveRelocationResult.Ok(
                    relocationNo,
                    request.ExecuteBackupMechanism
                        ? $"电子介质资料备份完成，共生成 {context.RelocationItems.Count} 条备份立档事实，原件未改动。"
                        : $"电子介质迁档完成，共影响 {context.RelocationItems.Count} 条立档事实。");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private void EnsureArchiveAdmin()
        {
            if (!_archiveRegisterService.IsArchiveAdminUser(_userContextService.CurrentUser))
            {
                throw new InvalidOperationException("仅资料室管理员可执行资料迁档。");
            }
        }

        private string ResolveOperatorName()
        {
            var user = _userContextService.CurrentUser;
            return string.IsNullOrWhiteSpace(user?.RealName) ? user?.LoginName?.Trim() ?? "资料室管理员" : user.RealName.Trim();
        }

        private async Task<string> GenerateRelocationNoAsync(string mediaKind, int year)
        {
            string prefix = $"迁档-{mediaKind}-{year}-";
            string? lastNo = await _relocationRepository.GetLastRelocationNoByPrefixAsync(prefix);
            int nextSequence = 1;
            if (!string.IsNullOrWhiteSpace(lastNo) && lastNo.Length > prefix.Length
                && int.TryParse(lastNo[prefix.Length..], out int parsed) && parsed > 0)
            {
                nextSequence = parsed + 1;
            }

            return $"{prefix}{nextSequence:D6}";
        }

        private static ArchiveRelocationContainerSummary MapSimulatedSummary(YearlyArchiveBox box)
        {
            return new ArchiveRelocationContainerSummary
            {
                ContainerId = box.Id,
                ContainerCode = box.ArchiveSequenceNo,
                StorageLocation = box.BoxLocationCode,
                ProjectName = box.ProjectName,
                Year = box.Year,
                LifecycleStatus = box.ContainerLifecycleStatus,
                ItemCount = box.MediaItemLinks.Count,
                Items = box.MediaItemLinks
                    .Select(link => new ArchiveRelocationItemSummary
                    {
                        MediaItemId = link.YearlyArchiveRegisterMediaItemId,
                        FormNo = link.MediaItem?.MediaEntry?.RegisterRecord?.FormNo?.Trim() ?? string.Empty,
                        ItemName = link.MediaItem?.ContentDesc?.Trim() ?? string.Empty,
                        ItemType = link.MediaItem?.ItemType?.Trim() ?? string.Empty
                    })
                    .ToList()
            };
        }

        private static string BuildElectronicSourceDisplayText(YearlyElectronicArchiveUnit unit)
        {
            string diskCode = ResolveActiveLinkedMediumCode(unit);
            string diskPart = string.IsNullOrWhiteSpace(diskCode) ? "无关联硬盘" : diskCode;
            return $"{unit.ElectronicArchiveNo} | {unit.StorageLocation} | {unit.StorageCarrierType} | 硬盘：{diskPart} | {unit.MediaItemLinks.Count} 项";
        }

        private static string ResolveActiveLinkedMediumCode(YearlyElectronicArchiveUnit unit)
        {
            if (IsOpticalDiscCarrier(unit.StorageCarrierType))
            {
                return string.Empty;
            }

            var activeLinkCodes = unit.MediumLinks
                .Select(link => link.HardDiskMedium)
                .Where(medium => medium != null && IsActiveDataCarrierMedium(medium))
                .Select(medium => medium!.DiskCode.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (activeLinkCodes.Count == 1)
            {
                return activeLinkCodes[0];
            }

            var linkedCodes = unit.MediumLinks
                .Select(link => link.HardDiskMedium?.DiskCode?.Trim())
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (linkedCodes.Count == 1)
            {
                return linkedCodes[0]!;
            }

            string? itemMediumCode = unit.MediaItemLinks
                .Select(link => link.MediumCode?.Trim())
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .GroupBy(code => code!, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Key)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(itemMediumCode))
            {
                return itemMediumCode;
            }

            var storedCodes = ParseLinkedMediumCodes(unit.LinkedMediumCodes);
            return storedCodes.Count == 1 ? storedCodes[0] : string.Empty;
        }

        private static bool IsActiveDataCarrierMedium(HardDiskMedium medium)
        {
            string status = medium.Ledger?.MediaStatus?.Trim() ?? string.Empty;
            string nature = medium.Ledger?.MediaNature?.Trim() ?? string.Empty;
            return string.Equals(status, HardDiskMedium.StatusInStockData, StringComparison.Ordinal)
                || string.Equals(nature, HardDiskMedium.NatureDataCarrier, StringComparison.Ordinal);
        }

        private static List<string> ParseLinkedMediumCodes(string linkedMediumCodes)
        {
            if (string.IsNullOrWhiteSpace(linkedMediumCodes))
            {
                return [];
            }

            return linkedMediumCodes
                .Split([',', '，', ';', '；', '\r', '\n', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static ArchiveRelocationContainerSummary MapElectronicSummary(YearlyElectronicArchiveUnit unit)
        {
            return new ArchiveRelocationContainerSummary
            {
                ContainerId = unit.Id,
                ContainerCode = unit.ElectronicArchiveNo,
                StorageLocation = unit.StorageLocation,
                ProjectName = unit.ProjectName,
                Year = unit.Year,
                LifecycleStatus = unit.UnitLifecycleStatus,
                StorageCarrierType = unit.StorageCarrierType,
                LinkedMediumCodes = unit.LinkedMediumCodes,
                ActiveLinkedMediumCode = ResolveActiveLinkedMediumCode(unit),
                ItemCount = unit.MediaItemLinks.Count,
                Items = unit.MediaItemLinks
                    .Select(link => new ArchiveRelocationItemSummary
                    {
                        MediaItemId = link.YearlyArchiveRegisterMediaItemId,
                        FormNo = link.FormNo?.Trim() ?? link.MediaItem?.MediaEntry?.RegisterRecord?.FormNo?.Trim() ?? string.Empty,
                        ItemName = link.ItemName?.Trim() ?? link.MediaItem?.ContentDesc?.Trim() ?? string.Empty,
                        ItemType = link.MediaItem?.ItemType?.Trim() ?? string.Empty
                    })
                    .ToList()
            };
        }

        private sealed class ArchiveRelocationExecutionContext
        {
            public int? TargetContainerId { get; init; }

            public string TargetContainerCode { get; init; } = string.Empty;

            public string TargetStorageLocation { get; init; } = string.Empty;

            public string SourceMediumDisposition { get; init; } = ArchiveRelocationSourceDisposition.None;

            public List<YearlyArchiveRelocationItem> RelocationItems { get; } = new();
        }

        private static YearlyArchiveRelocationRecord BuildRelocationRecord(
            string relocationNo,
            string mediaKind,
            string relocationMode,
            YearlyArchiveBox source,
            ArchiveRelocationExecutionContext context,
            string operatorName,
            DateTime operatedAt,
            string remarks,
            string previewReport)
        {
            return new YearlyArchiveRelocationRecord
            {
                RelocationNo = relocationNo,
                MediaKind = mediaKind,
                RelocationMode = relocationMode,
                SourceContainerId = source.Id,
                SourceContainerCode = source.ArchiveSequenceNo,
                SourceStorageLocation = source.BoxLocationCode,
                TargetContainerId = context.TargetContainerId,
                TargetContainerCode = context.TargetContainerCode,
                TargetStorageLocation = context.TargetStorageLocation,
                SourceMediumDisposition = context.SourceMediumDisposition,
                OperatedBy = operatorName,
                OperatedAt = operatedAt,
                Remarks = remarks?.Trim() ?? string.Empty,
                PreviewReport = previewReport
            };
        }

        private static YearlyArchiveRelocationRecord BuildRelocationRecord(
            string relocationNo,
            string mediaKind,
            string relocationMode,
            YearlyElectronicArchiveUnit source,
            ArchiveRelocationExecutionContext context,
            string operatorName,
            DateTime operatedAt,
            string remarks,
            string previewReport)
        {
            return new YearlyArchiveRelocationRecord
            {
                RelocationNo = relocationNo,
                MediaKind = mediaKind,
                RelocationMode = relocationMode,
                SourceContainerId = source.Id,
                SourceContainerCode = source.ElectronicArchiveNo,
                SourceStorageLocation = source.StorageLocation,
                TargetContainerId = context.TargetContainerId,
                TargetContainerCode = context.TargetContainerCode,
                TargetStorageLocation = context.TargetStorageLocation,
                SourceMediumDisposition = context.SourceMediumDisposition,
                OperatedBy = operatorName,
                OperatedAt = operatedAt,
                Remarks = remarks?.Trim() ?? string.Empty,
                PreviewReport = previewReport
            };
        }

        private void UpsertArchiveBoxPlacement(YearlyArchiveBox box, DateTime updatedAt, string updatedBy)
        {
            var placement = _filingRepository.GetArchiveBoxPlacementByCode(box.BoxLocationCode);
            string nowText = updatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            string sourceRecordKey = string.Join("|", box.RegisterRecords.Select(record => record.Id).Distinct().OrderBy(id => id));
            string normalizedPlacementMode = string.Equals(box.PlacementMode, "FrontOut", StringComparison.OrdinalIgnoreCase)
                ? "FrontOut"
                : "SpineOut";
            box.PlacementMode = normalizedPlacementMode;

            if (placement == null)
            {
                _filingRepository.AddArchiveBoxPlacement(new CabinetArchiveBoxPlacement
                {
                    BoxCode = box.BoxLocationCode,
                    BoxSpecification = NormalizeArchiveBoxSpecification(box.Specs),
                    CabinetName = box.CabinetName,
                    FaceCode = box.Side,
                    SlotCode = $"{box.Row}-{box.Column}",
                    PlacementMode = normalizedPlacementMode,
                    SourceType = "YearlyArchive",
                    SourceRecordKey = sourceRecordKey,
                    CreatedAt = nowText,
                    UpdatedAt = nowText,
                    UpdatedBy = updatedBy
                });
                return;
            }

            placement.BoxSpecification = NormalizeArchiveBoxSpecification(box.Specs);
            placement.CabinetName = box.CabinetName;
            placement.FaceCode = box.Side;
            placement.SlotCode = $"{box.Row}-{box.Column}";
            placement.SourceType = "YearlyArchive";
            placement.SourceRecordKey = sourceRecordKey;
            placement.PlacementMode = normalizedPlacementMode;
            placement.UpdatedAt = nowText;
            placement.UpdatedBy = updatedBy;
            if (string.IsNullOrWhiteSpace(placement.CreatedAt))
            {
                placement.CreatedAt = nowText;
            }
        }

        private static string NormalizeArchiveBoxSpecification(string? value)
            => ArchiveBoxSpecificationSupport.Normalize(value);

        private async Task UpdateFilingFactsForLinksAsync(
            string sourceLinkType,
            IReadOnlyCollection<int> linkIds,
            string afterContainerCode,
            string afterStorageLocation,
            int afterContainerId,
            DateTime updatedAt,
            string remark,
            List<YearlyArchiveRelocationItem> relocationItems)
        {
            var facts = await _relocationRepository.GetFilingFactsBySourceLinksAsync(sourceLinkType, linkIds);
            foreach (var fact in facts)
            {
                relocationItems.Add(new YearlyArchiveRelocationItem
                {
                    FilingFactId = fact.Id,
                    SourceLinkId = fact.SourceLinkId,
                    SourceLinkType = fact.SourceLinkType,
                    BeforeContainerCode = string.IsNullOrWhiteSpace(fact.CurrentContainerCode) ? fact.ContainerCode : fact.CurrentContainerCode,
                    BeforeStorageLocation = string.IsNullOrWhiteSpace(fact.CurrentStorageLocation) ? fact.StorageLocation : fact.CurrentStorageLocation,
                    AfterContainerCode = afterContainerCode,
                    AfterStorageLocation = afterStorageLocation
                });

                fact.ContainerId = afterContainerId;
                fact.CurrentContainerCode = afterContainerCode;
                fact.CurrentStorageLocation = afterStorageLocation;
                fact.LifecycleUpdatedAt = updatedAt;
                fact.LifecycleRemark = remark;
            }
        }

        private async Task UpdateFilingFactsForPhysicalMoveAsync(
            string mediaKind,
            int containerId,
            string afterStorageLocation,
            DateTime updatedAt,
            string remark,
            List<YearlyArchiveRelocationItem> relocationItems)
        {
            var facts = await _relocationRepository.GetFilingFactsByContainerAsync(mediaKind, containerId);
            foreach (var fact in facts)
            {
                string beforeLocation = string.IsNullOrWhiteSpace(fact.CurrentStorageLocation)
                    ? fact.StorageLocation
                    : fact.CurrentStorageLocation;
                string beforeCode = string.IsNullOrWhiteSpace(fact.CurrentContainerCode)
                    ? fact.ContainerCode
                    : fact.CurrentContainerCode;

                // 借出中的立档事实也同步当前位置，便于归还按活数据入库；生命周期仍保持 Borrowed。
                relocationItems.Add(new YearlyArchiveRelocationItem
                {
                    FilingFactId = fact.Id,
                    SourceLinkId = fact.SourceLinkId,
                    SourceLinkType = fact.SourceLinkType,
                    BeforeContainerCode = beforeCode,
                    BeforeStorageLocation = beforeLocation,
                    AfterContainerCode = beforeCode,
                    AfterStorageLocation = afterStorageLocation
                });

                fact.CurrentStorageLocation = afterStorageLocation;
                fact.LifecycleUpdatedAt = updatedAt;
                fact.LifecycleRemark = remark;
            }
        }

        private async Task<string> ResolveMoveToEmptyFinalStorageLocationAsync(
            YearlyElectronicArchiveUnit source,
            string requestedLocation)
        {
            string sourceLocation = source.StorageLocation?.Trim() ?? string.Empty;
            requestedLocation = requestedLocation?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(requestedLocation)
                || string.Equals(requestedLocation, sourceLocation, StringComparison.OrdinalIgnoreCase)
                || ArchiveSlotLocationSupport.IsSameSlot(sourceLocation, requestedLocation))
            {
                return sourceLocation;
            }

            if (!ArchiveSlotLocationSupport.TryParseSlotLocation(
                    requestedLocation,
                    out string cabinetName,
                    out string side,
                    out int row,
                    out int column))
            {
                throw new InvalidOperationException("目标存放位置无效。");
            }

            string slotCode = ArchiveSlotLocationSupport.BuildSlotKey(cabinetName, side, row, column);
            string slotPrefix = slotCode + "-";
            var occupiedIndexes = await _filingRepository.GetElectronicUnitSequenceIndexesInSlotAsync(
                slotCode,
                slotPrefix,
                source.Id);
            int minSequence = ArchiveSlotLocationSupport.ResolveMinimumAvailableSequence(occupiedIndexes);
            return ArchiveSlotLocationSupport.BuildFullElectronicLocation(cabinetName, side, row, column, minSequence);
        }

        private async Task UpdateFilingFactsForDiskSwapAsync(
            int containerId,
            string afterStorageLocation,
            string newMediumCode,
            DateTime updatedAt,
            string remark,
            List<YearlyArchiveRelocationItem> relocationItems)
        {
            var facts = await _relocationRepository.GetFilingFactsByContainerAsync(
                ArchiveRegisterDomainValues.MediaKindElectronic,
                containerId);
            foreach (var fact in facts)
            {
                if (string.Equals(fact.LifecycleStatus, FilingFactLifecycleStatus.Borrowed, StringComparison.Ordinal))
                {
                    continue;
                }

                string beforeContainerCode = string.IsNullOrWhiteSpace(fact.CurrentContainerCode)
                    ? fact.ContainerCode
                    : fact.CurrentContainerCode;
                string beforeStorageLocation = string.IsNullOrWhiteSpace(fact.CurrentStorageLocation)
                    ? fact.StorageLocation
                    : fact.CurrentStorageLocation;

                relocationItems.Add(new YearlyArchiveRelocationItem
                {
                    FilingFactId = fact.Id,
                    SourceLinkId = fact.SourceLinkId,
                    SourceLinkType = fact.SourceLinkType,
                    BeforeContainerCode = beforeContainerCode,
                    BeforeStorageLocation = beforeStorageLocation,
                    AfterContainerCode = beforeContainerCode,
                    AfterStorageLocation = afterStorageLocation
                });

                fact.CurrentStorageLocation = afterStorageLocation;
                if (!string.IsNullOrWhiteSpace(newMediumCode))
                {
                    fact.MediumCode = newMediumCode;
                }

                fact.LifecycleUpdatedAt = updatedAt;
                fact.LifecycleRemark = remark;
            }
        }

        private static bool IsHardDiskCarrier(string storageCarrierType)
            => storageCarrierType.Contains("硬盘", StringComparison.Ordinal);

        private static bool IsOpticalDiscCarrier(string storageCarrierType)
            => storageCarrierType.Contains("光盘", StringComparison.Ordinal);

        /// <summary>
        /// 解析电子介质袋当前物理位置。光盘袋在开柜界面以关联光盘台账位置为准，避免袋位置与台账漂移导致迁档误判。
        /// </summary>
        private static string ResolveElectronicUnitPhysicalStorageLocation(YearlyElectronicArchiveUnit unit)
        {
            ArgumentNullException.ThrowIfNull(unit);

            string unitLocation = unit.StorageLocation?.Trim() ?? string.Empty;
            if (!IsOpticalDiscCarrier(unit.StorageCarrierType))
            {
                return unitLocation;
            }

            string? ledgerLocation = unit.DiscLinks
                .Select(link => link.OpticalDiscMedium?.Ledger)
                .Where(ledger => ledger != null
                    && string.Equals(ledger.MediaStatus, OpticalDiscMedium.StatusInStock, StringComparison.Ordinal))
                .Select(ledger => ledger!.StorageLocation?.Trim())
                .FirstOrDefault(location => !string.IsNullOrWhiteSpace(location));

            return string.IsNullOrWhiteSpace(ledgerLocation) ? unitLocation : ledgerLocation;
        }

        private static bool IsHardDiskMergeTargetUnit(YearlyElectronicArchiveUnit unit)
        {
            ArgumentNullException.ThrowIfNull(unit);

            return IsHardDiskCarrier(unit.StorageCarrierType)
                && !IsOpticalDiscCarrier(unit.StorageCarrierType)
                && unit.MediaItemLinks.Count > 0;
        }

        private static void EnsureHardDiskMergeTargetUnit(YearlyElectronicArchiveUnit target)
        {
            ArgumentNullException.ThrowIfNull(target);

            if (IsHardDiskMergeTargetUnit(target))
            {
                return;
            }

            if (IsOpticalDiscCarrier(target.StorageCarrierType))
            {
                throw new InvalidOperationException("并入同项目硬盘模式下，目标不能为光盘介质袋（光盘不可二次写入）。");
            }

            if (target.MediaItemLinks.Count == 0)
            {
                throw new InvalidOperationException("并入同项目硬盘模式下，目标应为本项目已用硬盘袋。");
            }

            throw new InvalidOperationException("并入同项目硬盘模式下，目标必须为已用的硬盘介质袋。");
        }

        private static string? ValidateHardDiskMergeTargetUnit(YearlyElectronicArchiveUnit target)
        {
            ArgumentNullException.ThrowIfNull(target);

            if (IsOpticalDiscCarrier(target.StorageCarrierType))
            {
                return "并入同项目硬盘模式下，目标不能为光盘介质袋（光盘不可二次写入）。";
            }

            if (target.MediaItemLinks.Count == 0)
            {
                return "并入同项目硬盘模式下，目标应为本项目已用硬盘袋。";
            }

            if (!IsHardDiskCarrier(target.StorageCarrierType))
            {
                return "并入同项目硬盘模式下，目标必须为已用的硬盘介质袋。";
            }

            return null;
        }

        private void SyncLinkedHardDiskLedgerStorageLocation(
            YearlyElectronicArchiveUnit unit,
            string newStorageLocation,
            DateTime operatedAt,
            string remark,
            string operatorName)
        {
            if (!IsHardDiskCarrier(unit.StorageCarrierType))
            {
                return;
            }

            string normalizedLocation = newStorageLocation.Trim();
            string relatedBatch = unit.ElectronicArchiveNo.Trim();
            string relatedArchiveTitle = string.IsNullOrWhiteSpace(unit.ContentSummary)
                ? relatedBatch
                : unit.ContentSummary.Trim();

            foreach (var mediumLink in unit.MediumLinks)
            {
                var medium = mediumLink.HardDiskMedium;
                if (medium == null || !IsActiveDataCarrierMedium(medium) || medium.Ledger == null)
                {
                    continue;
                }

                var before = HardDiskLedgerSyncSupport.CaptureSnapshot(medium);
                if (HardDiskLedgerSyncSupport.IsSameFullLocation(before.Location, normalizedLocation))
                {
                    continue;
                }

                medium.Ledger.StorageLocation = normalizedLocation;
                medium.Ledger.UpdatedTime = operatedAt;
                medium.UpdatedTime = operatedAt;
                medium.Remark = string.Join("；",
                    new[] { medium.Remark?.Trim(), remark }.Where(value => !string.IsNullOrWhiteSpace(value)));

                _filingRepository.AddHardDiskMediaTransaction(
                    HardDiskLedgerSyncSupport.BuildSyncTransaction(
                        medium,
                        before,
                        operatorName,
                        operatedAt,
                        remark,
                        "资料迁档：同步更新关联硬盘台账存放位置",
                        relatedBatch,
                        relatedArchiveTitle));
            }
        }

        private void SyncLinkedOpticalDiscLedgerStorageLocation(
            YearlyElectronicArchiveUnit unit,
            string newStorageLocation,
            DateTime operatedAt,
            string remark,
            string operatorName)
        {
            if (!IsOpticalDiscCarrier(unit.StorageCarrierType))
            {
                return;
            }

            string normalizedLocation = newStorageLocation.Trim();
            string relatedBatch = unit.ElectronicArchiveNo.Trim();
            string relatedArchiveTitle = string.IsNullOrWhiteSpace(unit.ContentSummary)
                ? relatedBatch
                : unit.ContentSummary.Trim();

            foreach (var discLink in unit.DiscLinks)
            {
                var disc = discLink.OpticalDiscMedium;
                if (disc == null || disc.Ledger == null)
                {
                    continue;
                }

                if (!string.Equals(disc.Ledger.MediaStatus, OpticalDiscMedium.StatusInStock, StringComparison.Ordinal))
                {
                    continue;
                }

                var before = OpticalDiscLedgerSyncSupport.CaptureSnapshot(disc);
                if (HardDiskLedgerSyncSupport.IsSameFullLocation(before.Location, normalizedLocation))
                {
                    continue;
                }

                disc.Ledger.StorageLocation = normalizedLocation;
                disc.Ledger.UpdatedTime = operatedAt;
                disc.UpdatedTime = operatedAt;

                if (OpticalDiscLedgerSyncSupport.HasLedgerMaterialChange(before, disc.Ledger))
                {
                    disc.Transactions.Add(new OpticalDiscMediaTransaction
                    {
                        Medium = disc,
                        TransactionType = OpticalDiscMediaTransaction.TypeRelocate,
                        BusinessNo = relatedBatch,
                        BeforeStatus = before.Status,
                        AfterStatus = disc.Ledger.MediaStatus?.Trim() ?? string.Empty,
                        BeforeLocation = before.Location,
                        AfterLocation = normalizedLocation,
                        OperatorName = operatorName,
                        OperateTime = operatedAt,
                        RelatedBatch = relatedBatch,
                        RelatedArchiveTitle = relatedArchiveTitle,
                        Description = "资料迁档：同步更新关联光盘台账存放位置",
                        Remark = remark
                    });
                }
            }
        }

        private void FormatHardDiskMediumToBlank(
            HardDiskMedium medium,
            string targetLocation,
            DateTime operatedAt,
            string remark,
            string operatorName,
            string relatedBatch,
            string relatedArchiveTitle)
        {
            var before = HardDiskLedgerSyncSupport.CaptureSnapshot(medium);

            var ledger = medium.Ledger ?? new HardDiskLedger
            {
                MediumId = medium.Id,
                CreatedTime = operatedAt
            };

            if (medium.Ledger == null)
            {
                medium.Ledger = ledger;
            }

            ledger.MediaStatus = HardDiskMedium.StatusInStockBlank;
            ledger.MediaNature = HardDiskMedium.NatureBlank;
            ledger.HolderOrOrganization = "资料室";
            ledger.StorageLocation = HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(targetLocation);
            ledger.NeedReturn = false;
            ledger.UpdatedTime = operatedAt;
            medium.RegisterLock = null;
            medium.Remark = string.Join("；",
                new[] { medium.Remark?.Trim(), remark }.Where(value => !string.IsNullOrWhiteSpace(value)));
            medium.UpdatedTime = operatedAt;

            if (HardDiskLedgerSyncSupport.HasLedgerMaterialChange(before, ledger))
            {
                _filingRepository.AddHardDiskMediaTransaction(
                    HardDiskLedgerSyncSupport.BuildSyncTransaction(
                        medium,
                        before,
                        operatorName,
                        operatedAt,
                        remark,
                        "资料迁档：原硬盘格式化后归位空盘档口",
                        relatedBatch,
                        relatedArchiveTitle));
            }
        }

        private static void MarkOpticalDiscDestroyed(OpticalDiscMedium disc, DateTime operatedAt, string remark, string operatorName)
        {
            disc.Remarks = string.Join("；",
                new[] { disc.Remarks?.Trim(), remark }.Where(value => !string.IsNullOrWhiteSpace(value)));
            disc.UpdatedTime = operatedAt;

            var ledger = disc.Ledger ??= new OpticalDiscLedger
            {
                MediumId = disc.Id,
                DiscCode = disc.DiscCode,
                CreatedTime = operatedAt
            };
            string beforeStatus = ledger.MediaStatus;
            string beforeLocation = ledger.StorageLocation;
            ledger.MediaStatus = OpticalDiscMedium.StatusDestroyed;
            ledger.HolderOrOrganization = "资料室";
            ledger.UpdatedTime = operatedAt;

            disc.Transactions.Add(new OpticalDiscMediaTransaction
            {
                Medium = disc,
                TransactionType = OpticalDiscMediaTransaction.TypeDestroy,
                BeforeStatus = beforeStatus,
                AfterStatus = ledger.MediaStatus,
                BeforeLocation = beforeLocation,
                AfterLocation = beforeLocation,
                OperatorName = operatorName,
                OperateTime = operatedAt,
                TargetOrganization = "资料室",
                Description = "资料迁档销毁原光盘",
                Remark = remark
            });
        }
    }
}
