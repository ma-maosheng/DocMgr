using DocMgr.Models.Cabinets;

using DocMgr.Models.YearlyArchive;

using DocMgr.Repositories.Interfaces;

using DocMgr.Services.Interfaces;

using DocMgr.Services.YearlyArchive;



namespace DocMgr.Services.Cabinets

{

    /// <summary>

    /// 档案盒 / 电子介质袋内容查询：年度资料按资料子项展示，历史存档维持原状。

    /// </summary>

    public sealed class CabinetArchiveBoxContentService : ICabinetArchiveBoxContentService

    {

        private readonly ICabinetOpenLayoutService _cabinetOpenLayoutService;

        private readonly ICabinetOpenLayoutRepository _cabinetOpenLayoutRepository;



        public CabinetArchiveBoxContentService(

            ICabinetOpenLayoutService cabinetOpenLayoutService,

            ICabinetOpenLayoutRepository cabinetOpenLayoutRepository)

        {

            _cabinetOpenLayoutService = cabinetOpenLayoutService;

            _cabinetOpenLayoutRepository = cabinetOpenLayoutRepository;

        }



        public IReadOnlyList<CabinetArchiveBoxContentDescriptor> GetContents(string boxCode)

        {

            if (string.IsNullOrWhiteSpace(boxCode))

            {

                return Array.Empty<CabinetArchiveBoxContentDescriptor>();

            }



            var yearlyBox = _cabinetOpenLayoutRepository.FindInUseYearlyArchiveBoxByLocationCode(boxCode);

            if (yearlyBox != null)

            {

                var yearlyRows = _cabinetOpenLayoutRepository.GetYearlyArchiveBoxMediaItemRows(yearlyBox);

                var reservationsByFactId = LoadActiveReservationsByFactIds(yearlyRows);

                return yearlyRows

                    .Select(row => BuildYearlyArchiveDescriptor(

                        row,

                        CabinetArchiveContainerViewMode.SimulatedArchiveBox,

                        yearlyBox.Year,

                        reservationsByFactId))

                    .ToList();

            }



            return _cabinetOpenLayoutService.GetArchiveBoxContents(boxCode);

        }



        public IReadOnlyList<CabinetArchiveBoxContentDescriptor> GetElectronicBagContents(int electronicArchiveUnitId)

        {

            if (electronicArchiveUnitId <= 0)

            {

                return Array.Empty<CabinetArchiveBoxContentDescriptor>();

            }



            var unit = _cabinetOpenLayoutRepository.FindInUseElectronicArchiveUnitById(electronicArchiveUnitId);

            return unit == null

                ? Array.Empty<CabinetArchiveBoxContentDescriptor>()

                : BuildElectronicBagDescriptors(unit);

        }



        public IReadOnlyList<CabinetArchiveBoxContentDescriptor> GetElectronicBagContentsByLocation(string storageLocationCode)

        {

            if (string.IsNullOrWhiteSpace(storageLocationCode))

            {

                return Array.Empty<CabinetArchiveBoxContentDescriptor>();

            }



            var unit = _cabinetOpenLayoutRepository.FindInUseElectronicArchiveUnitByLocationCode(storageLocationCode);

            return unit == null

                ? Array.Empty<CabinetArchiveBoxContentDescriptor>()

                : BuildElectronicBagDescriptors(unit);

        }



        public bool IsYearlyArchiveBoxAtLocation(string boxCode) =>

            _cabinetOpenLayoutRepository.FindInUseYearlyArchiveBoxByLocationCode(boxCode) != null;



        public bool IsElectronicArchiveBagAtLocation(string storageLocationCode) =>

            _cabinetOpenLayoutRepository.FindInUseElectronicArchiveUnitByLocationCode(storageLocationCode) != null;



        public CabinetElectronicArchiveBagHeader? GetElectronicBagHeader(int electronicArchiveUnitId)

