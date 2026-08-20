using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DocMgr.ViewModels.YearlyArchive
{
    public class ArchiveDetailViewModel : ViewModelBase
    {
        private readonly IArchiveRegisterService _archiveRegisterService;
        private readonly IArchiveFilingSearchService _searchService;
        private readonly IArchiveFilingSearchPoolSession _poolSession;
        private readonly IArchiveFilingFactRepository _filingFactRepository;
        private readonly IDialogService _dialogService;
        private bool _isInitialized;
        private int _recordId;
        private string? _initializeKey;
        private ArchiveDetailHighlightContext? _searchHighlight;
        private string? _filterPoolMediaKind;
        private int? _primaryFilingFactId;
        private YearlyArchiveRegisterRecord? _record;

        public YearlyArchiveRegisterRecord? Record
        {
            get => _record;
            private set => SetProperty(ref _record, value);
        }

        public ObservableCollection<ArchiveDetailMediaEntryItem> ElectronicMediaEntries { get; } = new();
        public ObservableCollection<ArchiveDetailMediaEntryItem> SimulatedMediaEntries { get; } = new();
        public ObservableCollection<ArchiveDetailMediaEntryItem> ProofMediaEntries { get; } = new();
        public ObservableCollection<ArchiveDetailArchiveBoxResult> ArchiveBoxResults { get; } = new();
        public ObservableCollection<ArchiveDetailElectronicUnitResult> ElectronicUnitResults { get; } = new();
        public ObservableCollection<SystemAttachment> Attachments { get; } = new();

        public RelayCommand<SystemAttachment> ViewAttachmentCommand { get; }
        public RelayCommand ExpandAllElectronicMediaItemsCommand { get; }
        public RelayCommand CollapseAllElectronicMediaItemsCommand { get; }

        public bool HasElectronicSubItems => ElectronicMediaEntries.Any(entry => entry.Items.Count > 0);

        public bool HasSearchHighlight { get; private set; }

        public string SearchHighlightSummary =>
            _searchHighlight == null
                ? string.Empty
                : _searchHighlight.HasContentEntryHighlight
                    ? "以下已标记本次检索命中的介质、资料子项、目录/文件明细及对应立档容器。"
                    : "以下已标记本次检索命中的介质、资料子项及对应立档容器。";

        public bool IsFilterSelectionMode =>
            !string.IsNullOrWhiteSpace(_filterPoolMediaKind)
            && string.Equals(
                _filterPoolMediaKind,
                ArchiveRegisterDomainValues.MediaKindElectronic,
                StringComparison.Ordinal);

        public string FilterPoolSummary =>
            IsFilterSelectionMode
                ? $"筛选池：{_poolSession.GetPool(_filterPoolMediaKind!).Count} 条"
                : string.Empty;

        public bool HasFilterPoolSelections =>
            ElectronicUnitResults.SelectMany(unit => unit.Items).Any(CollectFilterSelectionFromUnitItem);

        public RelayCommand AddSelectedToFilterPoolCommand { get; }

        public ArchiveDetailViewModel(
            IArchiveRegisterService archiveRegisterService,
            IArchiveFilingSearchService searchService,
            IArchiveFilingSearchPoolSession poolSession,
            IArchiveFilingFactRepository filingFactRepository,
            IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(archiveRegisterService);
            ArgumentNullException.ThrowIfNull(searchService);
            ArgumentNullException.ThrowIfNull(poolSession);
            ArgumentNullException.ThrowIfNull(filingFactRepository);
            ArgumentNullException.ThrowIfNull(dialogService);

            _archiveRegisterService = archiveRegisterService;
            _searchService = searchService;
            _poolSession = poolSession;
            _filingFactRepository = filingFactRepository;
            _dialogService = dialogService;
            _poolSession.PoolChanged += HandleFilterPoolChanged;
            ViewAttachmentCommand = new RelayCommand<SystemAttachment>(async attachment => await ViewAttachmentAsync(attachment));
            ExpandAllElectronicMediaItemsCommand = new RelayCommand(
                _ => SetAllElectronicMediaItemsExpanded(true),
                _ => HasElectronicSubItems);
            CollapseAllElectronicMediaItemsCommand = new RelayCommand(
                _ => SetAllElectronicMediaItemsExpanded(false),
                _ => HasElectronicSubItems);
            AddSelectedToFilterPoolCommand = new RelayCommand(
                async _ => await AddSelectedToFilterPoolAsync(),
                _ => IsFilterSelectionMode);
        }

        private void HandleFilterPoolChanged(string mediaKind)
        {
            if (!string.Equals(mediaKind, _filterPoolMediaKind, StringComparison.Ordinal))
            {
                return;
            }

            NotifyFilterSelectionStateChanged();
        }

        public async Task InitializeAsync(
            int recordId,
            ArchiveDetailHighlightContext? searchHighlight = null,
            string? filterPoolMediaKind = null,
            int? primaryFilingFactId = null)
        {
            string initializeKey = BuildInitializeKey(recordId, searchHighlight, filterPoolMediaKind, primaryFilingFactId);
            if (_isInitialized && _initializeKey == initializeKey)
            {
                return;
            }

            _recordId = recordId;
            _searchHighlight = searchHighlight;
            _filterPoolMediaKind = filterPoolMediaKind;
            _primaryFilingFactId = primaryFilingFactId;
            _initializeKey = initializeKey;
            _isInitialized = true;
            OnPropertyChanged(nameof(IsFilterSelectionMode));
            OnPropertyChanged(nameof(FilterPoolSummary));
            await LoadRecordAsync(recordId);
        }

        private static string BuildInitializeKey(
            int recordId,
            ArchiveDetailHighlightContext? searchHighlight,
            string? filterPoolMediaKind,
            int? primaryFilingFactId)
        {
            if (searchHighlight == null)
            {
                return $"{recordId}:none:{filterPoolMediaKind}:{primaryFilingFactId}";
            }

            return $"{recordId}:{searchHighlight.MediaKind}:{searchHighlight.RegisterMediaId}:{searchHighlight.MediaItemId}:{searchHighlight.ContainerCode}:{searchHighlight.ContentEntryKeyword}:{searchHighlight.ContentEntryKindFilter}:{string.Join(",", searchHighlight.MatchedContentEntryIds)}:{filterPoolMediaKind}:{primaryFilingFactId}";
        }

        private async Task LoadRecordAsync(int recordId)
        {
            if (recordId <= 0)
            {
                _dialogService.ShowMessage("无效的记录编号。", "提示");
                return;
            }

            try
            {
                var record = await _archiveRegisterService.GetByIdAsync(recordId);
                if (record == null)
                {
                    _dialogService.ShowMessage($"未找到编号为 {recordId} 的登记记录。", "提示");
                    return;
                }

                Record = record;
                await LoadAttachmentsAsync(record.FormNo);
                ApplyDetailCollections(record);
                await ApplyFilingFactBindingAsync(record);
                ApplySearchHighlight();
                NotifyFilterSelectionStateChanged();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"加载资料详情失败：{ex.Message}");
            }
        }

        private async Task LoadAttachmentsAsync(string formNo)
        {
            Attachments.Clear();

            if (string.IsNullOrWhiteSpace(formNo))
            {
                return;
            }

            var attachments = await _archiveRegisterService.GetAttachmentsByFormNoAsync(formNo);
            foreach (var attachment in attachments.OrderByDescending(item => item.UploadTime))
            {
                Attachments.Add(attachment);
            }
        }

        private void ApplyDetailCollections(YearlyArchiveRegisterRecord record)
        {
            ReplaceCollection(ElectronicMediaEntries, record.MediaEntries
                .Where(media => IsElectronicMedia(media) && !IsProofMedia(media))
                .Select(MapMediaEntry)
                .ToList());

            ReplaceCollection(SimulatedMediaEntries, record.MediaEntries
                .Where(media => IsSimulatedMedia(media) && !IsProofMedia(media))
                .Select(MapMediaEntry)
                .ToList());

            ReplaceCollection(ProofMediaEntries, record.MediaEntries
                .Where(IsProofMedia)
                .Select(MapMediaEntry)
                .ToList());

            ReplaceCollection(ArchiveBoxResults, record.ArchiveBoxes
                .OrderBy(item => item.ArchiveSequenceNo)
                .Select(MapArchiveBoxResult)
                .ToList());

            ReplaceCollection(ElectronicUnitResults, record.ElectronicArchiveUnits
                .OrderBy(item => item.ElectronicArchiveNo)
                .Select(MapElectronicUnitResult)
                .ToList());

            OnPropertyChanged(nameof(HasElectronicSubItems));
        }

        private void ApplySearchHighlight()
        {
            ClearSearchHighlights();

            if (_searchHighlight == null)
            {
                HasSearchHighlight = false;
                OnPropertyChanged(nameof(HasSearchHighlight));
                OnPropertyChanged(nameof(SearchHighlightSummary));
                return;
            }

            var highlight = _searchHighlight;
            bool isElectronic = string.Equals(
                highlight.MediaKind,
                ArchiveRegisterDomainValues.MediaKindElectronic,
                StringComparison.Ordinal);

            var registerMediaEntries = isElectronic ? ElectronicMediaEntries : SimulatedMediaEntries;
            HighlightRegisterMediaSection(registerMediaEntries, highlight);

            foreach (var box in ArchiveBoxResults)
            {
                HighlightFilingContainer(box, highlight, box.ArchiveSequenceNo, box.Items);
            }

            foreach (var unit in ElectronicUnitResults)
            {
                if (MatchesContainerCode(highlight.ContainerCode, unit.ElectronicArchiveNo))
                {
                    unit.IsSearchHighlighted = true;
                }

                foreach (var item in unit.Items)
                {
                    if (!MatchesElectronicUnitFilingItem(highlight, item))
                    {
                        continue;
                    }

                    item.IsSearchHighlighted = true;
                    item.IsDetailsExpanded = true;
                    unit.IsSearchHighlighted = true;
                }
            }

            HasSearchHighlight = registerMediaEntries.Any(media => media.IsSearchHighlighted)
                || registerMediaEntries.SelectMany(media => media.Items).Any(item => item.IsSearchHighlighted)
                || registerMediaEntries.SelectMany(media => media.Items)
                    .SelectMany(item => item.ContentEntries)
                    .Any(entry => entry.IsSearchHighlighted)
                || ArchiveBoxResults.Any(box => box.IsSearchHighlighted)
                || ElectronicUnitResults.Any(unit => unit.IsSearchHighlighted || unit.Items.Any(item => item.IsSearchHighlighted));

            OnPropertyChanged(nameof(HasSearchHighlight));
            OnPropertyChanged(nameof(SearchHighlightSummary));
        }

        private static void HighlightRegisterMediaSection(
            IEnumerable<ArchiveDetailMediaEntryItem> mediaEntries,
            ArchiveDetailHighlightContext highlight)
        {
            foreach (var mediaEntry in mediaEntries)
            {
                HighlightRegisterMediaEntry(mediaEntry, highlight);
            }
        }

        private static void HighlightRegisterMediaEntry(
            ArchiveDetailMediaEntryItem mediaEntry,
            ArchiveDetailHighlightContext highlight)
        {
            if (highlight.RegisterMediaId > 0 && mediaEntry.RegisterMediaId == highlight.RegisterMediaId)
            {
                mediaEntry.IsSearchHighlighted = true;
            }

            foreach (var item in mediaEntry.Items)
            {
                if (!MatchesMediaItem(highlight, item))
                {
                    continue;
                }

                item.IsSearchHighlighted = true;
                item.IsDetailsExpanded = true;
                mediaEntry.IsSearchHighlighted = true;
                HighlightContentEntries(item, highlight);
            }
        }

        private static void HighlightContentEntries(
            ArchiveDetailMediaItem item,
            ArchiveDetailHighlightContext highlight)
        {
            if (!highlight.HasContentEntryHighlight)
            {
                return;
            }

            var contentSearchCriteria = highlight.ToContentSearchCriteria();
            bool highlightedAny = false;

            foreach (var entry in item.ContentEntries)
            {
                bool isMatched = highlight.MatchedContentEntryIds.Count > 0
                    ? highlight.MatchedContentEntryIds.Contains(entry.EntryId)
                    : ContentEntrySearchSupport.MatchesEntry(
                        entry.EntryKind,
                        entry.EntryName,
                        contentSearchCriteria);

                if (!isMatched)
                {
                    continue;
                }

                entry.IsSearchHighlighted = true;
                highlightedAny = true;
            }

            if (highlightedAny)
            {
                item.IsSearchHighlighted = true;
                item.IsDetailsExpanded = true;
            }
        }

        private static void HighlightFilingContainer(
            ArchiveDetailArchiveBoxResult container,
            ArchiveDetailHighlightContext highlight,
            string containerCode,
            IReadOnlyList<ArchiveDetailMediaItem> items)
        {
            if (MatchesContainerCode(highlight.ContainerCode, containerCode))
            {
                container.IsSearchHighlighted = true;
            }

            foreach (var item in items)
            {
                if (!MatchesMediaItem(highlight, item))
                {
                    continue;
                }

                item.IsSearchHighlighted = true;
                container.IsSearchHighlighted = true;
                HighlightContentEntries(item, highlight);
            }
        }

        private static bool MatchesElectronicUnitFilingItem(
            ArchiveDetailHighlightContext highlight,
            ArchiveDetailElectronicUnitFilingItem item)
        {
            if (highlight.MediaItemId > 0 && item.MediaItemId == highlight.MediaItemId)
            {
                return true;
            }

            if (highlight.MediaItemId > 0)
            {
                return false;
            }

            return string.Equals(NormalizeMatchText(highlight.ItemType), NormalizeMatchText(item.ItemType), StringComparison.Ordinal)
                && string.Equals(NormalizeMatchText(highlight.ItemName), NormalizeMatchText(item.ItemName), StringComparison.Ordinal);
        }

        private static bool MatchesMediaItem(ArchiveDetailHighlightContext highlight, ArchiveDetailMediaItem item)
        {
            if (highlight.MediaItemId > 0 && item.MediaItemId == highlight.MediaItemId)
            {
                return true;
            }

            if (highlight.MediaItemId > 0)
            {
                return false;
            }

            return string.Equals(NormalizeMatchText(highlight.ItemType), NormalizeMatchText(item.ItemType), StringComparison.Ordinal)
                && string.Equals(NormalizeMatchText(highlight.ItemName), NormalizeMatchText(item.ContentDesc), StringComparison.Ordinal);
        }

        private static bool MatchesContainerCode(string highlightContainerCode, string containerCode)
        {
            string normalizedHighlight = NormalizeMatchText(highlightContainerCode);
            if (normalizedHighlight == "-" || string.IsNullOrEmpty(normalizedHighlight))
            {
                return false;
            }

            return string.Equals(normalizedHighlight, NormalizeMatchText(containerCode), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeMatchText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        private void ClearSearchHighlights()
        {
            ClearMediaHighlights(ElectronicMediaEntries);
            ClearMediaHighlights(SimulatedMediaEntries);
            ClearMediaHighlights(ProofMediaEntries);

            foreach (var box in ArchiveBoxResults)
            {
                box.IsSearchHighlighted = false;
                ClearItemHighlights(box.Items);
            }

            foreach (var unit in ElectronicUnitResults)
            {
                unit.IsSearchHighlighted = false;
                foreach (var item in unit.Items)
                {
                    item.IsSearchHighlighted = false;
                }
            }
        }

        private static void ClearMediaHighlights(IEnumerable<ArchiveDetailMediaEntryItem> mediaEntries)
        {
            foreach (var mediaEntry in mediaEntries)
            {
                mediaEntry.IsSearchHighlighted = false;
                ClearItemHighlights(mediaEntry.Items);
            }
        }

        private static void ClearItemHighlights(IEnumerable<ArchiveDetailMediaItem> items)
        {
            foreach (var item in items)
            {
                item.IsSearchHighlighted = false;
                foreach (var entry in item.ContentEntries)
                {
                    entry.IsSearchHighlighted = false;
                }
            }
        }

        private void SetAllElectronicMediaItemsExpanded(bool expanded)
        {
            foreach (var mediaEntry in ElectronicMediaEntries)
            {
                foreach (var item in mediaEntry.Items)
                {
                    item.IsDetailsExpanded = expanded;
                }
            }
        }

        private static ArchiveDetailMediaEntryItem MapMediaEntry(YearlyArchiveRegisterMedia media)
        {
            var items = media.Items
                .OrderBy(item => item.ItemType)
                .ThenBy(item => item.ContentDesc)
                .Select(MapMediaItem)
                .ToList();

            return new ArchiveDetailMediaEntryItem(
                media.Id,
                NormalizeText(media.MediaKind),
                NormalizeText(media.MediaType),
                FormatCount(media.MediaCount),
                NormalizeText(ElectronicMediaItemSupport.BuildStoragePathSummary(media)),
                NormalizeText(media.Disposition),
                items);
        }

        private static ArchiveDetailMediaItem MapMediaItem(YearlyArchiveRegisterMediaItem item)
        {
            var detail = item.ElectronicDetail;
            var contentEntries = MapContentEntries(detail);

            return new ArchiveDetailMediaItem(
                item.Id,
                NormalizeText(item.ItemType),
                NormalizeText(item.ContentDesc),
                FormatCount(item.ContentCount),
                NormalizeText(item.StoragePath),
                NormalizeConfidentialLevel(item.ConfidentialLevel),
                NormalizeText(item.MediaEntry?.MediaType),
                NormalizeText(SimulatedMediaItemClassificationSupport.ResolveMaterialCategory(item)),
                NormalizeText(SimulatedMediaItemClassificationSupport.ResolveSubCategory(item)),
                NormalizeText(SimulatedMediaItemClassificationSupport.ResolveOrganizationFormDisplay(item)),
                detail == null ? string.Empty : $"{detail.DataSizeMb:0.##} MB",
                NormalizeText(item.Note),
                contentEntries);
        }

        private static ArchiveDetailElectronicContentEntryItem MapContentEntry(
            YearlyArchiveRegisterElectronicMediaItemEntry entry)
        {
            return new ArchiveDetailElectronicContentEntryItem(
                entry.Id,
                NormalizeText(entry.EntryKind),
                NormalizeText(entry.EntryName),
                NormalizeText(entry.RelativePath),
                FormatEntryDate(entry.CreatedAt),
                FormatEntryDate(entry.ModifiedAt),
                FormatEntrySize(entry.SizeMb));
        }

        private static IEnumerable<ArchiveDetailElectronicContentEntryItem> MapContentEntries(
            YearlyArchiveRegisterElectronicMediaItemDetail? detail)
        {
            if (detail?.Entries == null || detail.Entries.Count == 0)
            {
                return Array.Empty<ArchiveDetailElectronicContentEntryItem>();
            }

            return detail.Entries
                .OrderBy(entry => entry.SortOrder)
                .ThenBy(entry => entry.EntryName)
                .Select(MapContentEntry)
                .ToList();
        }

        private static string NormalizeConfidentialLevel(string? value)
        {
            return NormalizeText(ArchiveRegisterDomainValues.NormalizeConfidentialLevel(value));
        }

        private static ArchiveDetailArchiveBoxResult MapArchiveBoxResult(YearlyArchiveBox box)
        {
            var items = box.MediaItemLinks
                .Where(link => link.MediaItem != null)
                .GroupBy(link => link.MediaItem.Id)
                .Select(group => MapMediaItem(group.First().MediaItem))
                .OrderBy(item => item.ItemType)
                .ThenBy(item => item.ContentDesc)
                .ToList();

            return new ArchiveDetailArchiveBoxResult(
                NormalizeText(box.ArchiveSequenceNo),
                NormalizeText(box.BoxLocationCode),
                NormalizeText(box.Specs),
                NormalizeText(box.PlacementMode),
                NormalizeText(box.ArchivedBy),
                FormatDate(box.ArchivedDate),
                NormalizeText(box.Remarks),
                items);
        }

        private static ArchiveDetailElectronicUnitResult MapElectronicUnitResult(YearlyElectronicArchiveUnit unit)
        {
            var items = unit.MediaItemLinks
                .OrderBy(link => link.FormNo)
                .ThenBy(link => link.MaterialName)
                .ThenBy(link => link.ItemName)
                .Select(link => MapElectronicUnitFilingItem(link, unit))
                .ToList();

            return new ArchiveDetailElectronicUnitResult(
                NormalizeText(unit.ElectronicArchiveNo),
                NormalizeText(unit.StorageLocation),
                NormalizeText(unit.StorageCarrierType),
                NormalizeText(unit.LinkedMediumCodes),
                NormalizeText(unit.Disposition),
                FormatCount(unit.MediaCount),
                NormalizeText(unit.ContentSummary),
                NormalizeText(unit.ArchivedBy),
                FormatDate(unit.ArchivedDate),
                NormalizeText(unit.Remarks),
                items);
        }

        private static ArchiveDetailElectronicUnitFilingItem MapElectronicUnitFilingItem(
            YearlyElectronicArchiveUnitMediaItemLink link,
            YearlyElectronicArchiveUnit unit)
        {
            var mediaItem = link.MediaItem;
            string yearText = NormalizeText(unit.Year);
            string projectName = NormalizeText(unit.ProjectName);
            string materialName = NormalizeText(link.MaterialName);
            string itemName = !string.IsNullOrWhiteSpace(link.ItemName)
                ? link.ItemName.Trim()
                : mediaItem != null
                    ? NormalizeText(mediaItem.ContentDesc)
                    : "-";
            string itemType = mediaItem != null
                ? NormalizeText(mediaItem.ItemType)
                : "-";
            string dataSizeText = link.DataSizeMb > 0
                ? $"{link.DataSizeMb:0.##} MB"
                : mediaItem?.ElectronicDetail != null
                    ? $"{mediaItem.ElectronicDetail.DataSizeMb:0.##} MB"
                    : "-";
            string filingStoragePath = NormalizeText(link.FilingStoragePath);
            string confidentialLevel = mediaItem != null
                ? NormalizeConfidentialLevel(mediaItem.ConfidentialLevel)
                : "-";
            var electronicDetail = mediaItem?.ElectronicDetail;

            return new ArchiveDetailElectronicUnitFilingItem(
                link.YearlyArchiveRegisterMediaItemId,
                NormalizeText(link.FormNo),
                yearText,
                projectName,
                materialName,
                itemName,
                itemType,
                confidentialLevel,
                NormalizeText(electronicDetail?.MaterialCategory),
                NormalizeText(electronicDetail?.SubCategory),
                NormalizeText(electronicDetail?.DataOrganizationForm),
                NormalizeText(mediaItem?.MediaEntry?.MediaType),
                dataSizeText,
                filingStoragePath,
                MapContentEntries(electronicDetail));
        }

        private async Task ApplyFilingFactBindingAsync(YearlyArchiveRegisterRecord record)
        {
            var mediaItemIds = record.MediaEntries
                .SelectMany(media => media.Items)
                .Select(item => item.Id)
                .Concat(record.ElectronicArchiveUnits
                    .SelectMany(unit => unit.MediaItemLinks)
                    .Select(link => link.YearlyArchiveRegisterMediaItemId))
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (mediaItemIds.Count == 0)
            {
                return;
            }

            var facts = await _filingFactRepository.GetFactsByMediaItemIdsAsync(mediaItemIds);
            var filingFactIdByMediaItemId = BuildPreferredFilingFactIndex(
                facts,
                _searchHighlight?.ContainerCode);

            if (_primaryFilingFactId is int primaryFactId
                && _searchHighlight?.MediaItemId is int highlightedMediaItemId
                && highlightedMediaItemId > 0)
            {
                filingFactIdByMediaItemId[highlightedMediaItemId] = primaryFactId;
            }

            foreach (var mediaEntry in ElectronicMediaEntries)
            {
                foreach (var item in mediaEntry.Items)
                {
                    item.FilingFactId = filingFactIdByMediaItemId.GetValueOrDefault(item.MediaItemId);
                }
            }

            foreach (var unit in ElectronicUnitResults)
            {
                foreach (var item in unit.Items)
                {
                    item.FilingFactId = filingFactIdByMediaItemId.GetValueOrDefault(item.MediaItemId);
                }
            }
        }

        private static Dictionary<int, int> BuildPreferredFilingFactIndex(
            IReadOnlyList<YearlyArchiveFilingFact> facts,
            string? preferredContainerCode)
        {
            var result = new Dictionary<int, int>();
            foreach (var group in facts.GroupBy(fact => fact.MediaItemId))
            {
                var match = group.FirstOrDefault(fact => MatchesContainerCode(preferredContainerCode, fact.ContainerCode))
                    ?? group.FirstOrDefault(fact => fact.PrimaryFilingFactId == null)
                    ?? group.First();
                result[group.Key] = match.Id;
            }

            return result;
        }

        private async Task AddSelectedToFilterPoolAsync()
        {
            if (string.IsNullOrWhiteSpace(_filterPoolMediaKind))
            {
                return;
            }

            var selections = new List<ArchiveSearchPoolSelection>();
            var hitsByFactId = new Dictionary<int, FiledArchiveSearchHit>();
            var entriesById = new Dictionary<int, MatchedContentEntryInfo>();

            foreach (var unit in ElectronicUnitResults)
            {
                foreach (var item in unit.Items)
                {
                    CollectFilterSelections(item, selections, hitsByFactId, entriesById);
                }
            }

            if (selections.Count == 0)
            {
                _dialogService.ShowMessage("请先勾选资料子项或目录/文件。", "提示");
                return;
            }

            foreach (int filingFactId in selections.Select(selection => selection.FilingFactId).Distinct())
            {
                if (hitsByFactId.ContainsKey(filingFactId))
                {
                    continue;
                }

                var hit = await _searchService.GetSearchHitByFilingFactIdAsync(filingFactId);
                if (hit != null)
                {
                    hitsByFactId[filingFactId] = hit;
                }
            }

            var mergeResult = _poolSession.Merge(
                _filterPoolMediaKind,
                selections,
                hitsByFactId,
                entriesById);

            ClearFilterSelections();
            NotifyFilterSelectionStateChanged();
            ShowFilterPoolMergeMessage(mergeResult, selections.Count);
        }

        private void CollectFilterSelections(
            ArchiveDetailElectronicUnitFilingItem item,
            ICollection<ArchiveSearchPoolSelection> selections,
            IDictionary<int, FiledArchiveSearchHit> hitsByFactId,
            IDictionary<int, MatchedContentEntryInfo> entriesById)
        {
            if (item.FilingFactId <= 0)
            {
                return;
            }

            if (item.IsFilterSelected)
            {
                selections.Add(ArchiveSearchPoolSupport.CreateWholeMediaItem(item.FilingFactId));
                return;
            }

            foreach (var entry in item.ContentEntries.Where(entry => entry.IsFilterSelected))
            {
                selections.Add(ArchiveSearchPoolSupport.CreateContentEntry(item.FilingFactId, entry.EntryId));
                entriesById[entry.EntryId] = new MatchedContentEntryInfo
                {
                    EntryId = entry.EntryId,
                    EntryKind = entry.EntryKind,
                    EntryName = entry.EntryName,
                    RelativePath = entry.RelativePath
                };
            }
        }

        private static bool CollectFilterSelectionFromUnitItem(ArchiveDetailElectronicUnitFilingItem item) =>
            item.CanFilterSelect
            && (item.IsFilterSelected || item.ContentEntries.Any(entry => entry.IsFilterSelected));

        private void ClearFilterSelections()
        {
            foreach (var unit in ElectronicUnitResults)
            {
                foreach (var item in unit.Items)
                {
                    item.IsFilterSelected = false;
                    foreach (var entry in item.ContentEntries)
                    {
                        entry.IsFilterSelected = false;
                    }
                }
            }
        }

        private void NotifyFilterSelectionStateChanged()
        {
            OnPropertyChanged(nameof(FilterPoolSummary));
            OnPropertyChanged(nameof(HasFilterPoolSelections));
        }

        private void ShowFilterPoolMergeMessage(ArchiveSearchPoolSupport.MergeResult mergeResult, int incomingCount)
        {
            if (mergeResult.AddedCount > 0)
            {
                _dialogService.ShowMessage(
                    $"已加入筛选池 {mergeResult.AddedCount} 条。当前共 {_poolSession.GetPool(_filterPoolMediaKind!).Count} 条。",
                    "加入完成");
                return;
            }

            if (mergeResult.SkippedDuplicateCount > 0 && incomingCount == mergeResult.SkippedDuplicateCount)
            {
                _dialogService.ShowMessage("所选条目已在筛选池中。", "提示");
                return;
            }

            if (mergeResult.SkippedWholeExistsCount > 0)
            {
                _dialogService.ShowMessage("对应资料子项已以「整子项」在筛选池中，无法再单独加入目录/文件。", "提示");
            }
        }

        private async Task ViewAttachmentAsync(SystemAttachment? attachment)
        {
            if (attachment == null)
            {
                return;
            }

            try
            {
                var result = await _archiveRegisterService.PrepareAttachmentViewFlowAsync(attachment);
                if (!result.Success || result.Attachment?.FileContent == null)
                {
                    _dialogService.ShowMessage(result.Message);
                    return;
                }

                var fullAttachment = result.Attachment;
                if (_dialogService.ShowConfirm("直接打开？\n【确定】打开 【取消】另存为"))
                {
                    var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{fullAttachment.FileName}");
                    await File.WriteAllBytesAsync(path, fullAttachment.FileContent);
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    FileName = fullAttachment.FileName
                };

                if (dialog.ShowDialog() == true)
                {
                    await File.WriteAllBytesAsync(dialog.FileName, fullAttachment.FileContent);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"查看附件失败：{ex.Message}");
            }
        }

        private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
        {
            target.Clear();
            foreach (var item in items)
            {
                target.Add(item);
            }
        }

        private static bool IsElectronicMedia(YearlyArchiveRegisterMedia media)
        {
            return string.Equals(media.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal);
        }

        private static bool IsSimulatedMedia(YearlyArchiveRegisterMedia media)
        {
            return string.Equals(media.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal);
        }

        private static bool IsProofMedia(YearlyArchiveRegisterMedia media)
        {
            return media.Items.Any(item => string.Equals(item.ItemType, ArchiveRegisterDomainValues.ItemTypeProof, StringComparison.Ordinal));
        }

        private static string NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        private static string FormatCount(int count)
        {
            return count > 0 ? count.ToString() : "-";
        }

        private static string FormatDate(DateTime value)
        {
            return value == default ? "-" : value.ToString("yyyy-MM-dd HH:mm");
        }

        private static string FormatEntryDate(DateTime? value)
        {
            return value.HasValue && value.Value != default
                ? value.Value.ToString("yyyy-MM-dd HH:mm")
                : "-";
        }

        private static string FormatEntrySize(decimal? sizeMb)
        {
            return sizeMb.HasValue && sizeMb.Value > 0
                ? $"{sizeMb.Value:0.##} MB"
                : "-";
        }
    }
}
