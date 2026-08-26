using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.Shared;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DocMgr.Services.Cabinets
{
    public partial class CabinetOpenLayoutService : ICabinetOpenLayoutService
    {
        private const int MagneticDiskSlotCapacity = 10;
        private const int MagneticDiskOpticalDiscSlotCapacity = 20;
        private readonly ICabinetOpenLayoutRepository _cabinetOpenLayoutRepository;

        public CabinetOpenLayoutService(ICabinetOpenLayoutRepository cabinetOpenLayoutRepository)
        {
            _cabinetOpenLayoutRepository = cabinetOpenLayoutRepository;
        }

        public IReadOnlyList<CabinetSlotDescriptor> BuildSlots(CabinetOpenRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.LayerCount <= 0 || request.ColumnCount <= 0)
            {
                return Array.Empty<CabinetSlotDescriptor>();
            }

            string normalizedCabinetName = CabinetNameNormalizer.Normalize(request.CabinetName);
            var slots = new List<CabinetSlotDescriptor>(request.LayerCount * request.ColumnCount);
            var assignments = EnumerateArchiveAssignments()
                .Where(assignment => assignment.Parsed.CabinetName == normalizedCabinetName)
                .Where(assignment => assignment.Parsed.LayerIndex >= 1 && assignment.Parsed.LayerIndex <= request.LayerCount)
                .Where(assignment => assignment.Parsed.ColumnIndex >= 1 && assignment.Parsed.ColumnIndex <= request.ColumnCount)
                .ToList();
            var placementLookup = LoadPlacementLookup(normalizedCabinetName);
            var boxSpecificationLookup = _cabinetOpenLayoutRepository.GetArchiveBoxSpecificationLookup();
            var slotSpecification = _cabinetOpenLayoutRepository.GetCabinetSlotSpecification(GetCabinetTypeCode(request.CabinetType));
            var (slotCanvasWidth, slotCanvasHeight) = ResolveSlotCanvasSize(request, slotSpecification);
            var yearlyBoxIds = assignments
                .Where(assignment => assignment.YearlyArchiveBoxId is > 0)
                .Select(assignment => assignment.YearlyArchiveBoxId!.Value)
                .Distinct()
                .ToList();
            var pendingReturnByBoxId = BuildSimulatedBoxPendingReturnLookup(yearlyBoxIds);
            var inventoryMarkByBoxId = BuildSimulatedBoxInventoryMarkLookup(yearlyBoxIds);
            var activeWithdrawalLockByBoxId = yearlyBoxIds.Count == 0
                ? new Dictionary<int, CabinetOccupationLockDescriptor>()
                : _cabinetOpenLayoutRepository.GetActiveWithdrawalLocksByArchiveBoxIds(yearlyBoxIds);

            if (request.CabinetType == CabinetType.MagneticDisk)
            {
                return BuildMagneticDiskSlots(request, normalizedCabinetName, slotCanvasWidth, slotCanvasHeight);
            }

            var archiveBoxesBySlot = BuildArchiveBoxesBySlot(request, assignments, placementLookup, boxSpecificationLookup, slotCanvasWidth, slotCanvasHeight, pendingReturnByBoxId, inventoryMarkByBoxId, activeWithdrawalLockByBoxId);
            var slotMetricsBySlot = BuildSlotMetricsBySlot(request, assignments, placementLookup, boxSpecificationLookup, slotCanvasWidth);
            var archiveCategoryLookup = request.CabinetType == CabinetType.Standard
                ? _cabinetOpenLayoutRepository.GetArchiveSlotCategoryLookup(request.CabinetId)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int visualRowIndex = 0; visualRowIndex < request.LayerCount; visualRowIndex++)
            {
                int layerIndex = request.LayerCount - visualRowIndex;

                for (int visualColumnIndex = 0; visualColumnIndex < request.ColumnCount; visualColumnIndex++)
                {
                    int columnIndex = visualColumnIndex + 1;
                    string slotCode = $"{layerIndex}-{columnIndex}";

                    slotMetricsBySlot.TryGetValue(slotCode, out var metrics);
                    archiveBoxesBySlot.TryGetValue(slotCode, out var archiveBoxes);
                    archiveBoxes ??= [];
                    string slotToolTipText = AppendSimulatedBoxPendingReturnHint(
                        metrics?.ToolTipText ?? string.Empty,
                        archiveBoxes);
                    string dedicatedSlotCategoryName = archiveCategoryLookup.TryGetValue($"{request.Face}:{slotCode}", out var categoryName)
                        ? CabinetArchiveSlotCategoryAssignment.NormalizeCategoryName(categoryName)
                        : string.Empty;
                    bool isYearlyMaterialsSlot = CabinetArchiveSlotCategoryAssignment.MatchesCategory(
                        dedicatedSlotCategoryName,
                        CabinetArchiveSlotCategoryAssignment.CategoryYearlyMaterials);
                    bool isHistoricalMaterialsSlot = CabinetArchiveSlotCategoryAssignment.MatchesCategory(
                        dedicatedSlotCategoryName,
                        CabinetArchiveSlotCategoryAssignment.CategoryHistoricalMaterials);
                    bool isMixedUseArchiveSlot = CabinetArchiveSlotCategoryAssignment.MatchesCategory(
                        dedicatedSlotCategoryName,
                        CabinetArchiveSlotCategoryAssignment.CategoryMixed);

                    slots.Add(new CabinetSlotDescriptor
                    {
                        VisualRowIndex = visualRowIndex,
                        VisualColumnIndex = visualColumnIndex,
                        LayerIndex = layerIndex,
                        ColumnIndex = columnIndex,
                        SlotCode = slotCode,
                        Face = request.Face,
                        ArchiveBoxes = archiveBoxes,
                        SlotCanvasWidth = slotCanvasWidth,
                        SlotCanvasHeight = slotCanvasHeight,
                        UtilizationRatio = metrics?.UtilizationRatio ?? 0d,
                        UtilizationText = metrics?.UtilizationText ?? "0%",
                        CapacitySummaryText = metrics?.CapacitySummaryText ?? "已放 0盒",
                        RemainingSummaryText = metrics?.RemainingSummaryText ?? "余 0cm",
                        LayoutModeText = metrics?.LayoutModeText ?? string.Empty,
                        SlotToolTipText = slotToolTipText,
                        IsCrossFaceLinked = metrics?.IsCrossFaceLinked ?? false,
                        IsSpecialRule = metrics?.IsSpecialRule ?? false,
                        SpecialRuleText = metrics?.SpecialRuleText ?? string.Empty,
                        IsYearlyMaterialsDedicatedSlot = isYearlyMaterialsSlot,
                        IsHistoricalMaterialsDedicatedSlot = isHistoricalMaterialsSlot,
                        IsMixedUseArchiveSlot = isMixedUseArchiveSlot,
                        DedicatedSlotCategoryName = dedicatedSlotCategoryName
                    });
                }
            }

            return slots;
        }

        private IReadOnlyList<CabinetSlotDescriptor> BuildMagneticDiskSlots(CabinetOpenRequest request, string normalizedCabinetName, double slotCanvasWidth, double slotCanvasHeight)
        {
            var slots = new List<CabinetSlotDescriptor>(request.LayerCount * request.ColumnCount);
            var cabinet = _cabinetOpenLayoutRepository.GetCabinetByIdOrName(request.CabinetId, normalizedCabinetName);
            var categoryLookup = _cabinetOpenLayoutRepository.GetHardDiskSlotCategoryLookup(request.CabinetId);
            var inStockMediaBySlot = new Dictionary<string, List<CabinetHardDiskMediumDescriptor>>(StringComparer.OrdinalIgnoreCase);
            var pendingReturnMediaBySlot = new Dictionary<string, List<CabinetHardDiskMediumDescriptor>>(StringComparer.OrdinalIgnoreCase);
            var media = _cabinetOpenLayoutRepository.GetHardDiskMediaWithLedger();
            var mediumArchiveLookup = LoadMediumArchiveLookup(media.Select(item => item.Id));
            var electronicUnitIds = mediumArchiveLookup.Values
                .Select(context => context.ElectronicArchiveUnitId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            var activeWithdrawalLockByUnitId = electronicUnitIds.Count == 0
                ? new Dictionary<int, CabinetOccupationLockDescriptor>()
                : _cabinetOpenLayoutRepository.GetActiveWithdrawalLocksByElectronicUnitIds(electronicUnitIds);

            var pendingMediumIds = media
                .Where(item => item.Ledger != null)
                .Where(item => !IsInStockStatus(item.Ledger!.MediaStatus) && item.Ledger.NeedReturn)
                .Select(item => item.Id)
                .ToHashSet();

            var latestTransactions = _cabinetOpenLayoutRepository.GetHardDiskMediaTransactionsByMediumIds(pendingMediumIds.ToList())
                .GroupBy(item => item.MediumId)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.OperateTime).First());

            var opticalDiscMedia = _cabinetOpenLayoutRepository.GetOpticalDiscMediaWithLedger();
            var opticalDiscArchiveLookup = LoadOpticalDiscArchiveLookup(opticalDiscMedia.Select(item => item.Id));
            var opticalElectronicUnitIds = opticalDiscArchiveLookup.Values
                .Select(context => context.ElectronicArchiveUnitId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            var allElectronicUnitIds = electronicUnitIds
                .Concat(opticalElectronicUnitIds)
                .Distinct()
                .ToList();
            if (allElectronicUnitIds.Count > electronicUnitIds.Count)
            {
                activeWithdrawalLockByUnitId = _cabinetOpenLayoutRepository.GetActiveWithdrawalLocksByElectronicUnitIds(allElectronicUnitIds);
            }
            var usedDataSizeLookup = LoadUsedDataSizeLookup(media, opticalDiscMedia, mediumArchiveLookup, opticalDiscArchiveLookup);

            var inStockMediumIds = media
                .Where(item => item.Ledger != null && IsInStockStatus(item.Ledger.MediaStatus))
                .Select(item => item.Id)
                .ToList();
            var activeOutboundApplicationLockByMediumId = inStockMediumIds.Count == 0
                ? new Dictionary<int, CabinetOccupationLockDescriptor>()
                : _cabinetOpenLayoutRepository.GetActiveOutboundApplicationLocksByMediumIds(inStockMediumIds);

            var pendingOpticalDiscIds = opticalDiscMedia
                .Where(item => item.Ledger != null)
                .Where(item => !IsOpticalDiscInStockStatus(item.Ledger!.MediaStatus) && item.Ledger.NeedReturn)
                .Select(item => item.Id)
                .ToHashSet();

            var latestOpticalDiscTransactions = _cabinetOpenLayoutRepository.GetOpticalDiscMediaTransactionsByMediumIds(pendingOpticalDiscIds.ToList())
                .GroupBy(item => item.MediumId)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.OperateTime).First());

            foreach (var medium in media)
            {
                if (medium.Ledger == null)
                {
                    continue;
                }

                if (IsInStockStatus(medium.Ledger.MediaStatus))
                {
                    if (!TryParseMagneticDiskLocation(medium.Ledger.StorageLocation, out var location) ||
                        !IsMatchingLocation(location, normalizedCabinetName, request.Face, request.LayerCount, request.ColumnCount))
                    {
                        continue;
                    }

                    MediumArchiveContext? archiveContext = mediumArchiveLookup.TryGetValue(medium.Id, out var loadedContext)
                        ? loadedContext
                        : null;

                    // 盘失/拟销且未挂电子袋：不占柜展示（硬盘盘库盘失已清空位置；资料盘库必挂袋）。
                    if ((string.Equals(
                            NormalizeStatusText(medium.Ledger.MediaStatus),
                            NormalizeStatusText(HardDiskMedium.StatusInStockLost),
                            StringComparison.OrdinalIgnoreCase)
                        || string.Equals(
                            NormalizeStatusText(medium.Ledger.MediaStatus),
                            NormalizeStatusText(HardDiskMedium.StatusInStockScrap),
                            StringComparison.OrdinalIgnoreCase))
                        && archiveContext == null)
                    {
                        continue;
                    }

                    var outboundApplicationLock = medium.RegisterLock == null
                        ? activeOutboundApplicationLockByMediumId.GetValueOrDefault(medium.Id, CabinetOccupationLockDescriptor.Empty)
                        : CabinetOccupationLockDescriptor.Empty;
                    AddMedium(
                        inStockMediaBySlot,
                        location.SlotCode,
                        CreateHardDiskDescriptor(
                            medium,
                            false,
                            archiveContext,
                            usedDataSizeLookup,
                            ResolveElectronicUnitWithdrawalLock(archiveContext, activeWithdrawalLockByUnitId),
                            outboundApplicationLock));
                    continue;
                }

                if (!medium.Ledger.NeedReturn || !latestTransactions.TryGetValue(medium.Id, out var latestTransaction))
                {
                    continue;
                }

                if (!TryParseMagneticDiskLocation(latestTransaction.BeforeLocation, out var sourceLocation) ||
                    !IsMatchingLocation(sourceLocation, normalizedCabinetName, request.Face, request.LayerCount, request.ColumnCount))
                {
                    continue;
                }

                MediumArchiveContext? pendingArchiveContext = mediumArchiveLookup.TryGetValue(medium.Id, out var loadedPendingContext)
                    ? loadedPendingContext
                    : null;
                AddMedium(pendingReturnMediaBySlot, sourceLocation.SlotCode, CreateHardDiskDescriptor(medium, true, pendingArchiveContext, usedDataSizeLookup, ResolveElectronicUnitWithdrawalLock(pendingArchiveContext, activeWithdrawalLockByUnitId)));
            }

            foreach (var opticalDiscMedium in opticalDiscMedia)
            {
                if (opticalDiscMedium.Ledger == null)
                {
                    continue;
                }

                MediumArchiveContext? opticalDiscArchiveContext = opticalDiscArchiveLookup.TryGetValue(opticalDiscMedium.Id, out var loadedOpticalDiscContext)
                    ? loadedOpticalDiscContext
                    : null;

                if (IsOpticalDiscInStockStatus(opticalDiscMedium.Ledger.MediaStatus))
                {
                    if (!TryParseMagneticDiskLocation(opticalDiscMedium.Ledger.StorageLocation, out var location)
                        || !IsMatchingLocation(location, normalizedCabinetName, request.Face, request.LayerCount, request.ColumnCount))
                    {
                        continue;
                    }

                    if ((string.Equals(
                            NormalizeStatusText(opticalDiscMedium.Ledger.MediaStatus),
                            NormalizeStatusText(OpticalDiscMedium.StatusLost),
                            StringComparison.OrdinalIgnoreCase)
                        || string.Equals(
                            NormalizeStatusText(opticalDiscMedium.Ledger.MediaStatus),
                            NormalizeStatusText(OpticalDiscMedium.StatusScrap),
                            StringComparison.OrdinalIgnoreCase))
                        && opticalDiscArchiveContext == null)
                    {
                        continue;
                    }

                    AddMedium(
                        inStockMediaBySlot,
                        location.SlotCode,
                        CreateOpticalDiscDescriptor(
                            opticalDiscMedium,
                            false,
                            opticalDiscArchiveContext,
                            usedDataSizeLookup,
                            ResolveElectronicUnitWithdrawalLock(opticalDiscArchiveContext, activeWithdrawalLockByUnitId)));
                    continue;
                }

                if (!opticalDiscMedium.Ledger.NeedReturn || !latestOpticalDiscTransactions.TryGetValue(opticalDiscMedium.Id, out var latestOpticalDiscTransaction))
                {
                    continue;
                }

                if (!TryParseMagneticDiskLocation(latestOpticalDiscTransaction.BeforeLocation, out var sourceLocation)
                    || !IsMatchingLocation(sourceLocation, normalizedCabinetName, request.Face, request.LayerCount, request.ColumnCount))
                {
                    continue;
                }

                AddMedium(pendingReturnMediaBySlot, sourceLocation.SlotCode, CreateOpticalDiscDescriptor(opticalDiscMedium, true, opticalDiscArchiveContext, usedDataSizeLookup, ResolveElectronicUnitWithdrawalLock(opticalDiscArchiveContext, activeWithdrawalLockByUnitId)));
            }

            for (int visualRowIndex = 0; visualRowIndex < request.LayerCount; visualRowIndex++)
            {
                int layerIndex = request.LayerCount - visualRowIndex;

                for (int visualColumnIndex = 0; visualColumnIndex < request.ColumnCount; visualColumnIndex++)
                {
                    int columnIndex = visualColumnIndex + 1;
                    string slotCode = $"{layerIndex}-{columnIndex}";
                    var slotMedia = inStockMediaBySlot.TryGetValue(slotCode, out var presentMedia)
                        ? presentMedia.OrderBy(item => item.DiskCode, StringComparer.OrdinalIgnoreCase).ToList()
                        : [];
                    var pendingMedia = pendingReturnMediaBySlot.TryGetValue(slotCode, out var returningMedia)
                        ? returningMedia.OrderBy(item => item.DiskCode, StringComparer.OrdinalIgnoreCase).ToList()
                        : [];
                    string dedicatedSlotCategoryName = categoryLookup.TryGetValue($"{request.Face}:{slotCode}", out var categoryName)
                        ? CabinetHardDiskSlotCategoryAssignment.NormalizeCategoryName(categoryName)
                        : string.Empty;
                    bool isDamagedSlot = CabinetHardDiskSlotCategoryAssignment.MatchesCategory(dedicatedSlotCategoryName, CabinetHardDiskSlotCategoryAssignment.CategoryDamaged);
                    bool isDamagedOpticalDiscSlot = CabinetHardDiskSlotCategoryAssignment.MatchesCategory(dedicatedSlotCategoryName, CabinetHardDiskSlotCategoryAssignment.CategoryDamagedOpticalDisc);
                    bool isDataSlot = CabinetHardDiskSlotCategoryAssignment.MatchesCategory(dedicatedSlotCategoryName, CabinetHardDiskSlotCategoryAssignment.CategoryData);
                    bool isDataOpticalDiscSlot = CabinetHardDiskSlotCategoryAssignment.MatchesCategory(dedicatedSlotCategoryName, CabinetHardDiskSlotCategoryAssignment.CategoryDataOpticalDisc);
                    bool isHistoricalDataSlot = CabinetHardDiskSlotCategoryAssignment.MatchesCategory(dedicatedSlotCategoryName, CabinetHardDiskSlotCategoryAssignment.CategoryHistoricalDataHardDisk);
                    bool isHistoricalDataOpticalDiscSlot = CabinetHardDiskSlotCategoryAssignment.MatchesCategory(dedicatedSlotCategoryName, CabinetHardDiskSlotCategoryAssignment.CategoryHistoricalDataOpticalDisc);
                    bool isBlankSlot = CabinetHardDiskSlotCategoryAssignment.MatchesCategory(dedicatedSlotCategoryName, CabinetHardDiskSlotCategoryAssignment.CategoryBlank);
                    int slotCapacity = CabinetHardDiskSlotCategoryAssignment.ResolveDedicatedSlotCapacity(dedicatedSlotCategoryName);
                    slotMedia = OrderSlotMedia(slotMedia);
                    var metrics = BuildMagneticDiskSlotMetrics(slotCode, slotMedia, pendingMedia, dedicatedSlotCategoryName, slotCapacity);

                    slots.Add(new CabinetSlotDescriptor
                    {
                        VisualRowIndex = visualRowIndex,
                        VisualColumnIndex = visualColumnIndex,
                        LayerIndex = layerIndex,
                        ColumnIndex = columnIndex,
                        SlotCode = slotCode,
                        Face = request.Face,
                        SlotCanvasWidth = slotCanvasWidth,
                        SlotCanvasHeight = slotCanvasHeight,
                        IsMagneticDiskSlot = true,
                        HardDiskCapacity = slotCapacity,
                        HardDiskMedia = slotMedia,
                        PendingReturnMedia = pendingMedia,
                        UtilizationRatio = metrics.UtilizationRatio,
                        UtilizationText = metrics.UtilizationText,
                        CapacitySummaryText = metrics.CapacitySummaryText,
                        RemainingSummaryText = metrics.RemainingSummaryText,
                        LayoutModeText = metrics.LayoutModeText,
                        SlotToolTipText = metrics.ToolTipText,
                        IsCrossFaceLinked = false,
                        IsSpecialRule = false,
                        SpecialRuleText = string.Empty,
                        IsDamagedDiskDedicatedSlot = isDamagedSlot,
                        IsDamagedOpticalDiscDedicatedSlot = isDamagedOpticalDiscSlot,
                        IsDataDiskDedicatedSlot = isDataSlot,
                        IsDataOpticalDiscDedicatedSlot = isDataOpticalDiscSlot,
                        IsHistoricalDataDiskDedicatedSlot = isHistoricalDataSlot,
                        IsHistoricalDataOpticalDiscDedicatedSlot = isHistoricalDataOpticalDiscSlot,
                        IsBlankDiskDedicatedSlot = isBlankSlot,
                        DedicatedSlotCategoryName = dedicatedSlotCategoryName
                    });
                }
            }

            return slots;
        }

        public IReadOnlyList<CabinetArchiveBoxContentDescriptor> GetArchiveBoxContents(string boxCode)
        {
            if (string.IsNullOrWhiteSpace(boxCode))
            {
                return Array.Empty<CabinetArchiveBoxContentDescriptor>();
            }

            var normalizedBoxCode = boxCode.Trim();

            return EnumerateArchiveAssignments()
                .Where(assignment => string.Equals(assignment.Parsed.BoxCode, normalizedBoxCode, StringComparison.OrdinalIgnoreCase))
                .OrderBy(assignment => assignment.SourceSortOrder)
                .ThenBy(assignment => assignment.IdentifierText)
                .ThenBy(assignment => assignment.TitleText)
                .Select(assignment => new CabinetArchiveBoxContentDescriptor
                {
                    BoxCode = assignment.Parsed.BoxCode,
                    SourceType = assignment.SourceType,
                    CategoryText = assignment.CategoryText,
                    HistoryCategoryText = ResolveHistoryCategoryText(assignment),
                    IdentifierText = assignment.IdentifierText,
                    TitleText = assignment.TitleText,
                    QuantityText = assignment.QuantityText,
                    DetailText = assignment.DetailText,
                    DateText = assignment.DateText,
                    BoxSpecs = assignment.BoxSpecification ?? string.Empty,
                    IsMixedPlacement = assignment.IsMixedPlacement,
                    OriginalBoxNumberText = assignment.SourceBoxNumberText,
                    RelatedBoxCodesText = string.Join("；", assignment.RelatedBoxCodes),
                    RelatedBoxCount = assignment.RelatedBoxCodes.Count,
                    PlacementNote = assignment.IsMixedPlacement
                        ? "该批资料登记时涉及多个档案盒，当前未细化到具体盒。以下记录为关联记录，不代表已精确归属本盒。"
                        : string.Empty,
                    ViewMode = CabinetArchiveContainerViewMode.HistoryArchiveBox
                })
                .ToList();
        }

        private static string ResolveHistoryCategoryText(ExpandedArchiveBoxAssignment assignment)
        {
            if (string.Equals(assignment.SourceType, "航摄影像", StringComparison.OrdinalIgnoreCase)
                || string.Equals(assignment.SourceType, "其他图件", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(assignment.SortCategory)
                    ? string.Empty
                    : assignment.SortCategory.Trim();
            }

            return string.Empty;
        }

        private Dictionary<string, CabinetArchiveBoxPlacement> LoadPlacementLookup(string cabinetName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(cabinetName);
            return _cabinetOpenLayoutRepository.GetPlacementLookup(cabinetName);
        }

        /// <summary>
        /// 模拟介质档案盒待还份数：汇总盒内模拟介质子项的出库待还份数（支持部分提档）；电子介质整件借出，不计入盒级份数待还。
        /// </summary>
        private Dictionary<int, int> BuildSimulatedBoxPendingReturnLookup(IReadOnlyCollection<int> boxIds)
        {
            if (boxIds.Count == 0)
            {
                return new Dictionary<int, int>();
            }

            var lookup = new Dictionary<int, int>();
            foreach (var box in _cabinetOpenLayoutRepository.GetYearlyArchiveBoxesByIds(boxIds))
            {
                var simulatedRows = _cabinetOpenLayoutRepository.GetYearlyArchiveBoxMediaItemRows(box)
                    .Where(row => string.Equals(
                        row.Fact.MediaKind,
                        ArchiveRegisterDomainValues.MediaKindSimulated,
                        StringComparison.Ordinal))
                    .ToList();
                if (simulatedRows.Count == 0)
                {
                    continue;
                }

                var totals = ArchiveSimulatedBoxSlotOccupancySupport.AggregateRows(simulatedRows);
                if (totals.PendingReturn > 0)
                {
                    lookup[box.Id] = totals.PendingReturn;
                }
            }

            return lookup;
        }

        /// <summary>
        /// 模拟档案盒盘库标识：空（盘库致空仍占格）/ 失（部分丢失）。
        /// </summary>
        private Dictionary<int, string> BuildSimulatedBoxInventoryMarkLookup(IReadOnlyCollection<int> boxIds)
        {
            if (boxIds.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            var lookup = new Dictionary<int, string>();
            foreach (var box in _cabinetOpenLayoutRepository.GetYearlyArchiveBoxesByIds(boxIds))
            {
                var simulatedRows = _cabinetOpenLayoutRepository.GetYearlyArchiveBoxMediaItemRows(box)
                    .Where(row => string.Equals(
                        row.Fact.MediaKind,
                        ArchiveRegisterDomainValues.MediaKindSimulated,
                        StringComparison.Ordinal))
                    .ToList();
                if (simulatedRows.Count == 0)
                {
                    continue;
                }

                var totals = ArchiveSimulatedBoxSlotOccupancySupport.AggregateRows(simulatedRows);
                string mark = CabinetOpenStatusBadgeSupport.BuildSimulatedInventoryMarkBadgeText(
                    totals.InventoryLost,
                    totals.InventoryScrap);
                if (!string.IsNullOrWhiteSpace(mark))
                {
                    lookup[box.Id] = mark;
                }
            }

            return lookup;
        }

        private static string AppendSimulatedBoxPendingReturnHint(
            string slotToolTipText,
            IReadOnlyList<CabinetArchiveBoxDescriptor> archiveBoxes)
        {
            int pendingReturnCopyCount = archiveBoxes.Sum(box => Math.Max(0, box.PendingReturnCopyCount));
            if (pendingReturnCopyCount <= 0)
            {
                return slotToolTipText;
            }

            string pendingHint = pendingReturnCopyCount > 1
                ? $"模拟介质待还：{pendingReturnCopyCount} 份（盒内部分提档）"
                : "模拟介质待还：1 份（盒内部分提档）";

            return string.IsNullOrWhiteSpace(slotToolTipText)
                ? pendingHint
                : $"{slotToolTipText}\n{pendingHint}";
        }

        private static Dictionary<string, IReadOnlyList<CabinetArchiveBoxDescriptor>> BuildArchiveBoxesBySlot(CabinetOpenRequest request, IEnumerable<ExpandedArchiveBoxAssignment> assignments, IReadOnlyDictionary<string, CabinetArchiveBoxPlacement> placementLookup, IReadOnlyDictionary<string, ArchiveBoxSpecification> boxSpecificationLookup, double slotCanvasWidth, double slotCanvasHeight, IReadOnlyDictionary<int, int> pendingReturnByBoxId, IReadOnlyDictionary<int, string> inventoryMarkByBoxId, IReadOnlyDictionary<int, CabinetOccupationLockDescriptor> activeWithdrawalLockByBoxId)
        {
            var groupedBoxes = assignments
                .Where(assignment => ResolveFaceCode(assignment, placementLookup) == request.Face.ToString())
                .GroupBy(assignment => ResolveSlotCode(assignment, placementLookup), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var boxGroups = group
                            .GroupBy(item => item.Parsed.BoxCode, StringComparer.OrdinalIgnoreCase)
                            .OrderBy(item => item.First().Parsed.SequenceIndex)
                            .ToList();
                        var layouts = CalculateBoxLayouts(boxGroups, placementLookup, boxSpecificationLookup, slotCanvasWidth, slotCanvasHeight);
                        return (IReadOnlyList<CabinetArchiveBoxDescriptor>)boxGroups
                            .Select(item => CreateArchiveBoxDescriptor(item, placementLookup, layouts[item.Key], pendingReturnByBoxId, inventoryMarkByBoxId, activeWithdrawalLockByBoxId))
                            .ToList();
                    },
                    StringComparer.OrdinalIgnoreCase);

            return groupedBoxes;
        }

        private static CabinetArchiveBoxDescriptor CreateArchiveBoxDescriptor(IGrouping<string, ExpandedArchiveBoxAssignment> group, IReadOnlyDictionary<string, CabinetArchiveBoxPlacement> placementLookup, BoxRenderLayout layout, IReadOnlyDictionary<int, int> pendingReturnByBoxId, IReadOnlyDictionary<int, string> inventoryMarkByBoxId, IReadOnlyDictionary<int, CabinetOccupationLockDescriptor> activeWithdrawalLockByBoxId)
        {
            var first = group.First();
            var relatedBoxCodes = group
                .SelectMany(item => item.RelatedBoxCodes)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sourceBoxNumbers = group
                .Select(item => item.SourceBoxNumberText)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool isMixedPlacement = group.Any(item => item.IsMixedPlacement);
            var sourceTypes = group
                .Select(item => item.SourceType)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            string sourceSummaryText = BuildSourceSummaryText(sourceTypes, group);
            string archiveIdentifierText = BuildArchiveIdentifierText(sourceSummaryText, group);
            bool isYearlyArchiveDisplay = string.Equals(sourceSummaryText, "年度资料", StringComparison.OrdinalIgnoreCase) && !isMixedPlacement;
            string archiveSequenceNoShortText = string.Empty;
            string yearlyYearText = string.Empty;
            string yearlyProjectText = string.Empty;
            if (isYearlyArchiveDisplay
                && TryResolveYearlyArchiveDisplayFields(group, out archiveSequenceNoShortText, out yearlyYearText, out yearlyProjectText, out _))
            {
                archiveIdentifierText = BuildYearlyArchiveIdentifierText(group);
            }
            string boxSpecification = ResolveBoxSpecification(group, placementLookup);
            int yearlyArchiveBoxId = first.YearlyArchiveBoxId ?? 0;
            int pendingReturnCopyCount = yearlyArchiveBoxId > 0
                ? pendingReturnByBoxId.GetValueOrDefault(yearlyArchiveBoxId)
                : 0;
            var withdrawalLock = yearlyArchiveBoxId > 0
                ? activeWithdrawalLockByBoxId.GetValueOrDefault(yearlyArchiveBoxId, CabinetOccupationLockDescriptor.Empty)
                : CabinetOccupationLockDescriptor.Empty;

            return new CabinetArchiveBoxDescriptor
            {
                BoxCode = first.Parsed.BoxCode,
                BoxLabel = first.Parsed.SequenceIndex > 0 ? $"{first.Parsed.SequenceIndex:D2}号盒" : first.Parsed.BoxCode,
                CategoryText = isMixedPlacement ? "待梳理" : "历史存档",
                ArchiveTypeText = sourceSummaryText,
                ArchiveIdentifierText = archiveIdentifierText,
                IsYearlyArchiveDisplay = isYearlyArchiveDisplay,
                ArchiveSequenceNoShortText = archiveSequenceNoShortText,
                YearText = yearlyYearText,
                ProjectText = yearlyProjectText,
                CountText = isYearlyArchiveDisplay
                    ? (group.Count() > 0 ? $"{group.Count()}条" : string.Empty)
                    : BuildHistoryArchiveCountText(group),
                SequenceIndex = first.Parsed.SequenceIndex,
                ItemCount = group.Count(),
                SlotCode = ResolveSlotCode(first, placementLookup),
                IsMixedPlacement = isMixedPlacement,
                OriginalBoxNumberText = string.Join("；", sourceBoxNumbers),
                RelatedBoxCodesText = string.Join("；", relatedBoxCodes),
                RelatedBoxCount = relatedBoxCodes.Count,
                MixedPlacementHint = isMixedPlacement
                    ? "该批资料登记时涉及多个档案盒，当前未细化到具体盒。"
                    : string.Empty,
                SourceSummaryText = sourceSummaryText,
                PendingSortingRecordCount = isMixedPlacement ? group.Count() : 0,
                BoxSpecification = boxSpecification,
                PlacementMode = ResolvePlacementMode(first.Parsed.BoxCode, placementLookup),
                LayoutX = layout.X,
                LayoutY = layout.Y,
                LayoutWidth = layout.Width,
                LayoutHeight = layout.Height,
                YearlyArchiveBoxId = yearlyArchiveBoxId,
                PendingReturnCopyCount = pendingReturnCopyCount,
                InventoryMarkBadgeText = yearlyArchiveBoxId > 0
                    ? inventoryMarkByBoxId.GetValueOrDefault(yearlyArchiveBoxId) ?? string.Empty
                    : string.Empty,
                HasOccupationLock = withdrawalLock.HasLock,
                OccupationLockToolTipText = withdrawalLock.ToolTipSupplement,
                OccupationLockBadgeText = withdrawalLock.BadgeText
            };
        }

        private static CabinetHardDiskMediumDescriptor CreateOpticalDiscDescriptor(
            OpticalDiscMedium medium,
            bool isPendingReturn,
            MediumArchiveContext? archiveContext,
            IReadOnlyDictionary<string, decimal> usedDataSizeLookup,
            CabinetOccupationLockDescriptor? withdrawalLock = null)
        {
            ArgumentNullException.ThrowIfNull(medium);

            string discCode = string.IsNullOrWhiteSpace(medium.DiscCode)
                ? $"光盘(未编号)-{medium.Id}"
                : medium.DiscCode.Trim();
            string capacityText = string.IsNullOrWhiteSpace(medium.Capacity) ? "容量未登记" : medium.Capacity.Trim();
            string statusText = string.IsNullOrWhiteSpace(medium.Ledger?.MediaStatus) ? OpticalDiscMedium.StatusInStock : medium.Ledger.MediaStatus.Trim();
            string locationText = string.IsNullOrWhiteSpace(medium.Ledger?.StorageLocation) ? "位置未登记" : medium.Ledger.StorageLocation.Trim();
            string holderText = string.IsNullOrWhiteSpace(medium.Ledger?.HolderOrOrganization) ? "未登记" : medium.Ledger.HolderOrOrganization.Trim();
            string electronicArchiveNo = archiveContext?.ElectronicArchiveNo ?? string.Empty;
            string electronicArchiveLocation = archiveContext?.StorageLocation ?? string.Empty;
            string normalizedStatus = NormalizeStatusText(statusText);
            bool isYearlyArchiveDisplay = archiveContext != null
                && (string.Equals(normalizedStatus, NormalizeStatusText(OpticalDiscMedium.StatusInStock), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalizedStatus, NormalizeStatusText(OpticalDiscMedium.StatusDamaged), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalizedStatus, NormalizeStatusText(OpticalDiscMedium.StatusLost), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalizedStatus, NormalizeStatusText(OpticalDiscMedium.StatusScrap), StringComparison.OrdinalIgnoreCase));
            string inventoryMarkBadgeText = ResolveYearlyMediumInventoryMarkBadge(
                statusText,
                isYearlyArchiveDisplay);
            decimal usedMb = ResolveUsedCapacityMb(discCode, archiveContext, usedDataSizeLookup);
            string yearText = archiveContext?.Year ?? string.Empty;
            string projectText = archiveContext?.ProjectName ?? string.Empty;
            string usedCapacityDisplayText = FormatCapacityDisplayText(usedMb);
            string archiveInfoText = BuildArchiveInfoText(discCode, archiveContext, usedMb, null, true);
            string mediumInfoText = BuildOpticalDiscMediumInfoText(medium, usedMb);
            string electronicArchiveHint = string.IsNullOrWhiteSpace(electronicArchiveNo) && string.IsNullOrWhiteSpace(electronicArchiveLocation)
                ? string.Empty
                : $"\n介质袋编号：{(string.IsNullOrWhiteSpace(electronicArchiveNo) ? "未登记" : electronicArchiveNo)}\n介质袋物理位置：{(string.IsNullOrWhiteSpace(electronicArchiveLocation) ? "未登记" : electronicArchiveLocation)}";
            string yearlyDisplayHint = isYearlyArchiveDisplay
                ? $"\n年度：{FormatYearDisplayText(yearText)}\n项目：{FormatProjectDisplayText(projectText)}\n已用容量：{usedCapacityDisplayText}"
                : string.Empty;
            var occupationLock = ResolveWithdrawalOccupationLock(withdrawalLock);
            string baseToolTipText = isPendingReturn
                ? $"{discCode}\n容量：{capacityText}\n状态：{statusText}\n当前所在：{locationText}\n当前保管：{holderText}{electronicArchiveHint}\n该介质原存于当前档口，后续需归还。"
                : $"{discCode}\n容量：{capacityText}\n状态：{statusText}\n当前所在：{locationText}\n当前保管：{holderText}{yearlyDisplayHint}{electronicArchiveHint}";

            int archiveSequenceNumber = ResolveArchiveSequenceNumber(discCode, archiveContext);
            string archiveSequenceText = ResolveArchiveSequenceText(discCode, archiveContext);
            if (archiveSequenceNumber <= 0
                && ArchiveSlotLocationSupport.TryParseSequenceIndex(locationText, out int ledgerSequence))
            {
                archiveSequenceNumber = ledgerSequence;
                archiveSequenceText = ledgerSequence.ToString("D2");
            }
            else if (archiveSequenceNumber <= 0
                && ArchiveSlotLocationSupport.TryParseSequenceIndex(electronicArchiveLocation, out int bagSequence))
            {
                archiveSequenceNumber = bagSequence;
                archiveSequenceText = bagSequence.ToString("D2");
            }

            return new CabinetHardDiskMediumDescriptor
            {
                DiskCode = discCode,
                CapacityText = capacityText,
                StatusText = statusText,
                CurrentLocationText = locationText,
                CurrentHolderText = holderText,
                ElectronicArchiveNoText = electronicArchiveNo,
                ElectronicArchiveLocationText = electronicArchiveLocation,
                MediumInfoText = mediumInfoText,
                ArchiveInfoText = archiveInfoText,
                HasArchiveInfo = archiveContext != null,
                IsPendingReturn = isPendingReturn,
                IsYearlyArchiveDisplay = isYearlyArchiveDisplay,
                IsOpticalDiscMedia = true,
                YearText = yearText,
                ProjectText = projectText,
                UsedCapacityDisplayText = usedCapacityDisplayText,
                RemainingCapacityDisplayText = string.Empty,
                ArchiveSequenceNumber = archiveSequenceNumber,
                ArchiveSequenceText = archiveSequenceText,
                ToolTipText = AppendOccupationLockToolTip(baseToolTipText, occupationLock),
                ElectronicArchiveUnitId = archiveContext?.ElectronicArchiveUnitId ?? 0,
                MediumId = medium.Id,
                IsBlankInStock = false,
                HasOccupationLock = occupationLock.HasLock,
                OccupationLockToolTipText = occupationLock.ToolTipSupplement,
                OccupationLockBadgeText = occupationLock.BadgeText,
                InventoryMarkBadgeText = inventoryMarkBadgeText
            };
        }

        private static Dictionary<string, SlotMetrics> BuildSlotMetricsBySlot(CabinetOpenRequest request, IReadOnlyList<ExpandedArchiveBoxAssignment> assignments, IReadOnlyDictionary<string, CabinetArchiveBoxPlacement> placementLookup, IReadOnlyDictionary<string, ArchiveBoxSpecification> boxSpecificationLookup, double slotCanvasWidth)
        {
            var relevantAssignments = assignments
                .Where(item => ResolveFaceCode(item, placementLookup) == request.Face.ToString())
                .ToList();

            return relevantAssignments
                .GroupBy(item => ResolveSlotCode(item, placementLookup), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => BuildSlotMetrics(request, group.Key, group.ToList(), placementLookup, boxSpecificationLookup, (decimal)slotCanvasWidth), StringComparer.OrdinalIgnoreCase);
        }

        private static SlotMetrics BuildSlotMetrics(CabinetOpenRequest request, string slotCode, IReadOnlyList<ExpandedArchiveBoxAssignment> assignments, IReadOnlyDictionary<string, CabinetArchiveBoxPlacement> placementLookup, IReadOnlyDictionary<string, ArchiveBoxSpecification> boxSpecificationLookup, decimal slotWidth)
        {
            var boxes = assignments
                .GroupBy(item => item.Parsed.BoxCode, StringComparer.OrdinalIgnoreCase)
                .Select(group => CreateSlotBoxInfo(group, placementLookup))
                .ToList();

            var placementMetrics = ResolvePlacementMetrics(boxes, boxSpecificationLookup, slotWidth);
            string tooltip = BuildSlotToolTipText(request, slotCode, boxes, placementMetrics.UtilizationRatio, placementMetrics.CapacitySummaryText, placementMetrics.RemainingSummaryText, placementMetrics.LayoutModeText, false, false, string.Empty);
            return new SlotMetrics(placementMetrics.UtilizationRatio, FormatPercent(placementMetrics.UtilizationRatio), placementMetrics.CapacitySummaryText, placementMetrics.RemainingSummaryText, placementMetrics.LayoutModeText, tooltip, false, false, string.Empty);
        }

        private static SlotMetrics BuildMagneticDiskSlotMetrics(string slotCode, IReadOnlyCollection<CabinetHardDiskMediumDescriptor> presentMedia, IReadOnlyCollection<CabinetHardDiskMediumDescriptor> pendingReturnMedia, string dedicatedSlotCategoryName, int slotCapacity)
        {
            int presentCount = presentMedia.Count;
            int pendingReturnCount = pendingReturnMedia.Count;
            int safeSlotCapacity = slotCapacity <= 0 ? MagneticDiskSlotCapacity : slotCapacity;
            double utilizationRatio = safeSlotCapacity <= 0 ? 0d : (double)presentCount / safeSlotCapacity;
            int remainingCount = Math.Max(safeSlotCapacity - presentCount, 0);
            string matrixText = safeSlotCapacity == MagneticDiskOpticalDiscSlotCapacity ? "5×4矩阵展示" : "5×2矩阵展示";
            string layoutModeText = pendingReturnCount > 0
                ? $"{matrixText} · 下方列出待归还介质"
                : matrixText;
            if (!string.IsNullOrWhiteSpace(dedicatedSlotCategoryName))
            {
                layoutModeText = $"{dedicatedSlotCategoryName} · {layoutModeText}";
            }
            string capacitySummaryText = $"在位 {presentCount}盘 / {safeSlotCapacity}盘";
            string remainingSummaryText = pendingReturnCount > 0
                ? $"空余 {remainingCount}盘位 · 待归还 {pendingReturnCount}盘"
                : $"空余 {remainingCount}盘位";

            return new SlotMetrics(
                utilizationRatio,
                FormatPercent(utilizationRatio),
                capacitySummaryText,
                remainingSummaryText,
                layoutModeText,
                BuildMagneticDiskSlotToolTip(slotCode, presentMedia, pendingReturnMedia, capacitySummaryText, remainingSummaryText, dedicatedSlotCategoryName),
                false,
                false,
                string.Empty);
        }

        private static List<CabinetHardDiskMediumDescriptor> OrderSlotMedia(IReadOnlyCollection<CabinetHardDiskMediumDescriptor> media)
        {
            return media
                .OrderBy(item => item.ArchiveSequenceNumber <= 0 ? int.MaxValue : item.ArchiveSequenceNumber)
                .ThenBy(item => item.DiskCode, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string BuildMagneticDiskSlotToolTip(string slotCode, IReadOnlyCollection<CabinetHardDiskMediumDescriptor> presentMedia, IReadOnlyCollection<CabinetHardDiskMediumDescriptor> pendingReturnMedia, string capacitySummaryText, string remainingSummaryText, string dedicatedSlotCategoryName)
        {
            string categoryTip = string.IsNullOrWhiteSpace(dedicatedSlotCategoryName)
                ? string.Empty
                : $"当前档口已设为{dedicatedSlotCategoryName}。\n";
            string presentSummary = presentMedia.Count == 0
                ? "当前无在位介质。"
                : $"在位：{string.Join('、', presentMedia.Select(item => item.DiskCode))}";
            string pendingSummary = pendingReturnMedia.Count == 0
                ? "当前无待归还介质。"
                : $"待归还：{string.Join('、', pendingReturnMedia.Select(item => item.DiskCode))}";
            return $"格口 {slotCode}\n{categoryTip}{capacitySummaryText}\n{remainingSummaryText}\n{presentSummary}\n{pendingSummary}";
        }

        private static void AddMedium(IDictionary<string, List<CabinetHardDiskMediumDescriptor>> lookup, string slotCode, CabinetHardDiskMediumDescriptor descriptor)
        {
            if (!lookup.TryGetValue(slotCode, out var items))
            {
                items = [];
                lookup[slotCode] = items;
            }

            items.Add(descriptor);
        }

        private static CabinetHardDiskMediumDescriptor CreateHardDiskDescriptor(
            HardDiskMedium medium,
            bool isPendingReturn,
            MediumArchiveContext? archiveContext,
            IReadOnlyDictionary<string, decimal> usedDataSizeLookup,
            CabinetOccupationLockDescriptor? withdrawalLock = null,
            CabinetOccupationLockDescriptor? outboundApplicationLock = null)
        {
            var ledger = medium.Ledger;
            string diskCode = string.IsNullOrWhiteSpace(medium.DiskCode) ? "未编号" : medium.DiskCode.Trim();
            string capacityText = string.IsNullOrWhiteSpace(medium.Capacity) ? "容量未登记" : medium.Capacity.Trim();
            string statusText = string.IsNullOrWhiteSpace(ledger?.MediaStatus) ? "状态未登记" : ledger.MediaStatus.Trim();
            string currentLocation = string.IsNullOrWhiteSpace(ledger?.StorageLocation) ? "位置未登记" : ledger.StorageLocation.Trim();
            string holder = string.IsNullOrWhiteSpace(ledger?.HolderOrOrganization) ? "未登记" : ledger.HolderOrOrganization.Trim();
            string electronicArchiveNo = archiveContext?.ElectronicArchiveNo ?? string.Empty;
            string electronicArchiveLocation = archiveContext?.StorageLocation ?? string.Empty;
            string normalizedStatus = NormalizeStatusText(statusText);
            bool isYearlyArchiveDisplay = archiveContext != null
                && (string.Equals(normalizedStatus, NormalizeStatusText(HardDiskMedium.StatusInStockData), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalizedStatus, NormalizeStatusText(HardDiskMedium.StatusInStockDamaged), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalizedStatus, NormalizeStatusText(HardDiskMedium.StatusInStockLost), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalizedStatus, NormalizeStatusText(HardDiskMedium.StatusInStockScrap), StringComparison.OrdinalIgnoreCase));
            string inventoryMarkBadgeText = ResolveYearlyMediumInventoryMarkBadge(
                statusText,
                isYearlyArchiveDisplay);
            decimal usedMb = ResolveUsedCapacityMb(diskCode, archiveContext, usedDataSizeLookup);
            decimal totalMb = ElectronicMediaCapacitySupport.ParseCapacityTextToMb(medium.Capacity);
            decimal remainingMb = totalMb > 0 ? Math.Max(0, totalMb - usedMb) : 0;
            string yearText = archiveContext?.Year ?? string.Empty;
            string projectText = archiveContext?.ProjectName ?? string.Empty;
            string usedCapacityDisplayText = FormatCapacityDisplayText(usedMb);
            string remainingCapacityDisplayText = totalMb > 0 ? FormatCapacityDisplayText(remainingMb) : "—";
            string mediumInfoText = BuildHardDiskMediumInfoText(medium, usedMb, totalMb);
            string archiveInfoText = BuildArchiveInfoText(diskCode, archiveContext, usedMb, remainingMb, false);
            string electronicArchiveHint = string.IsNullOrWhiteSpace(electronicArchiveNo) && string.IsNullOrWhiteSpace(electronicArchiveLocation)
                ? string.Empty
                : $"\n介质袋编号：{(string.IsNullOrWhiteSpace(electronicArchiveNo) ? "未登记" : electronicArchiveNo)}\n介质袋物理位置：{(string.IsNullOrWhiteSpace(electronicArchiveLocation) ? "未登记" : electronicArchiveLocation)}";
            string yearlyDisplayHint = isYearlyArchiveDisplay
                ? $"\n年度：{FormatYearDisplayText(yearText)}\n项目：{FormatProjectDisplayText(projectText)}\n已用容量：{usedCapacityDisplayText}\n剩余容量：{remainingCapacityDisplayText}"
                : string.Empty;
            var occupationLock = ResolveHardDiskOccupationLock(medium.RegisterLock, withdrawalLock, outboundApplicationLock);
            string baseToolTipText = isPendingReturn
                ? $"{diskCode}\n容量：{capacityText}\n状态：{statusText}\n当前所在：{currentLocation}\n当前保管：{holder}{electronicArchiveHint}\n该介质原存于当前档口，后续需归还。"
                : $"{diskCode}\n容量：{capacityText}\n状态：{statusText}\n当前所在：{currentLocation}{yearlyDisplayHint}{electronicArchiveHint}";

            return new CabinetHardDiskMediumDescriptor
            {
                DiskCode = diskCode,
                CapacityText = capacityText,
                StatusText = statusText,
                CurrentLocationText = currentLocation,
                CurrentHolderText = holder,
                ElectronicArchiveNoText = electronicArchiveNo,
                ElectronicArchiveLocationText = electronicArchiveLocation,
                MediumInfoText = mediumInfoText,
                ArchiveInfoText = archiveInfoText,
                HasArchiveInfo = archiveContext != null,
                IsPendingReturn = isPendingReturn,
                IsYearlyArchiveDisplay = isYearlyArchiveDisplay,
                IsOpticalDiscMedia = false,
                YearText = yearText,
                ProjectText = projectText,
                UsedCapacityDisplayText = usedCapacityDisplayText,
                RemainingCapacityDisplayText = remainingCapacityDisplayText,
                ArchiveSequenceNumber = ResolveArchiveSequenceNumber(diskCode, archiveContext),
                ArchiveSequenceText = ResolveArchiveSequenceText(diskCode, archiveContext),
                ToolTipText = AppendOccupationLockToolTip(baseToolTipText, occupationLock),
                ElectronicArchiveUnitId = archiveContext?.ElectronicArchiveUnitId ?? 0,
                MediumId = medium.Id,
                IsBlankInStock = string.Equals(statusText, HardDiskMedium.StatusInStockBlank, StringComparison.OrdinalIgnoreCase),
                HasOccupationLock = occupationLock.HasLock,
                OccupationLockToolTipText = occupationLock.ToolTipSupplement,
                OccupationLockBadgeText = occupationLock.BadgeText,
                InventoryMarkBadgeText = inventoryMarkBadgeText
            };
        }

        private static int ResolveArchiveSequenceNumber(string mediumCode, MediumArchiveContext? archiveContext)
        {
            return TryResolveArchiveSequenceInfo(mediumCode, archiveContext, out int sequenceNumber, out _) ? sequenceNumber : 0;
        }

        private static string ResolveArchiveSequenceText(string mediumCode, MediumArchiveContext? archiveContext)
        {
            return TryResolveArchiveSequenceInfo(mediumCode, archiveContext, out _, out string sequenceText)
                ? sequenceText
                : string.Empty;
        }

        private static bool TryResolveArchiveSequenceInfo(string mediumCode, MediumArchiveContext? archiveContext, out int sequenceNumber, out string sequenceText)
        {
            sequenceNumber = 0;
            sequenceText = string.Empty;

            if (!archiveContext.HasValue)
            {
                return false;
            }

            MediumArchiveContext context = archiveContext.Value;
            string targetStorageLocation = context.StorageLocation;
            if (string.IsNullOrWhiteSpace(targetStorageLocation))
            {
                return false;
            }

            string locationSuffix = ResolveLocationLastSegment(targetStorageLocation);
            if (string.IsNullOrWhiteSpace(locationSuffix))
            {
                return false;
            }

            if (!int.TryParse(locationSuffix, out int parsedSuffixNumber) || parsedSuffixNumber <= 0)
            {
                return false;
            }

            sequenceNumber = parsedSuffixNumber;
            sequenceText = locationSuffix.Length >= 2 ? locationSuffix : parsedSuffixNumber.ToString("D2");
            return true;
        }

        private static string ResolveLocationLastSegment(string storageLocation)
        {
            if (string.IsNullOrWhiteSpace(storageLocation))
            {
                return string.Empty;
            }

            string normalized = storageLocation.Trim();
            var splitSegments = normalized.Split(['-', '—', '_', '/', '\\', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (splitSegments.Length > 0)
            {
                string candidate = splitSegments[^1];
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate.Trim();
                }
            }

            var match = Regex.Match(normalized, "(\\d+)(?!.*\\d)");
            return match.Success ? match.Value : string.Empty;
        }

        private Dictionary<int, MediumArchiveContext> LoadMediumArchiveLookup(IEnumerable<int> mediumIds)
        {
            var targetIds = mediumIds.Distinct().ToList();
            if (targetIds.Count == 0)
            {
                return new Dictionary<int, MediumArchiveContext>();
            }

            return _cabinetOpenLayoutRepository.GetElectronicArchiveUnitMediumLinksByMediumIds(targetIds)
                .AsEnumerable()
                .GroupBy(link => link.HardDiskMediumId)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var selectedLink = group
                            .OrderByDescending(link => link.ElectronicArchiveUnit.ArchivedDate)
                            .ThenByDescending(link => link.Id)
                            .First();

                        var unit = selectedLink.ElectronicArchiveUnit;
                        return CreateMediumArchiveContext(unit);
                    });
        }

        private Dictionary<int, MediumArchiveContext> LoadOpticalDiscArchiveLookup(IEnumerable<int> mediumIds)
        {
            var targetIds = mediumIds.Distinct().ToList();
            if (targetIds.Count == 0)
            {
                return new Dictionary<int, MediumArchiveContext>();
            }

            return _cabinetOpenLayoutRepository.GetElectronicArchiveUnitDiscLinksByMediumIds(targetIds)
                .AsEnumerable()
                .GroupBy(link => link.OpticalDiscMediumId)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var selectedLink = group
                            .OrderByDescending(link => link.ElectronicArchiveUnit.ArchivedDate)
                            .ThenByDescending(link => link.Id)
                            .First();

                        var unit = selectedLink.ElectronicArchiveUnit;
                        return CreateMediumArchiveContext(unit);
                    });
        }

        private static MediumArchiveContext CreateMediumArchiveContext(YearlyElectronicArchiveUnit unit)
        {
            string archivedDateText = unit.ArchivedDate == default ? string.Empty : unit.ArchivedDate.ToString("yyyy-MM-dd");
            var mediumItems = unit.MediaItemLinks
                .OrderBy(link => link.FormNo)
                .ThenBy(link => link.MaterialName)
                .ThenBy(link => link.ItemName)
                .Select(link => MapMediumArchiveItemDetail(link))
                .ToList();

            return new MediumArchiveContext(
                string.IsNullOrWhiteSpace(unit.ElectronicArchiveNo) ? string.Empty : unit.ElectronicArchiveNo.Trim(),
                string.IsNullOrWhiteSpace(unit.StorageLocation) ? string.Empty : unit.StorageLocation.Trim(),
                string.IsNullOrWhiteSpace(unit.StoragePath) ? string.Empty : unit.StoragePath.Trim(),
                string.IsNullOrWhiteSpace(unit.StorageCarrierType) ? string.Empty : unit.StorageCarrierType.Trim(),
                string.IsNullOrWhiteSpace(unit.ContentSummary) ? string.Empty : unit.ContentSummary.Trim(),
                string.IsNullOrWhiteSpace(unit.ProjectName) ? string.Empty : unit.ProjectName.Trim(),
                string.IsNullOrWhiteSpace(unit.Year) ? string.Empty : unit.Year.Trim(),
                archivedDateText,
                string.IsNullOrWhiteSpace(unit.ArchivedBy) ? string.Empty : unit.ArchivedBy.Trim(),
                string.IsNullOrWhiteSpace(unit.LinkedMediumCodes) ? string.Empty : unit.LinkedMediumCodes.Trim(),
                string.IsNullOrWhiteSpace(unit.Disposition) ? string.Empty : unit.Disposition.Trim(),
                unit.MediaCount,
                string.IsNullOrWhiteSpace(unit.SourceType) ? string.Empty : unit.SourceType.Trim(),
                string.IsNullOrWhiteSpace(unit.Remarks) ? string.Empty : unit.Remarks.Trim(),
                0m,
                mediumItems,
                unit.Id);
        }

        private Dictionary<string, decimal> LoadUsedDataSizeLookup(
            IReadOnlyCollection<HardDiskMedium> hardDiskMedia,
            IReadOnlyCollection<OpticalDiscMedium> opticalDiscMedia,
            IReadOnlyDictionary<int, MediumArchiveContext> hardDiskArchiveLookup,
            IReadOnlyDictionary<int, MediumArchiveContext> opticalDiscArchiveLookup)
        {
            var mediumCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var medium in hardDiskMedia)
            {
                if (hardDiskArchiveLookup.ContainsKey(medium.Id) && !string.IsNullOrWhiteSpace(medium.DiskCode))
                {
                    mediumCodes.Add(medium.DiskCode.Trim());
                }
            }

            foreach (var medium in opticalDiscMedia)
            {
                if (opticalDiscArchiveLookup.ContainsKey(medium.Id) && !string.IsNullOrWhiteSpace(medium.DiscCode))
                {
                    mediumCodes.Add(medium.DiscCode.Trim());
                }
            }

            return mediumCodes.Count == 0
                ? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                : _cabinetOpenLayoutRepository.GetUsedDataSizeMbByMediumCodes(mediumCodes);
        }

        private static decimal ResolveUsedCapacityMb(
            string mediumCode,
            MediumArchiveContext? archiveContext,
            IReadOnlyDictionary<string, decimal> usedDataSizeLookup)
        {
            if (!string.IsNullOrWhiteSpace(mediumCode)
                && usedDataSizeLookup.TryGetValue(mediumCode.Trim(), out decimal usedFromLookup))
            {
                return usedFromLookup;
            }

            return archiveContext?.UsedCapacityMb ?? 0m;
        }

        private static string FormatCapacityDisplayText(decimal capacityMb)
            => capacityMb > 0 ? ElectronicMediaCapacitySupport.FormatCapacityMb(capacityMb) : "—";

        private static string FormatYearDisplayText(string? yearText)
            => string.IsNullOrWhiteSpace(yearText) ? "—" : yearText.Trim();

        private static string FormatProjectDisplayText(string? projectText)
            => string.IsNullOrWhiteSpace(projectText) ? "—" : projectText.Trim();

        private static string FormatDisplayField(string? value, string emptyText = "未登记")
            => string.IsNullOrWhiteSpace(value) ? emptyText : value.Trim();

        private static string FormatOptionalDate(DateTime? value)
            => value.HasValue && value.Value != default ? value.Value.ToString("yyyy-MM-dd") : "未登记";

        private static string BuildHardDiskMediumInfoText(HardDiskMedium medium, decimal usedMb, decimal totalMb)
        {
            var builder = new StringBuilder();
            builder.AppendLine("【硬盘基本参数】");
            builder.AppendLine($"硬盘编号：{FormatDisplayField(medium.DiskCode)}");
            builder.AppendLine($"序列号：{FormatDisplayField(medium.SerialNumber)}");
            builder.AppendLine($"硬盘类型：{FormatDisplayField(medium.DiskType)}");
            builder.AppendLine($"品牌：{FormatDisplayField(medium.Brand)}");
            builder.AppendLine($"容量：{FormatDisplayField(medium.Capacity)}");
            builder.AppendLine($"接口类型：{FormatDisplayField(medium.InterfaceType)}");
            builder.AppendLine($"出厂日期：{FormatOptionalDate(medium.FactoryDate)}");
            builder.AppendLine($"登记人：{FormatDisplayField(medium.RegisterPerson)}");
            builder.AppendLine($"登记日期：{(medium.RegisterDate == default ? "未登记" : medium.RegisterDate.ToString("yyyy-MM-dd"))}");
            builder.AppendLine($"登记方式：{FormatDisplayField(medium.RegistrationMethod)}");
            if (!string.IsNullOrWhiteSpace(medium.Remark))
            {
                builder.AppendLine($"备注：{medium.Remark.Trim()}");
            }

            builder.AppendLine();
            builder.AppendLine("【可用容量】");
            builder.AppendLine($"标称容量：{FormatDisplayField(medium.Capacity)}");
            if (usedMb > 0)
            {
                builder.AppendLine($"已用容量：{FormatCapacityDisplayText(usedMb)}");
            }

            if (totalMb > 0)
            {
                decimal remainingMb = Math.Max(0, totalMb - usedMb);
                builder.AppendLine($"剩余容量：{FormatCapacityDisplayText(remainingMb)}");
            }
            else if (usedMb <= 0)
            {
                builder.AppendLine("剩余容量：—");
            }

            return builder.ToString().TrimEnd();
        }

        private static string BuildOpticalDiscMediumInfoText(OpticalDiscMedium medium, decimal usedMb)
        {
            var builder = new StringBuilder();
            builder.AppendLine("【光盘基本参数】");
            builder.AppendLine($"光盘编号：{FormatDisplayField(medium.DiscCode)}");
            builder.AppendLine($"光盘类型：{FormatDisplayField(medium.DiscType)}");
            builder.AppendLine($"容量：{FormatDisplayField(medium.Capacity)}");
            if (!string.IsNullOrWhiteSpace(medium.Remarks))
            {
                builder.AppendLine($"备注：{medium.Remarks.Trim()}");
            }

            builder.AppendLine();
            builder.AppendLine("【可用容量】");
            builder.AppendLine($"标称容量：{FormatDisplayField(medium.Capacity)}");
            builder.AppendLine($"已用容量：{FormatCapacityDisplayText(usedMb)}");

            return builder.ToString().TrimEnd();
        }

        private static string BuildArchiveInfoText(string mediumCode, MediumArchiveContext? archiveContext, decimal usedMb, decimal? remainingMb, bool isOpticalDiscMedia)
        {
            string mediumLabel = isOpticalDiscMedia ? "光盘" : "硬盘";
            if (archiveContext == null)
            {
                return $"{mediumLabel}：{mediumCode}\n尚未关联电子介质袋资料信息。";
            }

            var context = archiveContext.Value;
            var builder = new StringBuilder();
            builder.AppendLine("【电子介质袋】");
            builder.AppendLine($"介质袋编号：{FormatDisplayField(context.ElectronicArchiveNo)}");
            builder.AppendLine($"介质袋物理位置：{FormatDisplayField(context.StorageLocation)}");
            builder.AppendLine($"所属项目：{FormatDisplayField(context.ProjectName)}");
            builder.AppendLine($"所属年度：{FormatDisplayField(context.Year)}");
            builder.AppendLine($"存储载体类型：{FormatDisplayField(context.StorageCarrierType)}");
            builder.AppendLine($"存储路径：{FormatDisplayField(context.StoragePath)}");
            builder.AppendLine($"关联介质编号：{FormatDisplayField(context.LinkedMediumCodes)}");
            builder.AppendLine($"处置方式：{FormatDisplayField(context.Disposition)}");
            builder.AppendLine($"介质数量：{(context.MediaCount > 0 ? context.MediaCount.ToString() : "未登记")}");
            builder.AppendLine($"资料内容摘要：{FormatDisplayField(context.ContentSummary)}");
            builder.AppendLine($"归档人：{FormatDisplayField(context.ArchivedBy)}");
            builder.AppendLine($"立档日期：{FormatDisplayField(context.ArchivedDateText)}");
            builder.AppendLine($"来源类型：{FormatDisplayField(context.SourceType)}");
            if (!string.IsNullOrWhiteSpace(context.Remarks))
            {
                builder.AppendLine($"备注：{context.Remarks.Trim()}");
            }

            var mediumItems = ResolveMediumArchiveItems(context.MediumItems, mediumCode);
            builder.AppendLine();
            builder.AppendLine($"【本{mediumLabel}资料明细】");
            if (mediumItems.Count == 0)
            {
                builder.AppendLine("暂无登记到本介质的资料子项。");
            }
            else
            {
                for (int index = 0; index < mediumItems.Count; index++)
                {
                    var item = mediumItems[index];
                    builder.AppendLine($"{index + 1}. 单号：{FormatDisplayField(item.FormNo)}");
                    builder.AppendLine($"   资料名称：{FormatDisplayField(item.MaterialName)}");
                    builder.AppendLine($"   子项名称：{FormatDisplayField(item.ItemName)}");
                    AppendDetailLineIfPresent(builder, "   来源介质", item.MediaType);
                    AppendDetailLineIfPresent(builder, "   资料类型", item.MaterialCategory);
                    AppendDetailLineIfPresent(builder, "   所属子类", item.SubCategory);
                    AppendDetailLineIfPresent(builder, "   组织形式", item.DataOrganizationForm);
                    AppendDetailLineIfPresent(builder, "   库管模式", item.ArchivePurpose);
                    builder.AppendLine($"   数据量：{FormatCapacityDisplayText(item.DataSizeMb)}");
                    builder.AppendLine($"   存储路径：{FormatDisplayField(item.FilingStoragePath)}");
                }
            }

            builder.AppendLine();
            builder.AppendLine("【容量占用】");
            builder.AppendLine($"本{mediumLabel}已用容量：{FormatCapacityDisplayText(usedMb)}");
            if (remainingMb.HasValue)
            {
                builder.AppendLine($"本{mediumLabel}剩余容量：{FormatCapacityDisplayText(remainingMb.Value)}");
            }

            return builder.ToString().TrimEnd();
        }

        private static MediumArchiveItemDetail MapMediumArchiveItemDetail(YearlyElectronicArchiveUnitMediaItemLink link)
        {
            var mediaItem = link.MediaItem;
            var detail = mediaItem?.ElectronicDetail;
            return new MediumArchiveItemDetail(
                string.IsNullOrWhiteSpace(link.MediumCode) ? string.Empty : link.MediumCode.Trim(),
                string.IsNullOrWhiteSpace(link.FormNo) ? string.Empty : link.FormNo.Trim(),
                string.IsNullOrWhiteSpace(link.MaterialName) ? string.Empty : link.MaterialName.Trim(),
                string.IsNullOrWhiteSpace(link.ItemName) ? string.Empty : link.ItemName.Trim(),
                mediaItem?.MediaEntry?.MediaType?.Trim() ?? string.Empty,
                detail?.MaterialCategory?.Trim() ?? string.Empty,
                detail?.SubCategory?.Trim() ?? string.Empty,
                detail?.DataOrganizationForm?.Trim() ?? string.Empty,
                mediaItem?.MediaEntry?.RegisterRecord?.ArchivePurpose?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(link.FilingStoragePath) ? string.Empty : link.FilingStoragePath.Trim(),
                link.DataSizeMb);
        }

        private static void AppendDetailLineIfPresent(StringBuilder builder, string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            builder.AppendLine($"{label}：{value.Trim()}");
        }

        private static IReadOnlyList<MediumArchiveItemDetail> ResolveMediumArchiveItems(
            IReadOnlyList<MediumArchiveItemDetail> mediumItems,
            string mediumCode)
        {
            if (mediumItems.Count == 0)
            {
                return mediumItems;
            }

            if (string.IsNullOrWhiteSpace(mediumCode))
            {
                return mediumItems;
            }

            string normalizedCode = mediumCode.Trim();
            var matchedItems = mediumItems
                .Where(item => string.Equals(item.MediumCode, normalizedCode, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return matchedItems.Count > 0 ? matchedItems : mediumItems;
        }

        private static string ResolveYearlyMediumInventoryMarkBadge(
            string? statusText,
            bool isYearlyArchiveDisplay)
        {
            if (!isYearlyArchiveDisplay)
            {
                return string.Empty;
            }

            string normalized = NormalizeStatusText(statusText);
            if (string.Equals(normalized, NormalizeStatusText(HardDiskMedium.StatusInStockDamaged), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, NormalizeStatusText(OpticalDiscMedium.StatusDamaged), StringComparison.OrdinalIgnoreCase))
            {
                return CabinetOpenStatusBadgeSupport.InventoryDamagedMarkText;
            }

            if (string.Equals(normalized, NormalizeStatusText(HardDiskMedium.StatusInStockLost), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, NormalizeStatusText(OpticalDiscMedium.StatusLost), StringComparison.OrdinalIgnoreCase))
            {
                return CabinetOpenStatusBadgeSupport.InventoryLostMarkText;
            }

            if (string.Equals(normalized, NormalizeStatusText(HardDiskMedium.StatusInStockScrap), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, NormalizeStatusText(OpticalDiscMedium.StatusScrap), StringComparison.OrdinalIgnoreCase))
            {
                return CabinetOpenStatusBadgeSupport.InventoryScrapMarkText;
            }

            return string.Empty;
        }

        private static bool IsInStockStatus(string? statusText)
        {
            string normalizedStatus = NormalizeStatusText(statusText);
            return string.Equals(normalizedStatus, NormalizeStatusText(HardDiskMedium.StatusInStockBlank), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedStatus, NormalizeStatusText(HardDiskMedium.StatusInStockData), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedStatus, NormalizeStatusText(HardDiskMedium.StatusInStockDamaged), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedStatus, NormalizeStatusText(HardDiskMedium.StatusInStockLost), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedStatus, NormalizeStatusText(HardDiskMedium.StatusInStockScrap), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOpticalDiscInStockStatus(string? statusText)
        {
            string normalizedStatus = NormalizeStatusText(statusText);
            return string.Equals(normalizedStatus, NormalizeStatusText(OpticalDiscMedium.StatusInStock), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedStatus, NormalizeStatusText(OpticalDiscMedium.StatusDamaged), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedStatus, NormalizeStatusText(OpticalDiscMedium.StatusLost), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedStatus, NormalizeStatusText(OpticalDiscMedium.StatusScrap), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeStatusText(string? statusText)
            => MediumStatusTextNormalizer.Normalize(statusText);

        private static bool TryParseMagneticDiskLocation(string? locationText, out MagneticDiskLocation location)
        {
            location = default;
            if (string.IsNullOrWhiteSpace(locationText))
            {
                return false;
            }

            string normalizedLocationText = NormalizeLocationText(locationText);
            var match = Regex.Match(
                normalizedLocationText,
                "^(?<cabinet>.+?)(?<face>[A-Za-z])\\s*[\\-]?\\s*(?<layer>\\d+)\\s*[\\-]\\s*(?<column>\\d+)(?:\\D.*)?$",
                RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return false;
            }

            string cabinetName = CabinetNameNormalizer.Normalize(match.Groups["cabinet"].Value);
            string faceCode = match.Groups["face"].Value.ToUpperInvariant();
            if (!int.TryParse(match.Groups["layer"].Value, out int layerIndex) || !int.TryParse(match.Groups["column"].Value, out int columnIndex))
            {
                return false;
            }

            location = new MagneticDiskLocation(cabinetName, faceCode, layerIndex, columnIndex);
            return true;
        }

        private static string NormalizeLocationText(string locationText)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(locationText);

            char[] chars = locationText.Trim()
                .Replace('－', '-')
                .Replace('（', '(')
                .Replace('）', ')')
                .ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                char ch = chars[i];
                if (ch is >= 'Ａ' and <= 'Ｚ')
                {
                    chars[i] = (char)(ch - 'Ａ' + 'A');
                    continue;
                }

                if (ch is >= 'ａ' and <= 'ｚ')
                {
                    chars[i] = (char)(ch - 'ａ' + 'a');
                }
            }

            return new string(chars);
        }

        private static bool IsMatchingLocation(MagneticDiskLocation location, string normalizedCabinetName, CabinetFace face, int layerCount, int columnCount)
        {
            return IsSameCabinetName(location.CabinetName, normalizedCabinetName)
                && string.Equals(location.FaceCode, face.ToString(), StringComparison.OrdinalIgnoreCase)
                && location.LayerIndex >= 1
                && location.LayerIndex <= layerCount
                && location.ColumnIndex >= 1
                && location.ColumnIndex <= columnCount;
        }

        private static bool IsSameCabinetName(string left, string right)
        {
            string normalizedLeft = CabinetNameNormalizer.Normalize(left);
            string normalizedRight = CabinetNameNormalizer.Normalize(right);
            if (string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(TrimCabinetSuffix(normalizedLeft), TrimCabinetSuffix(normalizedRight), StringComparison.OrdinalIgnoreCase);
        }

        private static string TrimCabinetSuffix(string cabinetName)
        {
            if (cabinetName.EndsWith("柜", StringComparison.Ordinal))
            {
                return cabinetName[..^1];
            }

            return cabinetName;
        }

        private IEnumerable<ExpandedArchiveBoxAssignment> EnumerateArchiveAssignments()
        {
            return _cabinetOpenLayoutRepository.GetTopoMaps()
                .AsEnumerable()
                .SelectMany(ExpandTopoMapAssignments)
                .Concat(_cabinetOpenLayoutRepository.GetAerialPhotos()
                    .AsEnumerable()
                    .SelectMany(ExpandAerialPhotoAssignments))
                .Concat(_cabinetOpenLayoutRepository.GetOtherMaps()
                    .AsEnumerable()
                    .SelectMany(ExpandOtherMapAssignments))
                .Concat(_cabinetOpenLayoutRepository.GetYearlyArchiveBoxesWithContents()
                    .AsEnumerable()
                    .SelectMany(ExpandYearlyArchiveAssignments));
        }

        private readonly record struct MagneticDiskLocation(string CabinetName, string FaceCode, int LayerIndex, int ColumnIndex)
        {
            public string SlotCode => $"{LayerIndex}-{ColumnIndex}";
        }

        private static IEnumerable<ExpandedArchiveBoxAssignment> ExpandTopoMapAssignments(TopoMap map)
        {
            if (map == null || string.IsNullOrWhiteSpace(map.BoxNumber))
            {
                yield break;
            }

            var rawBoxCodes = SplitArchiveBoxCodes(map.BoxNumber).ToArray();
            if (rawBoxCodes.Length == 0)
            {
                yield break;
            }

            bool isMixedPlacement = rawBoxCodes.Length > 1;

            foreach (var rawBoxCode in rawBoxCodes)
            {
                var parsed = ParseArchiveBox(rawBoxCode);
                if (parsed == null)
                {
                    continue;
                }

                yield return new ExpandedArchiveBoxAssignment(
                    parsed,
                    isMixedPlacement,
                    map.BoxNumber.Trim(),
                    rawBoxCodes,
                    "地形图",
                    1,
                    map.BoxSpecification,
                    map.Scale,
                    map.MapNumber,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    map.MapName,
                    map.SheetCount > 0 ? $"{map.SheetCount}幅" : string.Empty,
                    BuildTopoDetailText(map),
                    BuildDateText(map.SurveyDate, map.CreationDate),
                    map.Scale,
                    map.MapNumber,
                    map.MapName);
            }
        }

        private static IEnumerable<ExpandedArchiveBoxAssignment> ExpandAerialPhotoAssignments(AerialPhoto photo)
        {
            if (photo == null || string.IsNullOrWhiteSpace(photo.BoxNumber))
            {
                yield break;
            }

            var rawBoxCodes = SplitArchiveBoxCodes(photo.BoxNumber).ToArray();
            if (rawBoxCodes.Length == 0)
            {
                yield break;
            }

            bool isMixedPlacement = rawBoxCodes.Length > 1;

            foreach (var rawBoxCode in rawBoxCodes)
            {
                var parsed = ParseArchiveBox(rawBoxCode);
                if (parsed == null)
                {
                    continue;
                }

                yield return new ExpandedArchiveBoxAssignment(
                    parsed,
                    isMixedPlacement,
                    photo.BoxNumber.Trim(),
                    rawBoxCodes,
                    "航摄影像",
                    2,
                    photo.BoxSpecification,
                    photo.Scale,
                    photo.SurveyArea,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.IsNullOrWhiteSpace(photo.BoxContents) ? photo.SurveyArea : photo.BoxContents,
                    photo.PhotoCount > 0 ? $"{photo.PhotoCount}张" : string.Empty,
                    photo.Remark,
                    photo.PhotographyDate,
                    photo.Category,
                    photo.SurveyArea,
                    photo.BoxContents);
            }
        }

        private static IEnumerable<ExpandedArchiveBoxAssignment> ExpandOtherMapAssignments(OtherMap map)
        {
            if (map == null || string.IsNullOrWhiteSpace(map.BoxNumber))
            {
                yield break;
            }

            var rawBoxCodes = SplitArchiveBoxCodes(map.BoxNumber).ToArray();
            if (rawBoxCodes.Length == 0)
            {
                yield break;
            }

            bool isMixedPlacement = rawBoxCodes.Length > 1;

            foreach (var rawBoxCode in rawBoxCodes)
            {
                var parsed = ParseArchiveBox(rawBoxCode);
                if (parsed == null)
                {
                    continue;
                }

                yield return new ExpandedArchiveBoxAssignment(
                    parsed,
                    isMixedPlacement,
                    map.BoxNumber.Trim(),
                    rawBoxCodes,
                    "其他图件",
                    3,
                    map.BoxSpecification,
                    map.Scale,
                    map.SequenceNumber,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    map.MapName,
                    map.SheetCount > 0 ? $"{map.SheetCount}幅" : string.Empty,
                    map.Remark,
                    string.Empty,
                    map.Category,
                    map.SequenceNumber,
                    map.MapName);
            }
        }

        private static IEnumerable<ExpandedArchiveBoxAssignment> ExpandYearlyArchiveAssignments(YearlyArchiveBox box)
        {
            if (box == null || string.IsNullOrWhiteSpace(box.BoxLocationCode))
            {
                yield break;
            }

            var parsed = ParseArchiveBox(box.BoxLocationCode);
            if (parsed == null)
            {
                yield break;
            }

            string[] rawBoxCodes = [box.BoxLocationCode.Trim()];
            string dateText = box.ArchivedDate == default ? string.Empty : box.ArchivedDate.ToString("yyyy-MM-dd");
            string boxSpec = string.IsNullOrWhiteSpace(box.Specs) ? string.Empty : box.Specs.Trim();

            if (box.RegisterRecords.Count == 0)
            {
                yield return new ExpandedArchiveBoxAssignment(
                    parsed,
                    false,
                    box.BoxLocationCode.Trim(),
                    rawBoxCodes,
                    "年度资料",
                    4,
                    boxSpec,
                    "资料",
                    box.ArchiveSequenceNo,
                    string.Empty,
                    box.ProjectName,
                    box.ProjectName,
                    box.ProjectName,
                    "1份",
                    box.Remarks,
                    dateText,
                    box.Year,
                    box.ProjectName,
                    box.ArchiveSequenceNo,
                    box.ArchiveSequenceNo,
                    box.Id);
                yield break;
            }

            foreach (var record in box.RegisterRecords.OrderBy(item => item.FormNo, StringComparer.OrdinalIgnoreCase))
            {
                string identifierText = string.IsNullOrWhiteSpace(record.FormNo) ? box.ArchiveSequenceNo : record.FormNo;
                string titleText = string.IsNullOrWhiteSpace(record.MaterialName) ? box.ProjectName : record.MaterialName;
                yield return new ExpandedArchiveBoxAssignment(
                    parsed,
                    false,
                    box.BoxLocationCode.Trim(),
                    rawBoxCodes,
                    "年度资料",
                    4,
                    boxSpec,
                    "资料",
                    identifierText,
                    record.FormNo,
                    box.ProjectName,
                    titleText,
                    titleText,
                    "1份",
                    box.Remarks,
                    dateText,
                    box.Year,
                    box.ProjectName,
                    identifierText,
                    box.ArchiveSequenceNo,
                    box.Id);
            }
        }

        private static CabinetOccupationLockDescriptor ResolveElectronicUnitWithdrawalLock(
            MediumArchiveContext? archiveContext,
            IReadOnlyDictionary<int, CabinetOccupationLockDescriptor> activeWithdrawalLockByUnitId)
        {
            if (!archiveContext.HasValue || archiveContext.Value.ElectronicArchiveUnitId <= 0)
            {
                return CabinetOccupationLockDescriptor.Empty;
            }

            return activeWithdrawalLockByUnitId.GetValueOrDefault(
                archiveContext.Value.ElectronicArchiveUnitId,
                CabinetOccupationLockDescriptor.Empty);
        }

        private static CabinetOccupationLockDescriptor ResolveWithdrawalOccupationLock(CabinetOccupationLockDescriptor? withdrawalLock)
            => withdrawalLock is { HasLock: true } lockDescriptor ? lockDescriptor : CabinetOccupationLockDescriptor.Empty;

        private static CabinetOccupationLockDescriptor ResolveHardDiskOccupationLock(
            HardDiskRegisterLock? registerLock,
            CabinetOccupationLockDescriptor? withdrawalLock,
            CabinetOccupationLockDescriptor? outboundApplicationLock = null)
        {
            bool hasRegisterLock = registerLock != null;
            bool hasOutboundApplicationLock = outboundApplicationLock is { HasLock: true };
            bool hasWithdrawalLock = withdrawalLock is { HasLock: true };
            if (!hasRegisterLock && !hasOutboundApplicationLock && !hasWithdrawalLock)
            {
                return CabinetOccupationLockDescriptor.Empty;
            }

            var supplementParts = new List<string>();
            if (hasRegisterLock)
            {
                supplementParts.Add(BuildRegisterLockToolTip(registerLock!));
            }
            else if (hasOutboundApplicationLock)
            {
                supplementParts.Add(outboundApplicationLock!.ToolTipSupplement);
            }

            if (hasWithdrawalLock)
            {
                supplementParts.Add(withdrawalLock!.ToolTipSupplement);
            }

            return new CabinetOccupationLockDescriptor
            {
                HasLock = true,
                LockKindText = hasRegisterLock || hasOutboundApplicationLock ? "占用锁" : withdrawalLock!.LockKindText,
                BusinessTypeText = hasRegisterLock
                    ? registerLock!.BusinessType
                    : hasOutboundApplicationLock
                        ? outboundApplicationLock!.BusinessTypeText
                        : withdrawalLock!.BusinessTypeText,
                BusinessNoText = hasRegisterLock
                    ? registerLock!.BusinessNo
                    : hasOutboundApplicationLock
                        ? outboundApplicationLock!.BusinessNoText
                        : withdrawalLock!.BusinessNoText,
                ReservedCopyCount = hasWithdrawalLock ? withdrawalLock!.ReservedCopyCount : 0,
                ToolTipSupplement = string.Join("\n\n", supplementParts.Where(part => !string.IsNullOrWhiteSpace(part))),
            };
        }

        private static string BuildRegisterLockToolTip(HardDiskRegisterLock registerLock)
        {
            string businessNo = string.IsNullOrWhiteSpace(registerLock.BusinessNo)
                ? "（无）"
                : registerLock.BusinessNo.Trim();
            return $"占用锁\n业务类型：{registerLock.BusinessType}\n业务单号：{businessNo}";
        }

        private static string AppendOccupationLockToolTip(string baseToolTipText, CabinetOccupationLockDescriptor occupationLock)
        {
            if (!occupationLock.HasLock || string.IsNullOrWhiteSpace(occupationLock.ToolTipSupplement))
            {
                return baseToolTipText;
            }

            return string.IsNullOrWhiteSpace(baseToolTipText)
                ? occupationLock.ToolTipSupplement
                : $"{baseToolTipText}\n\n{occupationLock.ToolTipSupplement}";
        }

    }
}