        {

            var unit = _cabinetOpenLayoutRepository.FindInUseElectronicArchiveUnitById(electronicArchiveUnitId);

            return unit == null ? null : BuildElectronicBagHeader(unit);

        }



        public CabinetElectronicArchiveBagHeader? GetElectronicBagHeaderByLocation(string storageLocationCode)

        {

            var unit = _cabinetOpenLayoutRepository.FindInUseElectronicArchiveUnitByLocationCode(storageLocationCode);

            return unit == null ? null : BuildElectronicBagHeader(unit);

        }



        public CabinetArchiveContainerOccupationLockSummary GetArchiveBoxOccupationLockSummary(string boxCode)

        {

            if (string.IsNullOrWhiteSpace(boxCode))

            {

                return CabinetArchiveContainerOccupationLockSummary.Empty;

            }



            var yearlyBox = _cabinetOpenLayoutRepository.FindInUseYearlyArchiveBoxByLocationCode(boxCode);

            if (yearlyBox == null)

            {

                return CabinetArchiveContainerOccupationLockSummary.Empty;

            }



            var withdrawalLock = _cabinetOpenLayoutRepository

                .GetActiveWithdrawalLocksByArchiveBoxIds([yearlyBox.Id])

                .GetValueOrDefault(yearlyBox.Id, CabinetOccupationLockDescriptor.Empty);

            return BuildContainerOccupationLockSummary(withdrawalLock, Array.Empty<CabinetHardDiskOccupationLockInfo>());

        }



        public CabinetArchiveContainerOccupationLockSummary GetElectronicBagOccupationLockSummary(int electronicArchiveUnitId)

        {

            if (electronicArchiveUnitId <= 0)

            {

                return CabinetArchiveContainerOccupationLockSummary.Empty;

            }



            var unit = _cabinetOpenLayoutRepository.FindInUseElectronicArchiveUnitById(electronicArchiveUnitId);

            return unit == null

                ? CabinetArchiveContainerOccupationLockSummary.Empty

                : BuildElectronicBagOccupationLockSummary(unit.Id);

        }



        public CabinetArchiveContainerOccupationLockSummary GetElectronicBagOccupationLockSummaryByLocation(string storageLocationCode)

        {

            if (string.IsNullOrWhiteSpace(storageLocationCode))

            {

                return CabinetArchiveContainerOccupationLockSummary.Empty;

            }



            var unit = _cabinetOpenLayoutRepository.FindInUseElectronicArchiveUnitByLocationCode(storageLocationCode);

            return unit == null

                ? CabinetArchiveContainerOccupationLockSummary.Empty

                : BuildElectronicBagOccupationLockSummary(unit.Id);

        }



        private CabinetArchiveContainerOccupationLockSummary BuildElectronicBagOccupationLockSummary(int electronicArchiveUnitId)

        {

            var withdrawalLock = _cabinetOpenLayoutRepository

                .GetActiveWithdrawalLocksByElectronicUnitIds([electronicArchiveUnitId])

                .GetValueOrDefault(electronicArchiveUnitId, CabinetOccupationLockDescriptor.Empty);

            var hardDiskLocks = _cabinetOpenLayoutRepository.GetHardDiskOccupationLocksByElectronicUnitId(electronicArchiveUnitId);

            return BuildContainerOccupationLockSummary(withdrawalLock, hardDiskLocks);

        }



        private IReadOnlyList<CabinetArchiveBoxContentDescriptor> BuildElectronicBagDescriptors(YearlyElectronicArchiveUnit unit)

        {

            var rows = _cabinetOpenLayoutRepository.GetElectronicArchiveUnitMediaItemRows(unit);

            var reservationsByFactId = LoadActiveReservationsByFactIds(rows);

            var mediaStatusByMediumCode = _cabinetOpenLayoutRepository

                .GetLinkedMediumInventoryStatusesByElectronicUnitId(unit.Id);

            return rows

                .Select(row => BuildYearlyArchiveDescriptor(

                    row,

                    CabinetArchiveContainerViewMode.ElectronicArchiveBag,

                    unit.Year,

                    reservationsByFactId,

                    mediaStatusByMediumCode))

                .ToList();

        }



