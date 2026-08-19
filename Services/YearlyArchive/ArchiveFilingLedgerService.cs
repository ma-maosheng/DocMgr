using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using NPOI.XSSF.UserModel;
using System.IO;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 立档台账查询与导出。
    /// </summary>
    public sealed class ArchiveFilingLedgerService : IArchiveFilingLedgerService
    {
        private readonly IArchiveFilingFactRepository _filingFactRepository;
        private readonly IArchiveOutboundRepository _outboundRepository;

        public ArchiveFilingLedgerService(
            IArchiveFilingFactRepository filingFactRepository,
            IArchiveOutboundRepository outboundRepository)
        {
            _filingFactRepository = filingFactRepository;
            _outboundRepository = outboundRepository;
        }

        public async Task<IReadOnlyList<int>> GetExistingLedgerYearsAsync()
        {
            return await _filingFactRepository.GetDistinctLedgerArchiveYearsAsync();
        }

        public async Task<IReadOnlyList<FilingLedgerProjectFilterItem>> GetProjectOptionsForYearAsync(string? archiveYear)
        {
            return await _filingFactRepository.GetLedgerProjectsByArchiveYearAsync(archiveYear);
        }

        public async Task<IReadOnlyList<FilingLedgerRow>> SearchAsync(FilingLedgerSearchCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);

            var facts = await _filingFactRepository.SearchLedgerAsync(criteria);
            if (facts.Count == 0)
            {
                return Array.Empty<FilingLedgerRow>();
            }

            var mediaItemIds = facts
                .Select(fact => fact.MediaItemId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            var registerMediaIds = facts
                .Select(fact => fact.RegisterMediaId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var mediaItems = await _filingFactRepository.GetRegisterMediaItemsWithSupplementsAsync(mediaItemIds);
            var registerMedias = await _filingFactRepository.GetRegisterMediasByIdsAsync(registerMediaIds);
            var archivePurposeByRegisterRecordId = await _filingFactRepository.GetArchivePurposesByRegisterRecordIdsAsync(
                facts.Select(fact => fact.RegisterRecordId).Where(id => id > 0).Distinct().ToList());

            var supplementByMediaItemId = mediaItems.ToDictionary(
                item => item.Id,
                BuildRegisterSupplementFromMediaItem);
            var mediaTypeByRegisterMediaId = registerMedias.ToDictionary(
                media => media.Id,
                media => media.MediaType?.Trim() ?? string.Empty);

            var simulatedFactIds = facts
                .Where(fact => string.Equals(
                    fact.MediaKind,
                    ArchiveRegisterDomainValues.MediaKindSimulated,
                    StringComparison.Ordinal))
                .Select(fact => fact.Id)
                .ToList();
            var copySnapshots = simulatedFactIds.Count == 0
                ? new Dictionary<int, SimulatedFilingFactCopyCountSnapshot>()
                : await _outboundRepository.GetSimulatedFilingFactCopyCountSnapshotsByFilingFactIdsAsync(simulatedFactIds);

            return facts
                .Select(fact => MapRow(
                    fact,
                    supplementByMediaItemId,
                    mediaTypeByRegisterMediaId,
                    archivePurposeByRegisterRecordId,
                    copySnapshots))
                .ToList();
        }

        public async Task<IReadOnlyList<FilingLedgerContentEntryInfo>> GetContentEntriesByMediaItemIdAsync(
            int mediaItemId,
            string? filingStoragePath)
        {
            if (mediaItemId <= 0)
            {
                return Array.Empty<FilingLedgerContentEntryInfo>();
            }

            var entries = await _filingFactRepository.GetElectronicContentEntriesByMediaItemIdsAsync(
                new[] { mediaItemId });

            return entries
                .Select(entry => ContentEntrySearchSupport.ToLedgerInfo(entry, filingStoragePath))
                .ToList();
        }

        public Task ExportAsync(string filePath, IReadOnlyList<FilingLedgerRow> rows)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("导出文件路径不能为空。", nameof(filePath));
            }

            string? directoryPath = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("导出文件目录无效。", nameof(filePath));
            }

            Directory.CreateDirectory(directoryPath);

            return Task.Run(() =>
            {
                using var workbook = new XSSFWorkbook();
                var sheet = workbook.CreateSheet("立档台账");
                string[] headers =
                [
                    "立档事实编号",
                    "介质类型",
                    "表单号",
                    "资料名称",
                    "子项类型",
                    "子项名称",
                    "密级",
                    "内容数量",
                    "库内/立档",
                    "灭失份数",
                    "待还份数",
                    "不还份数",
                    "项目名称",
                    "提供单位",
                    "申请人",
                    "容器类型",
                    "容器编号",
                    "当前容器编号",
                    "立档位置",
                    "当前位置",
                    "档案柜",
                    "盒位置编码",
                    "盒规格",
                    "存储载体",
                    "介质编号",
                    "立档存储路径",
                    "数据量(MB)",
                    "立档时间",
                    "立档人",
                    "生命周期",
                    "借出提示",
                    "原件/备份",
                    "处置说明",
                    "生命周期备注",
                    "生命周期更新时间"
                ];

                var headerRow = sheet.CreateRow(0);
                for (int i = 0; i < headers.Length; i++)
                {
                    headerRow.CreateCell(i).SetCellValue(headers[i]);
                    sheet.SetColumnWidth(i, 18 * 256);
                }

                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    var item = rows[rowIndex];
                    var row = sheet.CreateRow(rowIndex + 1);
                    int col = 0;
                    row.CreateCell(col++).SetCellValue(item.FilingFactNo);
                    row.CreateCell(col++).SetCellValue(item.MediaKind);
                    row.CreateCell(col++).SetCellValue(item.FormNo);
                    row.CreateCell(col++).SetCellValue(item.MaterialName);
                    row.CreateCell(col++).SetCellValue(item.ItemType);
                    row.CreateCell(col++).SetCellValue(item.ItemName);
                    row.CreateCell(col++).SetCellValue(item.ConfidentialLevel);
                    row.CreateCell(col++).SetCellValue(item.ContentCount);
                    row.CreateCell(col++).SetCellValue(item.CopyCountStatusDisplay);
                    row.CreateCell(col++).SetCellValue(item.IsSimulatedMedia ? item.LostCopyCount : 0);
                    row.CreateCell(col++).SetCellValue(item.IsSimulatedMedia ? item.PendingReturnCopyCount : 0);
                    row.CreateCell(col++).SetCellValue(item.IsSimulatedMedia ? item.NoReturnCopyCount : 0);
                    row.CreateCell(col++).SetCellValue(item.ProjectName);
                    row.CreateCell(col++).SetCellValue(item.ProvideUnit);
                    row.CreateCell(col++).SetCellValue(item.ApplicantName);
                    row.CreateCell(col++).SetCellValue(item.ContainerKindDisplay);
                    row.CreateCell(col++).SetCellValue(item.ContainerCode);
                    row.CreateCell(col++).SetCellValue(item.CurrentContainerCode);
                    row.CreateCell(col++).SetCellValue(item.StorageLocation);
                    row.CreateCell(col++).SetCellValue(item.CurrentStorageLocation);
                    row.CreateCell(col++).SetCellValue(item.CabinetName);
                    row.CreateCell(col++).SetCellValue(item.BoxLocationCode);
                    row.CreateCell(col++).SetCellValue(item.BoxSpecs);
                    row.CreateCell(col++).SetCellValue(item.StorageCarrierType);
                    row.CreateCell(col++).SetCellValue(item.MediumCode);
                    row.CreateCell(col++).SetCellValue(item.FilingStoragePath);
                    row.CreateCell(col++).SetCellValue((double)item.DataSizeMb);
                    row.CreateCell(col++).SetCellValue(item.FiledAtDisplay);
                    row.CreateCell(col++).SetCellValue(item.FiledBy);
                    row.CreateCell(col++).SetCellValue(item.LifecycleStatusDisplay);
                    row.CreateCell(col++).SetCellValue(item.BorrowHintDisplay);
                    row.CreateCell(col++).SetCellValue(item.ArchiveCopyRoleDisplay);
                    row.CreateCell(col++).SetCellValue(item.Disposition);
                    row.CreateCell(col++).SetCellValue(item.LifecycleRemark);
                    row.CreateCell(col).SetCellValue(
                        item.LifecycleUpdatedAt?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty);
                }

                using var stream = File.Create(filePath);
                workbook.Write(stream);
            });
        }

        private static FilingLedgerRow MapRow(
            YearlyArchiveFilingFact fact,
            IReadOnlyDictionary<int, FilingLedgerRegisterSupplement> supplementByMediaItemId,
            IReadOnlyDictionary<int, string> mediaTypeByRegisterMediaId,
            IReadOnlyDictionary<int, string> archivePurposeByRegisterRecordId,
            IReadOnlyDictionary<int, SimulatedFilingFactCopyCountSnapshot> copySnapshots)
        {
            var supplement = fact.MediaItemId > 0 && supplementByMediaItemId.TryGetValue(fact.MediaItemId, out var resolvedSupplement)
                ? resolvedSupplement
                : FilingLedgerRegisterSupplement.Empty;
            string registerMediaType = !string.IsNullOrWhiteSpace(supplement.MediaType)
                ? supplement.MediaType
                : fact.RegisterMediaId > 0 && mediaTypeByRegisterMediaId.TryGetValue(fact.RegisterMediaId, out string? mediaType)
                    ? mediaType
                    : string.Empty;
            string archivePurpose = fact.RegisterRecordId > 0
                && archivePurposeByRegisterRecordId.TryGetValue(fact.RegisterRecordId, out string? purpose)
                ? purpose
                : string.Empty;

            bool isSimulated = string.Equals(
                fact.MediaKind,
                ArchiveRegisterDomainValues.MediaKindSimulated,
                StringComparison.Ordinal);
            int currentInArchive = isSimulated
                ? SimulatedInArchiveCopyCountSupport.ResolveCurrentInArchiveCopyCount(
                    fact.ContentCount,
                    copySnapshots.GetValueOrDefault(fact.Id) ?? new SimulatedFilingFactCopyCountSnapshot())
                : Math.Max(1, fact.ContentCount);
            var snapshot = copySnapshots.GetValueOrDefault(fact.Id) ?? new SimulatedFilingFactCopyCountSnapshot();

            return new FilingLedgerRow
            {
                FilingFactId = fact.Id,
                FilingFactNo = fact.FilingFactNo,
                MediaKind = fact.MediaKind,
                RegisterMediaType = registerMediaType,
                MaterialCategory = supplement.MaterialCategory,
                SubCategory = supplement.SubCategory,
                DataOrganizationForm = supplement.DataOrganizationForm,
                ArchivePurpose = archivePurpose,
                RegisterRecordId = fact.RegisterRecordId,
                RegisterMediaId = fact.RegisterMediaId,
                MediaItemId = fact.MediaItemId,
                FormNo = fact.FormNo,
                MaterialName = fact.MaterialName,
                ProjectName = fact.ProjectName,
                ProvideUnit = fact.ProvideUnit,
                ApplicantName = fact.ApplicantName,
                ItemType = fact.ItemType,
                ItemName = fact.ItemName,
                ConfidentialLevel = fact.ConfidentialLevel,
                ContentCount = fact.ContentCount,
                CurrentInArchiveCopyCount = currentInArchive,
                LostCopyCount = isSimulated ? snapshot.LostCopyCount : 0,
                PendingReturnCopyCount = isSimulated ? snapshot.PendingReturnCopyCount : 0,
                NoReturnCopyCount = isSimulated ? snapshot.NoReturnCopyCount : 0,
                ContainerKind = fact.ContainerKind,
                ContainerCode = fact.ContainerCode,
                StorageLocation = fact.StorageLocation,
                CabinetName = fact.CabinetName,
                BoxLocationCode = fact.BoxLocationCode,
                BoxSpecs = fact.BoxSpecs,
                StorageCarrierType = fact.StorageCarrierType,
                Disposition = fact.Disposition,
                MediumCode = fact.MediumCode,
                FilingStoragePath = fact.FilingStoragePath,
                DataSizeMb = fact.DataSizeMb,
                FiledAt = fact.FiledAt,
                FiledBy = fact.FiledBy,
                LifecycleStatus = fact.LifecycleStatus,
                CurrentContainerCode = string.IsNullOrWhiteSpace(fact.CurrentContainerCode)
                    ? fact.ContainerCode
                    : fact.CurrentContainerCode,
                CurrentStorageLocation = string.IsNullOrWhiteSpace(fact.CurrentStorageLocation)
                    ? fact.StorageLocation
                    : fact.CurrentStorageLocation,
                LifecycleUpdatedAt = fact.LifecycleUpdatedAt,
                LifecycleRemark = fact.LifecycleRemark,
                BorrowHintLevel = fact.BorrowHintLevel,
                BorrowHintText = fact.BorrowHintText,
                PrimaryFilingFactId = fact.PrimaryFilingFactId,
                ArchiveCopyRole = string.IsNullOrWhiteSpace(fact.ArchiveCopyRole)
                    ? FilingFactArchiveCopyRole.Original
                    : fact.ArchiveCopyRole
            };
        }

        private static FilingLedgerRegisterSupplement BuildRegisterSupplementFromMediaItem(
            YearlyArchiveRegisterMediaItem mediaItem)
        {
            var detail = mediaItem.ElectronicDetail;
            return new FilingLedgerRegisterSupplement
            {
                MediaType = mediaItem.MediaEntry?.MediaType?.Trim() ?? string.Empty,
                MaterialCategory = detail?.MaterialCategory?.Trim() ?? string.Empty,
                SubCategory = detail?.SubCategory?.Trim() ?? string.Empty,
                DataOrganizationForm = detail?.DataOrganizationForm?.Trim() ?? string.Empty,
            };
        }

        private sealed class FilingLedgerRegisterSupplement
        {
            public static FilingLedgerRegisterSupplement Empty { get; } = new();

            public string MediaType { get; init; } = string.Empty;

            public string MaterialCategory { get; init; } = string.Empty;

            public string SubCategory { get; init; } = string.Empty;

            public string DataOrganizationForm { get; init; } = string.Empty;
        }
    }
}
