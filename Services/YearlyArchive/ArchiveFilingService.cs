using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DocMgr.Services.YearlyArchive
{
    public partial class ArchiveFilingService : IArchiveFilingService
    {
        private static readonly Regex ArchiveSequenceNoRegex = new("^年度模拟-(\\d{4})-(\\d{3})$", RegexOptions.Compiled);
        private static readonly Regex ElectronicArchiveNoRegex = new("^年度电子-(\\d{4})-(\\d{3})$", RegexOptions.Compiled);
        private readonly IArchiveFilingRepository _archiveFilingRepository;
        private readonly ICabinetRepository _cabinetRepository;
        private readonly IHardDiskMediaService _hardDiskMediaService;
        private readonly IFilingFactWriter _filingFactWriter;
        private ElectronicArchiveSubmissionChangeTracker? _submissionChangeTracker;

        public ArchiveFilingService(
            IArchiveFilingRepository archiveFilingRepository,
            ICabinetRepository cabinetRepository,
            IHardDiskMediaService hardDiskMediaService,
            IFilingFactWriter filingFactWriter)
        {
            _archiveFilingRepository = archiveFilingRepository;
            _cabinetRepository = cabinetRepository;
            _hardDiskMediaService = hardDiskMediaService;
            _filingFactWriter = filingFactWriter;
        }

        public async Task<List<YearlyArchiveRegisterRecord>> GetPendingRecordsAsync(string? year = null)
        {
            return await _archiveFilingRepository.GetPendingRecordsAsync(ParseYear(year));
        }

        public async Task<List<YearlyArchiveRegisterRecord>> GetPendingSimulatedRecordsAsync(string? year = null)
        {
            return await _archiveFilingRepository.GetPendingSimulatedRecordsAsync(ParseYear(year));
        }

        public async Task<List<YearlyArchiveRegisterRecord>> GetPendingElectronicRecordsAsync(string? year = null)
        {
            return await _archiveFilingRepository.GetPendingElectronicRecordsAsync(ParseYear(year));
        }

        public Task<int> GetFiledSimulatedRecordCountAsync(string? year = null)
        {
            return _archiveFilingRepository.GetFiledSimulatedRecordCountAsync(ParseYear(year));
        }

        public Task<int> GetFiledElectronicRecordCountAsync(string? year = null)
        {
            return _archiveFilingRepository.GetFiledElectronicRecordCountAsync(ParseYear(year));
        }

        public async Task<List<YearlyArchiveBox>> GetExistingBoxesForProjectAsync(string projectName, string year)
        {
            if (string.IsNullOrWhiteSpace(projectName) || string.IsNullOrWhiteSpace(year))
            {
                return new List<YearlyArchiveBox>();
            }

            var boxes = await _archiveFilingRepository.GetExistingBoxesForProjectAsync(projectName, year);

            return OrderContainersByCode(boxes);
        }

        public async Task<List<YearlyElectronicArchiveUnit>> GetExistingElectronicUnitsForProjectAsync(string projectName, string year)
        {
            if (string.IsNullOrWhiteSpace(projectName) || string.IsNullOrWhiteSpace(year))
            {
                return new List<YearlyElectronicArchiveUnit>();
            }

            var units = await _archiveFilingRepository.GetExistingElectronicUnitsForProjectAsync(projectName, year);

            return OrderContainersByCode(units);
        }

        public async Task<string> GenerateNextArchiveSequenceNoAsync(string year)
        {
            if (string.IsNullOrWhiteSpace(year)) year = DateTime.Now.Year.ToString();
            string prefix = $"年度模拟-{year}-";
            var lastBox = await _archiveFilingRepository.GetLastArchiveBoxByPrefixAsync(prefix);
            int nextSeq = 1;
            if (lastBox != null) { string parts = lastBox.ArchiveSequenceNo.Substring(prefix.Length); if (int.TryParse(parts, out int current)) nextSeq = current + 1; }
            return $"{prefix}{nextSeq:D3}";
        }

        public async Task<string> GenerateNextElectronicArchiveNoAsync(string year)
        {
            if (string.IsNullOrWhiteSpace(year))
            {
                year = DateTime.Now.Year.ToString();
            }

            string prefix = $"年度电子-{year}-";
            var lastUnit = await _archiveFilingRepository.GetLastElectronicUnitByPrefixAsync(prefix);

            int nextSeq = 1;
            if (lastUnit != null)
            {
                string parts = lastUnit.ElectronicArchiveNo.Substring(prefix.Length);
                if (int.TryParse(parts, out int current))
                {
                    nextSeq = current + 1;
                }
            }

            return $"{prefix}{nextSeq:D3}";
        }

        /// <inheritdoc/>
        public async Task<ElectronicArchiveSubmissionResult> SubmitNewElectronicArchiveUnitAsync(ElectronicArchiveSubmissionRequest request, User? currentUser)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.ArchiveUnit);

            List<int> mediaItemIds = NormalizeElectronicSubmissionMediaItemIds(request.MediaItemIds);
            var (resolvedItemIds, mediaItems, mediaEntries) = await ResolveElectronicSubmissionAsync(request);
            mediaItemIds = resolvedItemIds;
            EnrichBorrowedHardDiskSubmissionAsync(request, mediaEntries);
            ValidateElectronicSubmissionRequest(request, mediaItemIds, mediaEntries, requireExistingUnitId: false);
            await ValidateCopySubmissionMediumCapacityAsync(request, mediaItems);

            var archiveUnit = CreateSubmissionArchiveUnit(request.ArchiveUnit, currentUser);
            ApplyOpticalDiscSingleArchiveRules(request, archiveUnit, mediaEntries);
            var records = mediaEntries
                .Select(item => item.RegisterRecord!)
                .DistinctBy(item => item.Id)
                .ToList();

            ValidateNewElectronicArchiveConstraints(archiveUnit, records);

            bool exists = await IsElectronicArchiveNoExistsAsync(archiveUnit.ElectronicArchiveNo);
            if (exists)
            {
                throw new InvalidOperationException($"编号 {archiveUnit.ElectronicArchiveNo} 已存在，请重新生成或手动修改。");
            }

            await using var transaction = await _archiveFilingRepository.BeginTransactionAsync();
            var changeTracker = new ElectronicArchiveSubmissionChangeTracker();
            _submissionChangeTracker = changeTracker;
            try
            {
                changeTracker.BeginSection("提交概要");
                changeTracker.AddLine($"立档方式：新建电子介质袋 / 模式 [{request.SubmissionMode}]");
                changeTracker.AddLine($"目标电子袋编号：{archiveUnit.ElectronicArchiveNo}");
                changeTracker.AddLine($"本次入袋明细数：{mediaItemIds.Count}");
                AppendRetainedHardDiskUsageSummary(changeTracker, request, archiveUnit);

                await PersistPendingExternalHardDiskAsync(request.PendingExternalHardDisk, currentUser);
                var borrowedHardDiskCandidate = await ResolveBorrowedHardDiskCandidateForSubmissionAsync(request, mediaEntries);
                await CreateElectronicArchiveUnitCoreAsync(
                    archiveUnit,
                    mediaItems,
                    request.FilingStoragePathByMediaItemId,
                    ResolveSubmissionMediumCode(request),
                    borrowedHardDiskCandidate,
                    currentUser,
                    request.PendingExternalHardDisk);
                var filingMediaEntryIds = mediaEntries.Select(entry => entry.Id).ToList();
                await FinalizeRetainedHardDiskAfterSubmissionAsync(
                    request with { BorrowedHardDiskCandidate = borrowedHardDiskCandidate },
                    archiveUnit,
                    filingMediaEntryIds,
                    currentUser,
                    archiveUnit.ArchivedDate);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
            finally
            {
                _submissionChangeTracker = null;
            }

            return new ElectronicArchiveSubmissionResult(archiveUnit.ElectronicArchiveNo, mediaItemIds.Count, false, changeTracker.BuildReport());
        }

        /// <inheritdoc/>
        public async Task<ElectronicArchiveSubmissionResult> SubmitAppendElectronicArchiveUnitAsync(ElectronicArchiveSubmissionRequest request, User? currentUser)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.ArchiveUnit);

            var (mediaItemIds, mediaItems, mediaEntries) = await ResolveElectronicSubmissionAsync(request);
            EnrichBorrowedHardDiskSubmissionAsync(request, mediaEntries);
            ValidateElectronicSubmissionRequest(request, mediaItemIds, mediaEntries, requireExistingUnitId: true);
            await ValidateCopySubmissionMediumCapacityAsync(request, mediaItems);

            var archiveUnit = CreateSubmissionArchiveUnit(request.ArchiveUnit, currentUser);
            ApplyOpticalDiscSingleArchiveRules(request, archiveUnit, mediaEntries);

            await using var transaction = await _archiveFilingRepository.BeginTransactionAsync();
            var changeTracker = new ElectronicArchiveSubmissionChangeTracker();
            _submissionChangeTracker = changeTracker;
            try
            {
                changeTracker.BeginSection("提交概要");
                changeTracker.AddLine($"立档方式：并入既有电子介质袋 / 模式 [{request.SubmissionMode}]");
                changeTracker.AddLine($"目标电子袋编号：{archiveUnit.ElectronicArchiveNo}");
                changeTracker.AddLine($"本次入袋明细数：{mediaItemIds.Count}");
                AppendRetainedHardDiskUsageSummary(changeTracker, request, archiveUnit);

                await PersistPendingExternalHardDiskAsync(request.PendingExternalHardDisk, currentUser);
                var borrowedHardDiskCandidate = await ResolveBorrowedHardDiskCandidateForSubmissionAsync(request, mediaEntries);
                await AppendToElectronicArchiveUnitCoreAsync(
                    request.ExistingElectronicArchiveUnitId!.Value,
                    archiveUnit,
                    mediaItems,
                    request.FilingStoragePathByMediaItemId,
                    ResolveSubmissionMediumCode(request),
                    borrowedHardDiskCandidate,
                    currentUser,
                    request.PendingExternalHardDisk);
                var appendFilingMediaEntryIds = mediaEntries.Select(entry => entry.Id).ToList();
                await FinalizeRetainedHardDiskAfterSubmissionAsync(
                    request with { BorrowedHardDiskCandidate = borrowedHardDiskCandidate },
                    archiveUnit,
                    appendFilingMediaEntryIds,
                    currentUser,
                    archiveUnit.ArchivedDate == default ? DateTime.Now : archiveUnit.ArchivedDate);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
            finally
            {
                _submissionChangeTracker = null;
            }

            return new ElectronicArchiveSubmissionResult(archiveUnit.ElectronicArchiveNo, mediaItemIds.Count, true, changeTracker.BuildReport());
        }

        private static void AppendRetainedHardDiskUsageSummary(
            ElectronicArchiveSubmissionChangeTracker changeTracker,
            ElectronicArchiveSubmissionRequest request,
            YearlyElectronicArchiveUnit archiveUnit)
        {
            ArgumentNullException.ThrowIfNull(changeTracker);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(archiveUnit);

            if (!request.IsRetainedHardDiskScenario)
            {
                return;
            }

            string borrowedDiskCode = request.BorrowedHardDiskCandidate?.DiskCode?.Trim() ?? string.Empty;
            string appendTargetLocation = string.IsNullOrWhiteSpace(request.AppendTargetStorageLocation)
                ? archiveUnit.StorageLocation?.Trim() ?? string.Empty
                : request.AppendTargetStorageLocation.Trim();
            string returnedLocation = ResolveBorrowedRetainedDiskReturnLocationDescription(request.BorrowedHardDiskCandidate);

            switch (request.SubmissionMode)
            {
                case ElectronicArchiveSubmissionMode.RetainedHardDiskAppendExistingHardDisk:
                    changeTracker.AddLine(
                        $"介质处置说明：登记介质来源为资料室借出硬盘 [{EmptyAsDash(borrowedDiskCode)}]；本次并档将资料拷贝并并入本项目已立档硬盘（目标档口：{EmptyAsDash(appendTargetLocation)}）；原借出硬盘后续将格式化并归还至 {returnedLocation}。");
                    if (!string.IsNullOrWhiteSpace(borrowedDiskCode))
                    {
                        changeTracker.AddLine(
                            $"归还与解锁说明：借出硬盘 [{borrowedDiskCode}] 将自动办理归还登记，完成格式化空盘归位，并解除 HardDiskRegisterLock 占用锁。"
                        );
                    }
                    break;
                case ElectronicArchiveSubmissionMode.RetainedHardDiskCopyToOpticalDisc:
                    changeTracker.AddLine(
                        $"介质处置说明：登记介质来源为资料室借出硬盘 [{EmptyAsDash(borrowedDiskCode)}]；本次将资料拷贝至光盘立档；原借出硬盘后续将格式化并归还至 {returnedLocation}。");
                    if (!string.IsNullOrWhiteSpace(borrowedDiskCode))
                    {
                        changeTracker.AddLine(
                            $"归还与解锁说明：借出硬盘 [{borrowedDiskCode}] 将自动办理归还登记，完成格式化空盘归位，并解除 HardDiskRegisterLock 占用锁。"
                        );
                    }
                    break;
                case ElectronicArchiveSubmissionMode.RetainedHardDiskDirectNew:
                    changeTracker.AddLine(
                        $"介质处置说明：登记介质来源为资料室借出硬盘 [{EmptyAsDash(borrowedDiskCode)}]；本次直接将该硬盘作为数据盘立档，不执行格式化归还。"
                    );
                    break;
                default:
                    if (request.RequiresFormatRetainedHardDisk)
                    {
                        changeTracker.AddLine(
                            $"介质处置说明：登记介质来源为资料室借出硬盘 [{EmptyAsDash(borrowedDiskCode)}]；立档完成后原借出硬盘将格式化并归还至 {returnedLocation}。"
                        );
                    }
                    break;
            }
        }

        private static string ResolveBorrowedRetainedDiskReturnLocationDescription(HardDiskMediaReturnCandidate? candidate)
        {
            if (candidate == null)
            {
                return "空白硬盘专用档口";
            }

            if (!string.IsNullOrWhiteSpace(candidate.OriginalLocation))
            {
                return $"原存放档口 [{candidate.OriginalLocation.Trim()}]";
            }

            if (!string.IsNullOrWhiteSpace(candidate.BorrowedLocation))
            {
                return $"借出时位置 [{candidate.BorrowedLocation.Trim()}]";
            }

            return "空白硬盘专用档口";
        }

        private static string EmptyAsDash(string value)
            => string.IsNullOrWhiteSpace(value) ? "—" : value;

        public async Task<int> GetBoxCountInCellAsync(string cabinetName, string side, int row, int col)
        {
            string slotKey = BuildArchiveSlotKey(cabinetName, side, row, col);
            if (string.IsNullOrWhiteSpace(slotKey))
            {
                return 0;
            }

            var occupiedSlotBoxCounts = await LoadOccupiedArchiveSlotBoxCountsAsync();
            return occupiedSlotBoxCounts.TryGetValue(slotKey, out int count)
                ? count
                : 0;
        }

        /// <inheritdoc/>
        public async Task<int> GetElectronicUnitCountInCellAsync(string cabinetName, string side, int row, int col)
        {
            string slotCode = BuildElectronicUnitSlotCode(cabinetName, side, row, col);
            if (string.IsNullOrWhiteSpace(slotCode))
            {
                return 0;
            }

            string slotPrefix = slotCode + "-";
            return await _archiveFilingRepository.CountElectronicUnitsInSlotAsync(slotCode, slotPrefix);
        }

        public async Task<int> GetMinimumAvailableElectronicSequenceInCellAsync(
            string cabinetName,
            string side,
            int row,
            int col,
            int? excludeUnitId = null)
        {
            string slotCode = BuildElectronicUnitSlotCode(cabinetName, side, row, col);
            if (string.IsNullOrWhiteSpace(slotCode))
            {
                return 1;
            }

            string slotPrefix = slotCode + "-";
            var occupiedIndexes = await _archiveFilingRepository.GetElectronicUnitSequenceIndexesInSlotAsync(
                slotCode,
                slotPrefix,
                excludeUnitId);
            return ArchiveSlotLocationSupport.ResolveMinimumAvailableSequence(occupiedIndexes);
        }

        public async Task<ArchiveBoxLocationSuggestion?> SuggestArchiveBoxLocationAsync(string projectName, string year, string boxSpecification)
        {
            if (string.IsNullOrWhiteSpace(boxSpecification))
            {
                return null;
            }

            string normalizedSpecification = NormalizeArchiveBoxSpecification(boxSpecification);
            var specificationLookup = (await _archiveFilingRepository.GetArchiveBoxSpecificationsAsync())
                .ToDictionary(item => item.Name, item => item, StringComparer.OrdinalIgnoreCase);
            EnsureDefaultArchiveBoxSpecifications(specificationLookup);

            var slotSpecificationLookup = (await _archiveFilingRepository.GetCabinetSlotSpecificationsAsync())
                .ToDictionary(item => item.CabinetTypeCode, item => item, StringComparer.OrdinalIgnoreCase);
            var specialRules = await _archiveFilingRepository.GetEnabledCabinetSlotSpecialRulesBySpecificationAsync(normalizedSpecification);
            var cabinets = (await _archiveFilingRepository.GetNonMagneticCabinetsAsync())
                .OrderBy(item => item.Type)
                .ThenBy(item => CabinetSelectionSupport.GetTraditionalCabinetNameOrder(item.Name))
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var existingBoxes = await _archiveFilingRepository.GetExistingYearlyArchiveBoxesWithCabinetAsync();
            var occupiedSlotBoxCounts = await LoadOccupiedArchiveSlotBoxCountsAsync();
            var placementLookup = (await _archiveFilingRepository.GetArchiveBoxPlacementsAsync())
                .ToDictionary(item => item.BoxCode, item => item, StringComparer.OrdinalIgnoreCase);

            string normalizedProjectName = projectName?.Trim() ?? string.Empty;
            string normalizedYear = year?.Trim() ?? string.Empty;
            var sameYearSameProjectSlotKeys = existingBoxes
                .Where(item => string.Equals(item.Year, normalizedYear, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.ProjectName, normalizedProjectName, StringComparison.OrdinalIgnoreCase))
                .Select(item => BuildArchiveSlotKey(item.CabinetName, item.Side, item.Row, item.Column))
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            string sameYearLastProjectSlotKey = ResolveLatestSlotKey(existingBoxes
                .Where(item => string.Equals(item.Year, normalizedYear, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(item.ProjectName, normalizedProjectName, StringComparison.OrdinalIgnoreCase)));
            string recentLastProjectSlotKey = ResolveLatestSlotKey(existingBoxes
                .Where(item => !string.Equals(item.ProjectName, normalizedProjectName, StringComparison.OrdinalIgnoreCase)));
            string firstFullyEmptyStackBottomSlotKey = existingBoxes.Count == 0
                ? ResolveFirstFullyEmptyStackBottomSlotKey(cabinets, occupiedSlotBoxCounts.Keys)
                : string.Empty;

            var candidates = new List<(int Priority, string CabinetName, string Side, int Row, int Column, int ExistingCount, string SuggestedCode, string Summary)>();
            var fallbackCandidates = new List<(int Priority, string CabinetName, string Side, int Row, int Column, int ExistingCount, string SuggestedCode, string Summary)>();

            foreach (var cabinet in cabinets)
            {
                string cabinetTypeCode = GetCabinetTypeCode(cabinet.Type);
                decimal slotWidth = ResolveSlotWidth(cabinet.Type, slotSpecificationLookup);

                var sides = cabinet.FaceCount > 1 ? new[] { "A", "B" } : new[] { "A" };
                for (int row = 1; row <= cabinet.LayerCount; row++)
                {
                    for (int column = 1; column <= cabinet.ColumnCount; column++)
                    {
                        foreach (var side in sides)
                        {
                            if (!TryResolvePlacementMode(cabinet.Name, side, row, column, normalizedSpecification, specialRules, out string placementMode))
                            {
                                continue;
                            }

                            var slotBoxes = existingBoxes
                                .Where(item => string.Equals(item.CabinetName, cabinet.Name, StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(item.Side, side, StringComparison.OrdinalIgnoreCase)
                                    && item.Row == row
                                    && item.Column == column)
                                .OrderBy(item => item.BoxIndex)
                                .ToList();

                            double occupiedWidth = slotBoxes.Sum(box => ResolveOccupiedWidthForBox(box, placementLookup, specificationLookup));
                            double nextBoxWidth = ResolveOccupiedWidth(placementMode, normalizedSpecification, specificationLookup);

                            string currentSlotKey = BuildArchiveSlotKey(cabinet.Name, side, row, column);
                            int stagePriority = ResolveSuggestionStagePriority(
                                currentSlotKey,
                                sameYearSameProjectSlotKeys,
                                sameYearLastProjectSlotKey,
                                recentLastProjectSlotKey,
                                firstFullyEmptyStackBottomSlotKey);
                            int physicalBoxCount = occupiedSlotBoxCounts.TryGetValue(currentSlotKey, out int slotBoxCount)
                                ? slotBoxCount
                                : 0;
                            int priority = stagePriority * 10 + (physicalBoxCount == 0 ? 0 : 1);
                            int nextIndex = physicalBoxCount + 1;
                            string suggestedCode = $"{cabinet.Name}{side}-{row}-{column}-{nextIndex:D2}";
                            string summary = stagePriority switch
                            {
                                0 => $"建议优先使用同年度同项目档口 {cabinet.Name}{side}-{row}-{column}。",
                                1 => $"建议使用同年度最近项目档口 {cabinet.Name}{side}-{row}-{column}。",
                                2 => $"建议使用近期最近项目档口 {cabinet.Name}{side}-{row}-{column}。",
                                3 => $"当前尚无年度资料立档，建议使用首个上下所有层均为空的底层档口 {cabinet.Name}{side}-{row}-{column}。",
                                _ => physicalBoxCount == 0
                                    ? $"建议使用空档口 {cabinet.Name}{side}-{row}-{column}。"
                                    : $"建议使用仍有余量的档口 {cabinet.Name}{side}-{row}-{column}。"
                            };

                            var slotCandidate = (priority, cabinet.Name, side, row, column, physicalBoxCount, suggestedCode, summary);
                            fallbackCandidates.Add(slotCandidate);

                            if (occupiedWidth + nextBoxWidth > (double)slotWidth)
                            {
                                continue;
                            }

                            candidates.Add(slotCandidate);
                        }
                    }
                }
            }

            var selectedCandidate = candidates
                .OrderBy(item => item.Priority)
                .ThenBy(item => CabinetSelectionSupport.GetTraditionalCabinetNameOrder(item.CabinetName))
                .ThenBy(item => item.CabinetName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Side, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Row)
                .ThenBy(item => item.Column)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(selectedCandidate.CabinetName))
            {
                var fallbackCandidate = fallbackCandidates
                    .OrderBy(item => item.Priority)
                    .ThenBy(item => item.ExistingCount)
                    .ThenBy(item => CabinetSelectionSupport.GetTraditionalCabinetNameOrder(item.CabinetName))
                    .ThenBy(item => item.CabinetName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Side, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Row)
                    .ThenBy(item => item.Column)
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(fallbackCandidate.CabinetName))
                {
                    selectedCandidate = (
                        fallbackCandidate.Priority,
                        fallbackCandidate.CabinetName,
                        fallbackCandidate.Side,
                        fallbackCandidate.Row,
                        fallbackCandidate.Column,
                        fallbackCandidate.ExistingCount,
                        fallbackCandidate.SuggestedCode,
                        $"{fallbackCandidate.Summary} 当前未找到严格满足容量规则的档口，已回退为占用最少的可用档口建议，请人工确认。");
                }
            }

            return string.IsNullOrWhiteSpace(selectedCandidate.CabinetName)
                ? null
                : new ArchiveBoxLocationSuggestion(selectedCandidate.CabinetName, selectedCandidate.Side, selectedCandidate.Row, selectedCandidate.Column, selectedCandidate.ExistingCount, selectedCandidate.SuggestedCode, selectedCandidate.Summary);
        }

        public async Task CreateArchiveBoxAsync(YearlyArchiveBox newBox, List<int> mediaItemIds)
        {
            await using var transaction = await _archiveFilingRepository.BeginTransactionAsync();
            try
            {
                ArgumentNullException.ThrowIfNull(newBox);
                ValidateArchiveBox(newBox);

                DateTime archivedAt = DateTime.Now;
                var mediaItems = await LoadSimulatedMediaItemsForArchivingAsync(mediaItemIds);
                var records = await LoadRegisterRecordsForSimulatedArchivingAsync(mediaItems);

                newBox.RegisterRecords.AddRange(records);
                _archiveFilingRepository.AddArchiveBox(newBox);

                await _archiveFilingRepository.SaveChangesAsync();

                var createdLinks = AddMediaItemLinks(newBox.Id, mediaItems.Select(item => item.Id), archivedAt);
                UpsertArchiveBoxPlacement(newBox, archivedAt);
                await _archiveFilingRepository.SaveChangesAsync();

                await _filingFactWriter.WriteForSimulatedLinksAsync(
                    newBox,
                    createdLinks,
                    mediaItems,
                    archivedAt,
                    newBox.ArchivedBy);

                await UpdateSimulatedArchiveStatusesAsync(records.Select(item => item.Id), archivedAt);

                await _archiveFilingRepository.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task CreateElectronicArchiveUnitAsync(YearlyElectronicArchiveUnit newUnit, List<int> mediaEntryIds)
        {
            await using var transaction = await _archiveFilingRepository.BeginTransactionAsync();
            try
            {
                var entries = await _archiveFilingRepository.GetElectronicMediaEntriesForArchivingAsync(mediaEntryIds);
                var mediaItemIds = entries.SelectMany(entry => entry.Items).Select(item => item.Id).Distinct().ToList();
                var mediaItems = await LoadElectronicMediaItemsForArchivingAsync(mediaItemIds);
                await CreateElectronicArchiveUnitCoreAsync(
                    newUnit,
                    mediaItems,
                    new Dictionary<int, string>(),
                    newUnit.LinkedMediumCodes?.Trim() ?? string.Empty);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task AppendToArchiveBoxAsync(int boxId, List<int> mediaItemIds)
        {
            await using var transaction = await _archiveFilingRepository.BeginTransactionAsync();
            try
            {
                DateTime archivedAt = DateTime.Now;
                var box = await _archiveFilingRepository.GetArchiveBoxWithRegisterRecordsAsync(boxId);

                if (box == null)
                {
                    throw new InvalidOperationException($"未找到指定档案盒：{boxId}");
                }

                ValidateArchiveBox(box);

                var mediaItems = await LoadSimulatedMediaItemsForArchivingAsync(mediaItemIds);
                var records = await LoadRegisterRecordsForSimulatedArchivingAsync(mediaItems);
                var existingRecordIds = box.RegisterRecords.Select(item => item.Id).ToHashSet();

                foreach (var record in records.Where(item => !existingRecordIds.Contains(item.Id)))
                {
                    box.RegisterRecords.Add(record);
                }

                var createdLinks = AddMediaItemLinks(box.Id, mediaItems.Select(item => item.Id), archivedAt);
                UpsertArchiveBoxPlacement(box, archivedAt);
                await _archiveFilingRepository.SaveChangesAsync();

                await _filingFactWriter.WriteForSimulatedLinksAsync(
                    box,
                    createdLinks,
                    mediaItems,
                    archivedAt,
                    box.ArchivedBy);

                await UpdateSimulatedArchiveStatusesAsync(records.Select(item => item.Id), archivedAt);

                await _archiveFilingRepository.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task AppendToElectronicArchiveUnitAsync(int unitId, YearlyElectronicArchiveUnit updatedUnit, List<int> mediaEntryIds, HardDiskMediaReturnCandidate? borrowedHardDiskCandidate = null, User? currentUser = null)
        {
            await using var transaction = await _archiveFilingRepository.BeginTransactionAsync();
            try
            {
                var entries = await _archiveFilingRepository.GetElectronicMediaEntriesForArchivingAsync(mediaEntryIds);
                var mediaItemIds = entries.SelectMany(entry => entry.Items).Select(item => item.Id).Distinct().ToList();
                var mediaItems = await LoadElectronicMediaItemsForArchivingAsync(mediaItemIds);
                await AppendToElectronicArchiveUnitCoreAsync(
                    unitId,
                    updatedUnit,
                    mediaItems,
                    new Dictionary<int, string>(),
                    updatedUnit.LinkedMediumCodes?.Trim() ?? string.Empty,
                    borrowedHardDiskCandidate,
                    currentUser);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public Task AppendToElectronicArchiveUnitAsync(int unitId, YearlyElectronicArchiveUnit updatedUnit, List<int> mediaEntryIds)
            => AppendToElectronicArchiveUnitAsync(unitId, updatedUnit, mediaEntryIds, null, null);

        private async Task AppendToElectronicArchiveUnitCoreAsync(
            int unitId,
            YearlyElectronicArchiveUnit updatedUnit,
            List<YearlyArchiveRegisterMediaItem> mediaItems,
            IReadOnlyDictionary<int, string> filingStoragePathByMediaItemId,
            string mediumCode,
            HardDiskMediaReturnCandidate? borrowedHardDiskCandidate,
            User? currentUser,
            PendingExternalHardDiskRegistration? pendingExternalHardDisk = null)
        {
            ArgumentNullException.ThrowIfNull(updatedUnit);
            ArgumentNullException.ThrowIfNull(mediaItems);

            var unit = await _archiveFilingRepository.GetElectronicArchiveUnitWithDetailsAsync(unitId);

            if (unit == null)
            {
                throw new InvalidOperationException($"未找到指定电子立档单元：{unitId}");
            }

            var mediaEntries = mediaItems
                .Select(item => item.MediaEntry!)
                .DistinctBy(item => item.Id)
                .ToList();
            var records = mediaEntries
                .Select(item => item.RegisterRecord!)
                .DistinctBy(item => item.Id)
                .ToList();
            DateTime archivedAt = DateTime.Now;
            ValidateElectronicAppendConstraints(unit, updatedUnit, records);
            var mergedUnit = MergeElectronicArchiveUnit(unit, updatedUnit, archivedAt);
            var linkedMedia = await PrepareElectronicArchiveUnitAsync(mergedUnit, archivedAt, borrowedHardDiskCandidate, pendingExternalHardDisk);

            ApplyElectronicArchiveUnitUpdates(unit, mergedUnit);
            MergeElectronicArchiveMediumLinks(unit, linkedMedia);
            await UpsertElectronicArchiveDiscLinksAsync(unit, archivedAt);
            var createdItemLinks = AddElectronicMediaItemLinks(unit, mediaItems, filingStoragePathByMediaItemId, mediumCode, archivedAt);
            SyncElectronicMediaEntryLinksAfterItemFiling(unit, mediaItems, archivedAt);

            _submissionChangeTracker?.BeginSection("电子介质袋（YearlyElectronicArchiveUnit）");
            _submissionChangeTracker?.AddLine(
                $"并入电子介质袋 [{unit.ElectronicArchiveNo}]；本次新增关联 {mediaItems.Count} 条资料明细；"
                + $"档口 [{unit.StorageLocation}]；关联硬盘 [{unit.LinkedMediumCodes}]");
            foreach (var item in mediaItems)
            {
                string formNo = item.MediaEntry?.RegisterRecord?.FormNo?.Trim() ?? "-";
                _submissionChangeTracker?.AddLine(
                    $"资料子项 Id={item.Id}（单号 {formNo} / {item.ContentDesc}）已并入电子袋");
            }

            var filingMediaEntryIds = mediaEntries.Select(entry => entry.Id).ToList();
            await MaintainBorrowedHardDiskReturnAsync(
                unit,
                linkedMedia,
                borrowedHardDiskCandidate,
                currentUser,
                archivedAt,
                filingMediaEntryIds);

            var existingRecordIds = unit.RegisterRecords.Select(item => item.Id).ToHashSet();

            foreach (var record in records.Where(item => !existingRecordIds.Contains(item.Id)))
            {
                unit.RegisterRecords.Add(record);
            }

            await _archiveFilingRepository.SaveChangesAsync();

            await _filingFactWriter.WriteForElectronicLinksAsync(
                unit,
                createdItemLinks,
                archivedAt,
                unit.ArchivedBy);

            await UpdateElectronicArchiveStatusesAsync(records.Select(item => item.Id), archivedAt);
            TrackRegisterRecordStatusUpdates(records, archivedAt);

            await _archiveFilingRepository.SaveChangesAsync();
        }

        public async Task<bool> IsArchiveSequenceExistsAsync(string sequenceNo)
        {
            return await _archiveFilingRepository.IsArchiveSequenceExistsAsync(sequenceNo);
        }

        public async Task<bool> IsElectronicArchiveNoExistsAsync(string sequenceNo)
        {
            return await _archiveFilingRepository.IsElectronicArchiveNoExistsAsync(sequenceNo);
        }

        public async Task<IReadOnlyList<HardDiskElectronicArchiveLinkInfo>> GetElectronicArchiveLinkInfosAsync(IEnumerable<int> mediumIds)
        {
            ArgumentNullException.ThrowIfNull(mediumIds);

            var targetIds = mediumIds.Distinct().ToList();
            if (targetIds.Count == 0)
            {
                return Array.Empty<HardDiskElectronicArchiveLinkInfo>();
            }

            return await _archiveFilingRepository.GetElectronicArchiveLinkInfosAsync(targetIds);
        }

        public async Task DeleteRecordAsync(int id)
        {
            var record = await _archiveFilingRepository.GetRegisterRecordForDeletionAsync(id);

            if (record != null)
            {
                var attachments = await _archiveFilingRepository.GetRegisterAttachmentsByBusinessIdAsync(id);
                _archiveFilingRepository.RemoveAttachments(attachments);
                _archiveFilingRepository.RemoveRegisterRecord(record);
                await _archiveFilingRepository.SaveChangesAsync();
            }
        }

        private static int? ParseYear(string? year)
        {
            return int.TryParse(year, out int parsedYear) ? parsedYear : null;
        }

        private async Task<List<YearlyArchiveRegisterRecord>> LoadRegisterRecordsForArchivingAsync(List<int> recordIds)
        {
            if (recordIds == null || recordIds.Count == 0)
            {
                throw new ArgumentException("至少需要一个登记单ID。", nameof(recordIds));
            }

            var records = await _archiveFilingRepository.GetRegisterRecordsForArchivingAsync(recordIds);

            if (records.Count != recordIds.Distinct().Count())
            {
                throw new InvalidOperationException("One or more archive register records were not found.");
            }

            return records;
        }

        private async Task<List<YearlyArchiveRegisterMediaItem>> LoadSimulatedMediaItemsForArchivingAsync(List<int> mediaItemIds)
        {
            if (mediaItemIds == null || mediaItemIds.Count == 0)
            {
                throw new ArgumentException("至少需要一个资料子项。", nameof(mediaItemIds));
            }

            var items = await _archiveFilingRepository.GetSimulatedMediaItemsForArchivingAsync(mediaItemIds);

            if (items.Count != mediaItemIds.Distinct().Count())
            {
                throw new InvalidOperationException("One or more archive media items were not found.");
            }

            if (items.Any(item => item.MediaEntry == null || item.MediaEntry.RegisterRecord == null))
            {
                throw new InvalidOperationException(
                    "资料子项缺少登记单关联信息。常见原因是登记单保存后介质明细已重建，但立档操作台仍引用旧子项。请先从操作台移除该登记单并重新加入，或刷新待立档列表后重试。");
            }

            if (items.Any(item => !string.Equals(item.MediaEntry!.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("仅模拟介质资料子项允许归入档案盒。");
            }

            if (items.Any(item => item.ArchiveBoxLinks.Any()))
            {
                throw new InvalidOperationException("所选资料子项中包含已立档内容，请刷新后重试。");
            }

            return items;
        }

        private async Task<List<YearlyArchiveRegisterMedia>> LoadElectronicMediaEntriesForArchivingAsync(List<int> mediaEntryIds)
        {
            if (mediaEntryIds == null || mediaEntryIds.Count == 0)
            {
                throw new ArgumentException("至少需要一个电子介质条目。", nameof(mediaEntryIds));
            }

            var entries = await _archiveFilingRepository.GetElectronicMediaEntriesForArchivingAsync(mediaEntryIds);

            if (entries.Count != mediaEntryIds.Distinct().Count())
            {
                throw new InvalidOperationException("One or more archive register media entries were not found.");
            }

            if (entries.Any(item => item.RegisterRecord == null))
            {
                throw new InvalidOperationException("电子介质条目缺少登记单关联信息。");
            }

            if (entries.Any(item => !string.Equals(item.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("仅电子介质条目允许归入电子介质袋。");
            }

            var archivedEntries = entries
                .Where(item => item.ElectronicArchiveUnitLinks.Any())
                .Select(item => item.RegisterRecord!.FormNo + "/" + item.MediaType)
                .ToList();

            if (archivedEntries.Count > 0)
            {
                throw new InvalidOperationException($"所选电子介质中包含已立档内容，请刷新后重试：{string.Join("；", archivedEntries)}");
            }

            return entries;
        }

        private async Task<List<YearlyArchiveRegisterRecord>> LoadRegisterRecordsForSimulatedArchivingAsync(IEnumerable<YearlyArchiveRegisterMediaItem> mediaItems)
        {
            ArgumentNullException.ThrowIfNull(mediaItems);

            var recordIds = mediaItems
                .Select(item => item.MediaEntry?.RegisterRecord?.Id ?? 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (recordIds.Count == 0)
            {
                throw new InvalidOperationException("未找到需要更新的登记单。");
            }

            return await _archiveFilingRepository.GetRegisterRecordsForSimulatedArchivingAsync(recordIds);
        }

        private static void ValidateArchiveBox(YearlyArchiveBox box)
        {
            ArgumentNullException.ThrowIfNull(box);

            if (string.IsNullOrWhiteSpace(box.ArchiveSequenceNo))
            {
                throw new ArgumentException("模拟立档编号不能为空。", nameof(box));
            }

            if (string.IsNullOrWhiteSpace(box.Year) || box.Year.Length != 4 || !box.Year.All(char.IsDigit))
            {
                throw new ArgumentException("模拟立档年度必须是四位年份。", nameof(box));
            }

            Match match = ArchiveSequenceNoRegex.Match(box.ArchiveSequenceNo);
            if (!match.Success || !string.Equals(match.Groups[1].Value, box.Year, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"模拟立档编号 [{box.ArchiveSequenceNo}] 不符合 年度模拟-年份-顺序号 规则。");
            }
        }

        private static void UpdateSimulatedArchiveStatuses(IEnumerable<YearlyArchiveRegisterRecord> records, DateTime archivedAt)
        {
            foreach (var record in records)
            {
                bool allSimulatedItemsArchived = record.MediaEntries
                    .Where(media => string.Equals(media.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(media => media.Items)
                    .All(item => item.ArchiveBoxLinks.Any());

                if (allSimulatedItemsArchived)
                {
                    record.MarkSimulatedAsArchived();
                }
                else
                {
                    record.SimulatedArchiveStatus = YearlyArchiveRegisterRecord.TrackPending;
                    record.RefreshOverallArchiveStatus();
                }

                UpdateArchivedDate(record, archivedAt);
            }
        }

        private static void UpdateElectronicArchiveStatuses(IEnumerable<YearlyArchiveRegisterRecord> records, DateTime archivedAt)
        {
            foreach (var record in records)
            {
                bool allElectronicItemsArchived = record.MediaEntries
                    .Where(media => string.Equals(media.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(media => media.Items)
                    .All(item => item.ElectronicArchiveUnitMediaItemLinks.Any());

                if (allElectronicItemsArchived)
                {
                    record.MarkElectronicAsArchived();
                }
                else
                {
                    record.ElectronicArchiveStatus = YearlyArchiveRegisterRecord.TrackPending;
                    record.RefreshOverallArchiveStatus();
                }

                UpdateArchivedDate(record, archivedAt);
            }
        }

        private static void UpdateArchivedDate(YearlyArchiveRegisterRecord record, DateTime archivedAt)
        {
            // 归档时间以"双轨立档进度"为准：模拟轨与电子轨均已立档才落归档日期，
            // 与 RefreshOverallArchiveStatus 保持同口径，避免仅单轨完成时被登记办结状态误置。
            record.ArchivedDate = record.IsElectronicArchived && record.IsSimulatedArchived
                ? record.ArchivedDate ?? archivedAt
                : null;
        }

        private void TrackRegisterRecordStatusUpdates(IEnumerable<YearlyArchiveRegisterRecord> records, DateTime archivedAt)
        {
            if (_submissionChangeTracker == null)
            {
                return;
            }

            _submissionChangeTracker.BeginSection("登记单状态（YearlyArchiveRegisterRecord）");
            foreach (var record in records)
            {
                bool allElectronicArchived = record.MediaEntries
                    .Where(media => string.Equals(media.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(media => media.Items)
                    .All(item => item.ElectronicArchiveUnitMediaItemLinks.Any());

                string electronicStatus = allElectronicArchived
                    ? YearlyArchiveRegisterRecord.TrackArchived.ToString()
                    : YearlyArchiveRegisterRecord.TrackPending.ToString();

                _submissionChangeTracker.AddLine(
                    $"登记单 [{record.FormNo}]：ElectronicArchiveStatus={electronicStatus}；Status={record.Status}；ArchivedDate={(record.ArchivedDate?.ToString("yyyy-MM-dd") ?? "—")}");
            }
        }

    }
}