        private static CabinetElectronicArchiveBagHeader BuildElectronicBagHeader(YearlyElectronicArchiveUnit unit) =>

            new()

            {

                ElectronicArchiveNo = unit.ElectronicArchiveNo?.Trim() ?? string.Empty,

                ProjectName = unit.ProjectName?.Trim() ?? string.Empty,

                Year = unit.Year?.Trim() ?? string.Empty,

                StorageLocation = unit.StorageLocation?.Trim() ?? string.Empty,

                StorageCarrierType = unit.StorageCarrierType?.Trim() ?? string.Empty,

                LinkedMediumCodes = unit.LinkedMediumCodes?.Trim() ?? string.Empty,

                Disposition = unit.Disposition?.Trim() ?? string.Empty,

                ContentSummary = unit.ContentSummary?.Trim() ?? string.Empty,

                MediaCount = Math.Max(0, unit.MediaCount),

                ArchivedBy = unit.ArchivedBy?.Trim() ?? string.Empty,

                ArchivedDateText = unit.ArchivedDate == default

                    ? string.Empty

                    : unit.ArchivedDate.ToString("yyyy-MM-dd"),

                Remarks = unit.Remarks?.Trim() ?? string.Empty,

            };



        private static CabinetArchiveBoxContentDescriptor BuildYearlyArchiveDescriptor(

            YearlyArchiveBoxMediaItemRow row,

            CabinetArchiveContainerViewMode viewMode,

            string? containerYear,

            IReadOnlyDictionary<int, IReadOnlyList<ActiveWithdrawalReservationSnapshot>> reservationsByFactId,

            IReadOnlyDictionary<string, string>? mediaStatusByMediumCode = null)

        {

            var fact = row.Fact;

            var supplement = row.Supplement;

            var breakdown = ArchiveBoxMediaItemCopyCountSupport.Resolve(

                fact,

                row.PendingReturnCopyCount,

                row.NoReturnCopyCount,

                row.LostCopyCount,

                row.InventoryLostCopyCount > 0 ? row.InventoryLostCopyCount : fact.InventoryLostCopyCount,

                row.InventoryScrapCopyCount > 0 ? row.InventoryScrapCopyCount : fact.InventoryScrapCopyCount);



            bool isElectronic = viewMode == CabinetArchiveContainerViewMode.ElectronicArchiveBag

                || string.Equals(fact.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal);

            string mediumInventoryStatusText = isElectronic

                ? ArchiveMediumInventoryStatusSupport.ResolveDisplay(

                    fact.MediumCode,

                    mediaStatusByMediumCode ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))

                : ArchiveMediumInventoryStatusSupport.DisplayNormal;



            string itemType = string.IsNullOrWhiteSpace(fact.ItemType) ? "资料子项" : fact.ItemType.Trim();

            string itemName = string.IsNullOrWhiteSpace(fact.ItemName) ? fact.MaterialName : fact.ItemName;

            string identifier = string.IsNullOrWhiteSpace(fact.FormNo) ? fact.FilingFactNo : fact.FormNo;

            string carrierTypeText = CabinetArchiveBoxContentDisplaySupport.ResolveCarrierTypeText(fact, supplement);

            decimal dataSizeMb = fact.DataSizeMb > 0 ? fact.DataSizeMb : supplement.DataSizeMb;

            var itemReservations = fact.Id > 0

                ? reservationsByFactId.GetValueOrDefault(fact.Id, Array.Empty<ActiveWithdrawalReservationSnapshot>())

                : Array.Empty<ActiveWithdrawalReservationSnapshot>();

            bool hasOccupationLock = itemReservations.Count > 0;

            string occupationLockDisplayText = FormatItemOccupationLockDisplay(itemReservations, isElectronic);



            return new CabinetArchiveBoxContentDescriptor

            {

                BoxCode = isElectronic ? fact.StorageLocation : fact.BoxLocationCode,

                SourceType = "年度资料",

                CategoryText = carrierTypeText,

                IdentifierText = identifier,

                TitleText = itemName,

                MaterialName = fact.MaterialName?.Trim() ?? string.Empty,

                ProjectYear = CabinetArchiveBoxContentDisplaySupport.ResolveProjectYear(row.ProjectYear, containerYear),

                ProjectName = fact.ProjectName?.Trim() ?? string.Empty,

                ProvideUnit = fact.ProvideUnit?.Trim() ?? string.Empty,

                ItemType = itemType,

                ConfidentialLevel = string.IsNullOrWhiteSpace(fact.ConfidentialLevel)

                    ? ArchiveRegisterDomainValues.ConfidentialLevelNone

                    : fact.ConfidentialLevel.Trim(),

                ApprovedCopyCount = Math.Max(0, fact.ContentCount),

                Note = supplement.Note,

                CarrierTypeText = carrierTypeText,

                ApplicantName = fact.ApplicantName?.Trim() ?? string.Empty,

                ArchivePurpose = row.ArchivePurpose?.Trim() ?? string.Empty,

                StoragePath = supplement.StoragePath,

                FilingStoragePath = fact.FilingStoragePath?.Trim() ?? string.Empty,

                MaterialCategory = supplement.MaterialCategory,

                SubCategory = supplement.SubCategory,

                DataOrganizationForm = supplement.DataOrganizationForm,

                DataSizeText = FormatDataSizeText(dataSizeMb),

                ContentEntryBreakdownText = supplement.ContentEntryBreakdownText,

                ContainerCode = fact.ContainerCode?.Trim() ?? string.Empty,

                BoxSpecs = fact.BoxSpecs?.Trim() ?? string.Empty,

                MediumCode = fact.MediumCode?.Trim() ?? string.Empty,

                FiledBy = fact.FiledBy?.Trim() ?? string.Empty,

                ArchiveCopyRoleDisplay = FormatArchiveCopyRoleDisplay(fact.ArchiveCopyRole),

                QuantityText = isElectronic

                    ? mediumInventoryStatusText

                    : FormatSimulatedQuantityText(breakdown),

                DetailText = BuildDetailText(fact),

                DateText = fact.FiledAt == default ? string.Empty : fact.FiledAt.ToString("yyyy-MM-dd"),

                IsYearlyArchiveMediaItem = true,

                IsElectronicMedia = isElectronic,

                FilingFactId = fact.Id,

                RegisterRecordId = fact.RegisterRecordId,

                RegisterMediaId = fact.RegisterMediaId,

                MediaItemId = fact.MediaItemId,

                MediaKind = fact.MediaKind?.Trim() ?? string.Empty,

                FiledCopyCount = breakdown.FiledCopyCount,

                CurrentInArchiveCopyCount = breakdown.CurrentInArchiveCopyCount,

                PendingReturnCopyCount = breakdown.PendingReturnCopyCount,

                NoReturnCopyCount = breakdown.NoReturnCopyCount,

                LostCopyCount = breakdown.LostCopyCount,

                InventoryLostCopyCount = breakdown.InventoryLostCopyCount,

                InventoryScrapCopyCount = breakdown.InventoryScrapCopyCount,

                ElectronicStockStatusText = mediumInventoryStatusText,

                ViewMode = viewMode,

                HasOccupationLock = hasOccupationLock,

                OccupationLockDisplayText = occupationLockDisplayText,

            };

        }



        private IReadOnlyDictionary<int, IReadOnlyList<ActiveWithdrawalReservationSnapshot>> LoadActiveReservationsByFactIds(

            IReadOnlyList<YearlyArchiveBoxMediaItemRow> rows)

        {

            var factIds = rows

                .Select(row => row.Fact.Id)

                .Where(id => id > 0)

                .Distinct()

                .ToList();

            return _cabinetOpenLayoutRepository.GetActiveWithdrawalReservationsByFilingFactIds(factIds);

        }



        private static CabinetArchiveContainerOccupationLockSummary BuildContainerOccupationLockSummary(

            CabinetOccupationLockDescriptor withdrawalLock,

            IReadOnlyList<CabinetHardDiskOccupationLockInfo> hardDiskLocks)

        {

            var lines = new List<string>();

            if (withdrawalLock.HasLock)

            {

                lines.Add(withdrawalLock.ToolTipSupplement);

            }



            foreach (var hardDiskLock in hardDiskLocks)

            {

                lines.Add($"占用锁：{hardDiskLock.DisplayText}");

            }



            if (lines.Count == 0)

            {

                return CabinetArchiveContainerOccupationLockSummary.Empty;

            }



            return new CabinetArchiveContainerOccupationLockSummary

            {

                HasAnyLock = true,

                NoticeTitle = "占用说明",

                NoticeText = string.Join("\n", lines),

            };

        }



        private static string FormatItemOccupationLockDisplay(

            IReadOnlyList<ActiveWithdrawalReservationSnapshot> reservations,

            bool isElectronic)

        {

            if (reservations.Count == 0)

            {

                return "—";

            }



            return string.Join("；", reservations

                .GroupBy(snapshot => snapshot.OutboundNo, StringComparer.OrdinalIgnoreCase)

                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)

                .Select(group =>

                {

                    int copyCount = group.Sum(snapshot => Math.Max(1, snapshot.ReservedCopyCount));

                    string outboundNo = string.IsNullOrWhiteSpace(group.Key) ? "（无单号）" : group.Key.Trim();

                    return isElectronic || copyCount <= 1

                        ? $"出库预订 · {outboundNo}"

                        : $"出库预订 · {outboundNo} · {copyCount}份";

                }));

        }



        private static string FirstNonEmpty(params string?[] values)

        {

            foreach (string? value in values)

            {

                if (!string.IsNullOrWhiteSpace(value))

                {

                    return value.Trim();

                }

            }



            return string.Empty;

        }



        private static string FormatDataSizeText(decimal dataSizeMb) =>

            dataSizeMb > 0 ? $"{dataSizeMb:N2} MB" : string.Empty;



        private static string FormatArchiveCopyRoleDisplay(string? archiveCopyRole) =>

            string.Equals(archiveCopyRole?.Trim(), FilingFactArchiveCopyRole.Backup, StringComparison.Ordinal)

                ? "备份"

                : string.Equals(archiveCopyRole?.Trim(), FilingFactArchiveCopyRole.Original, StringComparison.Ordinal)

                    ? "原件"

                    : string.Empty;



        private static string FormatSimulatedQuantityText(MediaItemCopyCountBreakdown breakdown) =>

            $"立档 {breakdown.FiledCopyCount} 份";



        private static string BuildDetailText(YearlyArchiveFilingFact fact)

        {

            var segments = new List<string>();

            if (!string.IsNullOrWhiteSpace(fact.BorrowHintText))

            {

                segments.Add(fact.BorrowHintText.Trim());

            }



            if (!string.IsNullOrWhiteSpace(fact.LifecycleRemark))

            {

                segments.Add(fact.LifecycleRemark.Trim());

            }



            if (!string.IsNullOrWhiteSpace(fact.FilingFactNo))

            {

                segments.Add($"台账 {fact.FilingFactNo.Trim()}");

            }



            return string.Join("；", segments);

        }

        /// <inheritdoc />
        public IReadOnlyList<SimulatedArchiveBoxPendingReturnDetailRow> GetSimulatedArchiveBoxPendingReturnDetails(string boxCode)
        {
            if (string.IsNullOrWhiteSpace(boxCode))
            {
                return Array.Empty<SimulatedArchiveBoxPendingReturnDetailRow>();
            }

            return _cabinetOpenLayoutRepository.GetSimulatedArchiveBoxPendingReturnDetails(boxCode.Trim());
        }

    }

}


