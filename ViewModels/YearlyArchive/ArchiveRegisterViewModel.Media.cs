using Microsoft.Win32;
using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace DocMgr.ViewModels.YearlyArchive
{
    public partial class ArchiveRegisterViewModel
    {
        private readonly List<string> _allElectronicDispositionOptions = new();
        private readonly List<string> _electronicDocumentSubCategoryOptions = new();
        private readonly List<string> _electronicDataSubCategoryOptions = new();

        private bool _isSyncingElectronicMediaSettings;

        /// <summary>
        /// 防止 RefreshUserBorrowedHardDiskCodesAsync 与绑定/介质同步互相触发造成栈溢出。
        /// </summary>
        private bool _borrowedHardDiskCodesRefreshInProgress;

        private string _selectedElectronicMediaType = string.Empty;
        public string SelectedElectronicMediaType
        {
            get => _selectedElectronicMediaType;
            set
            {
                if (SetProperty(ref _selectedElectronicMediaType, value))
                {
                    RefreshElectronicDispositionOptions();
                    ApplySelectedElectronicMediaSettingsToEntries();
                }
            }
        }

        private string _selectedElectronicDisposition = string.Empty;
        public string SelectedElectronicDisposition
        {
            get => _selectedElectronicDisposition;
            set
            {
                if (SetProperty(ref _selectedElectronicDisposition, value))
                {
                    ApplySelectedElectronicMediaSettingsToEntries();
                }
            }
        }

        public int DataElectronicMediaCount => MediaEntries.Count(m => IsDataElectronic(m));

        private void InitializeMediaViews()
        {
            DataElectronicMediaView = new ListCollectionView(MediaEntries) { Filter = m => IsDataElectronic(m as MediaEntryViewModel) };
            DataSimulatedMediaView = new ListCollectionView(MediaEntries) { Filter = m => IsDataSimulated(m as MediaEntryViewModel) };
            ProofSimulatedMediaView = new ListCollectionView(MediaEntries) { Filter = m => IsProofMedia(m as MediaEntryViewModel) };
            MediaEntries.CollectionChanged += MediaEntries_CollectionChanged;
        }

        private void MediaEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null) foreach (MediaEntryViewModel m in e.NewItems) AttachMediaEntry(m);
            if (e.OldItems != null) foreach (MediaEntryViewModel m in e.OldItems) DetachMediaEntry(m);
            // ListCollectionView.Refresh() 在 ObservableCollection 仍处理 Change 回调时调用会触发 InvalidOperationException；
            // 硬盘+留存等路径会同步刷新视图并联动借出列表，更易复现，故延后到当前变更结束后再执行。
            ScheduleAfterMediaEntriesChanged();
        }

        private void ScheduleAfterMediaEntriesChanged()
        {
            void Work()
            {
                RefreshMediaViews();
                SyncElectronicMediaSettingsFromEntries();
                OnPropertyChanged(nameof(IsBorrowedHardDiskRegistrationVisible));
                OnPropertyChanged(nameof(IsBorrowedHardDiskCodeRequired));
            }

            if (Application.Current?.Dispatcher is { } dispatcher
                && !dispatcher.HasShutdownStarted
                && !dispatcher.HasShutdownFinished)
            {
                dispatcher.BeginInvoke(Work, DispatcherPriority.Background);
            }
            else
            {
                Work();
            }
        }

        private void ScheduleRefreshMediaViews()
        {
            if (Application.Current?.Dispatcher is { } dispatcher
                && !dispatcher.HasShutdownStarted
                && !dispatcher.HasShutdownFinished)
            {
                dispatcher.BeginInvoke(RefreshMediaViews, DispatcherPriority.Background);
            }
            else
            {
                RefreshMediaViews();
            }
        }

        private void AttachMediaEntry(MediaEntryViewModel media)
        {
            media.PropertyChanged += MediaEntry_PropertyChanged;
            media.Items.CollectionChanged += MediaItems_CollectionChanged;
            foreach (var item in media.Items)
            {
                ConfigureElectronicMediaItem(item);
                item.PropertyChanged += MediaItem_PropertyChanged;
            }

            RecalculateQuantities(media);
        }
        private void DetachMediaEntry(MediaEntryViewModel media)
        {
            media.PropertyChanged -= MediaEntry_PropertyChanged;
            media.Items.CollectionChanged -= MediaItems_CollectionChanged;
            foreach (var item in media.Items)
            {
                item.PropertyChanged -= MediaItem_PropertyChanged;
                DetachContentEntryQuantityHandler(item);
            }
        }
        private void MediaEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MediaEntryViewModel.MediaKind)
                || e.PropertyName == nameof(MediaEntryViewModel.MediaType)
                || e.PropertyName == nameof(MediaEntryViewModel.Disposition)
                || e.PropertyName == nameof(MediaEntryViewModel.MediaCount)
                || e.PropertyName == nameof(MediaEntryViewModel.IsBorrowedHardDisk)
                || e.PropertyName == nameof(MediaEntryViewModel.BorrowedHardDiskCode))
            {
                RefreshMediaViews();
                SyncElectronicMediaSettingsFromEntries();
            }
        }
        private void MediaItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (MediaItemViewModel item in e.NewItems)
                {
                    ConfigureElectronicMediaItem(item);
                    item.PropertyChanged += MediaItem_PropertyChanged;
                }
            }

            if (e.OldItems != null) foreach (MediaItemViewModel item in e.OldItems) item.PropertyChanged -= MediaItem_PropertyChanged;

            var media = FindMediaEntryByItemsCollection(sender);
            if (media != null)
            {
                RecalculateQuantities(media);
            }

            ScheduleRefreshMediaViews();
        }
        private void MediaItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MediaItemViewModel.ItemType))
                ScheduleRefreshMediaViews();
        }
        private void RefreshMediaViews()
        {
            DataElectronicMediaView.Refresh();
            DataSimulatedMediaView.Refresh();
            ProofSimulatedMediaView.Refresh();
            OnPropertyChanged(nameof(DataElectronicMediaCount));
        }

        private MediaEntryViewModel? FindMediaEntryByItemsCollection(object? sender)
        {
            return MediaEntries.FirstOrDefault(m => ReferenceEquals(m.Items, sender));
        }

        private void RecalculateQuantities(MediaEntryViewModel? media = null)
        {
            IEnumerable<MediaEntryViewModel> entries = media == null
                ? MediaEntries
                : new MediaEntryViewModel[] { media };

            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                if (IsDataElectronic(entry))
                {
                    entry.SetAutoMediaCount(1);
                    foreach (var item in entry.Items)
                    {
                        RecalculateElectronicItemContentCount(item);
                    }
                }
                else
                {
                    entry.SetAutoMediaCount(entry.Items.Count);
                    if (!IsDataSimulated(entry))
                    {
                        foreach (var item in entry.Items)
                        {
                            item.SetAutoContentCount(1);
                        }
                    }
                }
            }
        }

        private static void RecalculateElectronicItemContentCount(MediaItemViewModel item)
        {
            item.SetAutoContentCount(1);
        }

        private void RecalculateAllQuantities()
        {
            RecalculateQuantities();
        }

        // Predicates
        private static bool IsProofMedia(MediaEntryViewModel? m) => m?.Items.Any(i => string.Equals(i.ItemType, ArchiveRegisterDomainValues.ItemTypeProof, StringComparison.Ordinal)) == true;
        private static bool IsDataElectronic(MediaEntryViewModel? m) => m != null && string.Equals(m.MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal) && !IsProofMedia(m);
        private static bool IsDataSimulated(MediaEntryViewModel? m) => m != null && string.Equals(m.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal) && !IsProofMedia(m);

        // Actions
        private void AddDataElectronicMediaEntry()
        {
            EnsureElectronicMediaSelections();
            AddMediaEntry(ArchiveRegisterDomainValues.MediaKindElectronic, ArchiveRegisterDomainValues.ItemTypeData);
        }
        private void AddDataSimulatedMediaEntry() => AddMediaEntry(ArchiveRegisterDomainValues.MediaKindSimulated, ArchiveRegisterDomainValues.ItemTypeData);
        private void AddProofSimulatedMediaEntry() => AddMediaEntry(ArchiveRegisterDomainValues.MediaKindSimulated, ArchiveRegisterDomainValues.ItemTypeProof);
        private void AddMediaEntry(string kind, string itemType)
        {
            bool isElectronic = string.Equals(kind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal);
            var entry = new MediaEntryViewModel
            {
                MediaKind = kind,
                MediaType = isElectronic ? ResolveSelectedElectronicMediaType() : ResolveDefaultMediaType(kind, itemType),
                Disposition = isElectronic ? ResolveSelectedElectronicDisposition() : ResolveDefaultDisposition(kind),
                IsBorrowedHardDisk = false,
                BorrowedHardDiskCode = string.Empty
            };
            entry.Items.Add(CreateDefaultElectronicMediaItem(itemType));
            MediaEntries.Add(entry);
            RecalculateQuantities(entry);

            if (isElectronic)
            {
                ApplySelectedElectronicMediaSettingsToEntries();
            }
        }
        private void RemoveMediaEntry(MediaEntryViewModel? m) { if (m != null) MediaEntries.Remove(m); }
        private void AddMediaItem(MediaEntryViewModel? m)
        {
            if (m == null)
            {
                return;
            }

            m.Items.Add(IsDataElectronic(m)
                ? CreateDefaultElectronicMediaItem(ArchiveRegisterDomainValues.ItemTypeData)
                : new MediaItemViewModel
                {
                    ItemType = IsProofMedia(m) ? ArchiveRegisterDomainValues.ItemTypeProof : ArchiveRegisterDomainValues.ItemTypeData,
                    ConfidentialLevel = ConfidentialLevelOptions.FirstOrDefault() ?? ArchiveRegisterDomainValues.ConfidentialLevelNone
                });
        }
        private void RemoveMediaItem(MediaItemViewModel? i) { if (i == null) return; var p = MediaEntries.FirstOrDefault(m => m.Items.Contains(i)); p?.Items.Remove(i); }

        private void SyncCollectionsFromRecord()
        {
            MediaEntries.Clear();
            if (CurrentRecord?.MediaEntries != null)
                foreach (var m in CurrentRecord.MediaEntries) MediaEntries.Add(CreateMediaEntryViewModel(m));

            SyncElectronicMediaSettingsFromEntries();
            RecalculateAllQuantities();
            EnsureUserBorrowedHardDiskListIncludesSelected();
        }

        private void EnsureUserBorrowedHardDiskListIncludesSelected()
        {
            if (!IsBorrowedHardDiskRegistrationVisible)
            {
                return;
            }

            foreach (var m in MediaEntries.Where(IsDataElectronic))
            {
                if (!m.IsRetainedHardDiskScenario || !m.IsBorrowedHardDisk)
                {
                    continue;
                }

                string code = m.BorrowedHardDiskCode?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(code))
                {
                    continue;
                }

                if (!UserBorrowedHardDiskCodes.Any(x => string.Equals(x, code, StringComparison.OrdinalIgnoreCase)))
                {
                    UserBorrowedHardDiskCodes.Insert(0, code);
                }
            }
        }

        private MediaEntryViewModel CreateMediaEntryViewModel(YearlyArchiveRegisterMedia media)
        {
            var vm = new MediaEntryViewModel
            {
                MediaKind = media.MediaKind,
                MediaType = media.MediaType,
                Disposition = media.Disposition,
                IsBorrowedHardDisk = media.IsBorrowedHardDisk,
                BorrowedHardDiskCode = media.BorrowedHardDiskCode
            };

            if (media.Items != null)
            {
                foreach (var item in media.Items)
                {
                    var itemVm = new MediaItemViewModel
                    {
                        ItemType = item.ItemType,
                        ContentDesc = item.ContentDesc,
                        ContentCount = item.ContentCount > 0 ? item.ContentCount : 1,
                        StoragePath = ElectronicMediaItemSupport.FormatStoragePathForRegistration(item.StoragePath),
                        Note = item.Note,
                        ConfidentialLevel = ResolveConfidentialLevelFromRecord(item.ConfidentialLevel)
                    };

                    if (item.ElectronicDetail != null)
                    {
                        itemVm.MaterialCategory = item.ElectronicDetail.MaterialCategory;
                        itemVm.SubCategory = item.ElectronicDetail.SubCategory;
                        itemVm.DataOrganizationForm = item.ElectronicDetail.DataOrganizationForm;
                        itemVm.DataSizeMb = item.ElectronicDetail.DataSizeMb;

                        foreach (var entry in item.ElectronicDetail.Entries.OrderBy(e => e.SortOrder))
                        {
                            itemVm.ContentEntries.Add(new ElectronicMediaItemEntryViewModel
                            {
                                EntryKind = entry.EntryKind,
                                EntryName = entry.EntryName,
                                RelativePath = entry.RelativePath,
                                SizeMb = entry.SizeMb,
                                CreatedAt = entry.CreatedAt,
                                ModifiedAt = entry.ModifiedAt
                            });
                        }
                    }

                    ConfigureElectronicMediaItem(itemVm);
                    itemVm.RefreshContentScanSummary();
                    vm.Items.Add(itemVm);
                }
            }

            RecalculateQuantities(vm);
            return vm;
        }

        private List<YearlyArchiveRegisterMedia> BuildMediaEntries()
        {
            EnsureElectronicMediaSelections();
            RecalculateAllQuantities();

            return MediaEntries.Select(m => new YearlyArchiveRegisterMedia
            {
                MediaKind = m.MediaKind,
                MediaType = IsDataElectronic(m) ? ResolveSelectedElectronicMediaType() : m.MediaType,
                MediaCount = IsDataElectronic(m) ? 1 : m.MediaCount,
                Disposition = IsDataElectronic(m)
                    ? ResolveSelectedElectronicDisposition()
                    : ArchiveRegisterDomainValues.ElectronicDispositionRetain,
                IsBorrowedHardDisk = IsDataElectronic(m) && m.IsRetainedHardDiskScenario && m.IsBorrowedHardDisk,
                BorrowedHardDiskCode = IsDataElectronic(m) && m.IsRetainedHardDiskScenario && m.IsBorrowedHardDisk
                    ? (m.BorrowedHardDiskCode?.Trim() ?? string.Empty)
                    : string.Empty,
                Items = m.Items.Select((item, index) => MapMediaItemEntity(item, IsDataElectronic(m), index)).ToList()
            }).ToList();
        }

        private static YearlyArchiveRegisterMediaItem MapMediaItemEntity(MediaItemViewModel item, bool isElectronic, int index)
        {
            var entity = new YearlyArchiveRegisterMediaItem
            {
                ItemType = item.ItemType,
                ContentDesc = item.ContentDesc,
                ContentCount = isElectronic ? 1 : item.ContentCount,
                StoragePath = ElectronicMediaItemSupport.FormatStoragePathForRegistration(item.StoragePath),
                Note = item.Note,
                ConfidentialLevel = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(item.ConfidentialLevel)
            };

            if (!isElectronic)
            {
                return entity;
            }

            entity.ElectronicDetail = new YearlyArchiveRegisterElectronicMediaItemDetail
            {
                MaterialCategory = item.MaterialCategory?.Trim() ?? string.Empty,
                SubCategory = item.SubCategory?.Trim() ?? string.Empty,
                DataOrganizationForm = item.DataOrganizationForm?.Trim() ?? string.Empty,
                DataSizeMb = item.DataSizeMb,
                Entries = item.ContentEntries
                    .Select((entry, entryIndex) => new YearlyArchiveRegisterElectronicMediaItemEntry
                    {
                        EntryKind = string.IsNullOrWhiteSpace(entry.EntryKind)
                            ? ElectronicMediaItemSupport.ResolveEntryKind(item.DataOrganizationForm)
                            : entry.EntryKind,
                        EntryName = entry.EntryName?.Trim() ?? string.Empty,
                        RelativePath = entry.RelativePath?.Trim() ?? string.Empty,
                        SizeMb = entry.SizeMb,
                        CreatedAt = entry.CreatedAt,
                        ModifiedAt = entry.ModifiedAt,
                        SortOrder = (entryIndex + 1) * 10
                    })
                    .ToList()
            };

            return entity;
        }

        private string ResolveDefaultMediaType(string kind, string itemType)
        {
            if (string.Equals(itemType, ArchiveRegisterDomainValues.ItemTypeProof, StringComparison.Ordinal))
            {
                return ProofSimulatedMediaTypeOptions.FirstOrDefault() ?? string.Empty;
            }

            return string.Equals(kind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
                ? (DataElectronicMediaTypeOptions.FirstOrDefault() ?? string.Empty)
                : (DataSimulatedMediaTypeOptions.FirstOrDefault() ?? string.Empty);
        }

        private string ResolveDefaultDisposition(string kind)
        {
            return string.Equals(kind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
                ? (DataElectronicDispositionOptions.FirstOrDefault() ?? string.Empty)
                : ArchiveRegisterDomainValues.SimulatedDispositionRetain;
        }

        private async Task LoadDomainOptionCollectionsAsync()
        {
            var domainOptions = await _archiveRegisterService.GetPageDomainOptionsAsync();

            ApplyOptions(SourceTypeOptions, domainOptions.SourceTypes);
            ApplyOptions(ArchivePurposeOptions, domainOptions.ArchivePurposes);
            ApplyOptions(SimulatedMediaKindOptions, domainOptions.SimulatedMediaKinds);
            ApplyOptions(DataItemTypeOptions, domainOptions.DataItemTypes);
            ApplyOptions(ProofItemTypeOptions, domainOptions.ProofItemTypes);
            ApplyOptions(DataElectronicMediaTypeOptions, domainOptions.DataElectronicMediaTypes);
            ApplyOptions(DataSimulatedMediaTypeOptions, domainOptions.DataSimulatedMediaTypes);
            ApplyOptions(ProofSimulatedMediaTypeOptions, domainOptions.ProofSimulatedMediaTypes);
            ApplyOptions(DataElectronicDispositionOptions, domainOptions.DataElectronicDispositions);
            _allElectronicDispositionOptions.Clear();
            _allElectronicDispositionOptions.AddRange(domainOptions.DataElectronicDispositions);
            ApplyOptions(ElectronicMaterialCategoryOptions, domainOptions.ElectronicMaterialCategories);
            ApplyOptions(ElectronicDataOrganizationFormOptions, domainOptions.ElectronicDataOrganizationForms);
            _electronicDocumentSubCategoryOptions.Clear();
            _electronicDocumentSubCategoryOptions.AddRange(domainOptions.ElectronicDocumentSubCategories);
            _electronicDataSubCategoryOptions.Clear();
            _electronicDataSubCategoryOptions.AddRange(domainOptions.ElectronicDataSubCategories);
            ApplyOptions(DataSimulatedDispositionOptions, domainOptions.DataSimulatedDispositions);
            ApplyOptions(ConfidentialLevelOptions, domainOptions.ConfidentialLevels);
            ApplyOptions(ProdOpinionOptions, domainOptions.ProdOpinionOptions);
            ApplyOptions(RndOpinionOptions, domainOptions.RndOpinionOptions);
            ApplyOptions(DeputyOpinionOptions, domainOptions.DeputyOpinionOptions);

            RefreshElectronicDispositionOptions();
            EnsureElectronicMediaSelections();
            foreach (var item in MediaEntries.SelectMany(entry => entry.Items))
            {
                RefreshElectronicSubCategoryOptions(item);
            }
            SyncElectronicMediaSettingsFromEntries();
            ApplySourceTypeSelection();
            ApplyArchivePurposeSelection();
        }

        private void EnsureElectronicMediaSelections()
        {
            _isSyncingElectronicMediaSettings = true;
            try
            {
                if (string.IsNullOrWhiteSpace(SelectedElectronicMediaType))
                {
                    SelectedElectronicMediaType = DataElectronicMediaTypeOptions.FirstOrDefault() ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(SelectedElectronicDisposition))
                {
                    SelectedElectronicDisposition = GetAllowedElectronicDispositionOptions().FirstOrDefault() ?? string.Empty;
                }
            }
            finally
            {
                _isSyncingElectronicMediaSettings = false;
            }

            SyncBorrowedHardDiskSettingsFromSelections();
        }

        private void SyncElectronicMediaSettingsFromEntries()
        {
            var firstElectronicMedia = MediaEntries.FirstOrDefault(m => IsDataElectronic(m));

            _isSyncingElectronicMediaSettings = true;
            try
            {
                string rawMediaType = string.IsNullOrWhiteSpace(firstElectronicMedia?.MediaType)
                    ? string.Empty
                    : firstElectronicMedia.MediaType.Trim();
                SelectedElectronicMediaType = string.IsNullOrEmpty(rawMediaType)
                    ? (DataElectronicMediaTypeOptions.FirstOrDefault() ?? string.Empty)
                    : (DataElectronicMediaTypeOptions.FirstOrDefault(o => string.Equals(o, rawMediaType, StringComparison.OrdinalIgnoreCase))
                       ?? rawMediaType);

                RefreshElectronicDispositionOptions();

                string preferredDisposition = string.IsNullOrWhiteSpace(firstElectronicMedia?.Disposition)
                    ? string.Empty
                    : firstElectronicMedia.Disposition.Trim();
                var allowedDispositions = GetAllowedElectronicDispositionOptions();

                string? matchedDisposition = allowedDispositions
                    .FirstOrDefault(o => string.Equals(o, preferredDisposition, StringComparison.OrdinalIgnoreCase));
                SelectedElectronicDisposition = matchedDisposition
                    ?? (allowedDispositions.FirstOrDefault() ?? string.Empty);

                IsBorrowedHardDisk = firstElectronicMedia?.IsRetainedHardDiskScenario == true && firstElectronicMedia.IsBorrowedHardDisk;
                BorrowedHardDiskCode = firstElectronicMedia?.IsRetainedHardDiskScenario == true
                    ? firstElectronicMedia.BorrowedHardDiskCode ?? string.Empty
                    : string.Empty;
            }
            finally
            {
                _isSyncingElectronicMediaSettings = false;
            }

            SyncBorrowedHardDiskSettingsFromSelections();
        }

        private void ApplySelectedElectronicMediaSettingsToEntries()
        {
            if (_isSyncingElectronicMediaSettings)
            {
                return;
            }

            foreach (var media in MediaEntries.Where(m => IsDataElectronic(m)))
            {
                media.MediaKind = ArchiveRegisterDomainValues.MediaKindElectronic;
                media.MediaType = ResolveSelectedElectronicMediaType();
                media.SetAutoMediaCount(1);
                media.Disposition = ResolveSelectedElectronicDisposition();
            }

            OnPropertyChanged(nameof(DataElectronicMediaCount));
            OnPropertyChanged(nameof(IsBorrowedHardDiskRegistrationVisible));
            OnPropertyChanged(nameof(IsBorrowedHardDiskCodeRequired));
        }

        private string ResolveSelectedElectronicMediaType()
        {
            return string.IsNullOrWhiteSpace(SelectedElectronicMediaType)
                ? (DataElectronicMediaTypeOptions.FirstOrDefault() ?? string.Empty)
                : SelectedElectronicMediaType;
        }

        private string ResolveSelectedElectronicDisposition()
        {
            return string.IsNullOrWhiteSpace(SelectedElectronicDisposition)
                ? (GetAllowedElectronicDispositionOptions().FirstOrDefault() ?? string.Empty)
                : SelectedElectronicDisposition;
        }

        private void RefreshElectronicDispositionOptions()
        {
            var allowedOptions = GetAllowedElectronicDispositionOptions();
            ApplyOptions(DataElectronicDispositionOptions, allowedOptions);

            string current = SelectedElectronicDisposition?.Trim() ?? string.Empty;
            string? canon = allowedOptions.FirstOrDefault(o => string.Equals(o, current, StringComparison.OrdinalIgnoreCase));
            if (canon != null)
            {
                if (!string.Equals(SelectedElectronicDisposition, canon, StringComparison.Ordinal))
                {
                    SelectedElectronicDisposition = canon;
                }
            }
            else
            {
                SelectedElectronicDisposition = allowedOptions.FirstOrDefault() ?? string.Empty;
            }

            SyncBorrowedHardDiskSettingsFromSelections();
        }

        private IReadOnlyList<string> GetAllowedElectronicDispositionOptions()
        {
            return _archiveRegisterService.GetAllowedElectronicDispositions(
                ResolveSelectedElectronicMediaType(),
                _allElectronicDispositionOptions);
        }

        private void SyncBorrowedHardDiskSettingsFromSelections()
        {
            bool isVisible = string.Equals(SelectedElectronicMediaType?.Trim(), ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk, StringComparison.OrdinalIgnoreCase)
                && string.Equals(SelectedElectronicDisposition?.Trim(), ArchiveRegisterDomainValues.ElectronicDispositionRetain, StringComparison.OrdinalIgnoreCase);

            if (isVisible)
            {
                OnPropertyChanged(nameof(IsBorrowedHardDiskRegistrationVisible));
                OnPropertyChanged(nameof(IsBorrowedHardDiskCodeRequired));
                _ = RefreshUserBorrowedHardDiskCodesAsync();
                return;
            }

            if (_isBorrowedHardDisk)
            {
                _isBorrowedHardDisk = false;
                OnPropertyChanged(nameof(IsBorrowedHardDisk));
            }

            if (!string.IsNullOrWhiteSpace(_borrowedHardDiskCode))
            {
                _borrowedHardDiskCode = string.Empty;
                OnPropertyChanged(nameof(BorrowedHardDiskCode));
            }

            OnPropertyChanged(nameof(IsBorrowedHardDiskRegistrationVisible));
            OnPropertyChanged(nameof(IsBorrowedHardDiskCodeRequired));
        }

        private readonly Dictionary<MediaItemViewModel, NotifyCollectionChangedEventHandler> _contentEntryQuantityHandlers = new();

        private MediaItemViewModel CreateDefaultElectronicMediaItem(string itemType)
        {
            var item = new MediaItemViewModel
            {
                ItemType = itemType,
                MaterialCategory = ElectronicMaterialCategoryOptions.FirstOrDefault() ?? ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocument,
                DataOrganizationForm = ElectronicDataOrganizationFormOptions.FirstOrDefault() ?? ArchiveRegisterDomainValues.ElectronicDataOrganizationFormDirectory,
                ConfidentialLevel = ConfidentialLevelOptions.FirstOrDefault() ?? ArchiveRegisterDomainValues.ConfidentialLevelNone
            };

            ConfigureElectronicMediaItem(item);
            return item;
        }

        private void ConfigureElectronicMediaItem(MediaItemViewModel item)
        {
            item.SubCategoryOptionsRefreshHandler = RefreshElectronicSubCategoryOptions;
            RefreshElectronicSubCategoryOptions(item);
            AttachContentEntryQuantityHandler(item);
        }

        private void AttachContentEntryQuantityHandler(MediaItemViewModel item)
        {
            if (_contentEntryQuantityHandlers.ContainsKey(item))
            {
                return;
            }

            NotifyCollectionChangedEventHandler handler = (_, _) =>
            {
                RecalculateElectronicItemContentCount(item);
            };
            _contentEntryQuantityHandlers[item] = handler;
            item.ContentEntries.CollectionChanged += handler;
        }

        private void DetachContentEntryQuantityHandler(MediaItemViewModel item)
        {
            if (_contentEntryQuantityHandlers.TryGetValue(item, out var handler))
            {
                item.ContentEntries.CollectionChanged -= handler;
                _contentEntryQuantityHandlers.Remove(item);
            }
        }

        private void RefreshElectronicSubCategoryOptions(MediaItemViewModel item)
        {
            IReadOnlyList<string> options = string.Equals(item.MaterialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocument, StringComparison.Ordinal)
                ? _electronicDocumentSubCategoryOptions
                : string.Equals(item.MaterialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategoryData, StringComparison.Ordinal)
                    ? _electronicDataSubCategoryOptions
                    : Array.Empty<string>();

            item.AvailableSubCategories.Clear();
            foreach (var option in options)
            {
                item.AvailableSubCategories.Add(option);
            }

            if (!string.IsNullOrWhiteSpace(item.SubCategory)
                && !options.Any(option => string.Equals(option, item.SubCategory, StringComparison.OrdinalIgnoreCase)))
            {
                item.SubCategory = string.Empty;
            }
        }

        private static ElectronicMediaItemEntryViewModel CreateContentEntryViewModel(ElectronicMediaContentScanEntry entry)
        {
            return new ElectronicMediaItemEntryViewModel
            {
                EntryKind = entry.EntryKind,
                EntryName = entry.EntryName,
                RelativePath = entry.RelativePath,
                SizeMb = entry.SizeMb,
                CreatedAt = entry.CreatedAt,
                ModifiedAt = entry.ModifiedAt
            };
        }

        private void ApplyScanResult(
            MediaItemViewModel item,
            ElectronicMediaContentScanResult result,
            IReadOnlyList<string>? scannedFilePaths = null,
            IReadOnlyList<string>? scannedDirectoryPaths = null)
        {
            item.StorageRootFullPath = result.RootPath;
            item.StoragePath = ElectronicMediaItemSupport.FormatStoragePathForRegistration(result.RootPath);
            item.ContentEntries.Clear();
            foreach (var entry in result.Entries)
            {
                item.ContentEntries.Add(CreateContentEntryViewModel(entry));
            }

            item.LastScannedFilePaths.Clear();
            if (scannedFilePaths != null)
            {
                item.LastScannedFilePaths.AddRange(scannedFilePaths);
            }

            item.LastScannedDirectoryPaths.Clear();
            if (scannedDirectoryPaths != null)
            {
                item.LastScannedDirectoryPaths.AddRange(scannedDirectoryPaths);
            }

            item.DataSizeMb = result.TotalSizeMb;
            item.RefreshContentScanSummary(result.FileCount);
            RecalculateElectronicItemContentCount(item);
        }

        private static bool CanRescanElectronicContent(MediaItemViewModel item)
        {
            if (item.IsDirectoryOrganizationForm)
            {
                return item.LastScannedDirectoryPaths.Count > 0
                    || (item.HasScannedEntries && !string.IsNullOrWhiteSpace(item.StoragePath));
            }

            if (item.IsFileOrganizationForm)
            {
                return item.LastScannedFilePaths.Count > 0;
            }

            return false;
        }

        private async Task PickFolderAndScanElectronicContentAsync(MediaItemViewModel? item)
        {
            if (item == null || !item.IsDirectoryOrganizationForm)
            {
                return;
            }

            var folders = _dialogService.PickFolders("选择电子资料目录", multiselect: true);
            if (folders == null || folders.Count == 0)
            {
                return;
            }

            if (item.HasScannedEntries
                && !_dialogService.ShowConfirm("重新选择目录将覆盖当前已扫描的目录/文件明细，是否继续？"))
            {
                return;
            }

            await ScanDirectoriesContentAsync(item, folders);
        }

        private async Task PickFilesAndScanElectronicContentAsync(MediaItemViewModel? item)
        {
            if (item == null || !item.IsFileOrganizationForm)
            {
                return;
            }

            var files = _dialogService.PickFiles("选择电子资料文件", multiselect: true);
            if (files == null || files.Count == 0)
            {
                return;
            }

            if (item.HasScannedEntries
                && !_dialogService.ShowConfirm("重新选择文件将覆盖当前已扫描的目录/文件明细，是否继续？"))
            {
                return;
            }

            await ScanFileContentAsync(item, files);
        }

        private async Task RescanElectronicContentAsync(MediaItemViewModel? item)
        {
            if (item == null || !CanRescanElectronicContent(item))
            {
                return;
            }

            if (item.IsDirectoryOrganizationForm)
            {
                var directories = ResolveScannedDirectoryPaths(item);
                if (directories.Count == 0)
                {
                    _dialogService.ShowMessage("原扫描目录已不存在，请重新选择目录。");
                    return;
                }

                await ScanDirectoriesContentAsync(
                    item,
                    directories,
                    string.IsNullOrWhiteSpace(item.StorageRootFullPath) ? null : item.StorageRootFullPath);
                return;
            }

            if (item.LastScannedFilePaths.Count > 0)
            {
                var existingFiles = item.LastScannedFilePaths
                    .Where(File.Exists)
                    .ToList();

                if (existingFiles.Count == 0)
                {
                    _dialogService.ShowMessage("原扫描文件已不存在，请重新选择文件。");
                    return;
                }

                await ScanFileContentAsync(
                    item,
                    existingFiles,
                    string.IsNullOrWhiteSpace(item.StorageRootFullPath) ? null : item.StorageRootFullPath);
                return;
            }

            _dialogService.ShowMessage("当前没有可重新扫描的文件来源，请重新选择文件。");
        }

        private async Task ScanDirectoriesContentAsync(MediaItemViewModel item, IReadOnlyList<string> folders, string? storageRootDirectory = null)
        {
            _dialogService.SetBusyState(true);
            try
            {
                var result = await Task.Run(() => _electronicMediaContentScanService.ScanDirectories(folders, storageRootDirectory));
                ApplyScanResult(item, result, scannedDirectoryPaths: folders.ToList());
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"扫描失败：{ex.Message}");
            }
            finally
            {
                _dialogService.SetBusyState(false);
            }
        }

        private static List<string> ResolveScannedDirectoryPaths(MediaItemViewModel item)
        {
            if (item.LastScannedDirectoryPaths.Count > 0)
            {
                return item.LastScannedDirectoryPaths
                    .Where(Directory.Exists)
                    .ToList();
            }

            if (string.IsNullOrWhiteSpace(item.StoragePath) || item.ContentEntries.Count == 0)
            {
                return new List<string>();
            }

            return item.ContentEntries
                .Select(entry => Path.GetFullPath(Path.Combine(item.StoragePath, entry.RelativePath)))
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task ScanFileContentAsync(MediaItemViewModel item, IReadOnlyList<string> files, string? storageRootDirectory = null)
        {
            _dialogService.SetBusyState(true);
            try
            {
                var result = await Task.Run(() => _electronicMediaContentScanService.ScanFiles(files, storageRootDirectory));
                ApplyScanResult(item, result, files.ToList());
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"扫描失败：{ex.Message}");
            }
            finally
            {
                _dialogService.SetBusyState(false);
            }
        }

        private void ClearElectronicContent(MediaItemViewModel? item)
        {
            if (item == null || !item.HasScannedEntries)
            {
                return;
            }

            if (!_dialogService.ShowConfirm("确定清空当前目录/文件明细吗？"))
            {
                return;
            }

            item.ClearScannedContent();
            RecalculateElectronicItemContentCount(item);
        }

        private void ViewElectronicContentEntries(MediaItemViewModel? item)
        {
            if (item == null || !item.HasScannedEntries)
            {
                return;
            }

            string title = string.IsNullOrWhiteSpace(item.ContentDesc)
                ? "目录/文件明细"
                : $"{item.ContentDesc} - 目录/文件明细";

            var entries = item.ContentEntries
                .Select(entry => new ElectronicMediaItemEntryDisplayItem(
                    entry.EntryKind,
                    entry.EntryName,
                    FormatEntryCreatedDate(item, entry),
                    FormatEntryModifiedDate(item, entry),
                    entry.SizeMb))
                .ToList();

            _dialogService.ShowElectronicMediaItemEntriesDialog(title, entries, item.ContentScanSummaryText);
        }

        private static string FormatEntryCreatedDate(MediaItemViewModel item, ElectronicMediaItemEntryViewModel entry)
        {
            if (entry.CreatedAt.HasValue)
            {
                return ElectronicMediaItemSupport.FormatModifiedDate(entry.CreatedAt);
            }

            if (string.IsNullOrWhiteSpace(item.StorageRootFullPath) || string.IsNullOrWhiteSpace(entry.RelativePath))
            {
                return "-";
            }

            string fullPath = Path.GetFullPath(Path.Combine(item.StorageRootFullPath, entry.RelativePath));
            return ElectronicMediaItemSupport.FormatModifiedDate(
                ElectronicMediaItemSupport.ResolveEntryCreatedAt(fullPath, entry.EntryKind));
        }

        private static string FormatEntryModifiedDate(MediaItemViewModel item, ElectronicMediaItemEntryViewModel entry)
        {
            if (entry.ModifiedAt.HasValue)
            {
                return ElectronicMediaItemSupport.FormatModifiedDate(entry.ModifiedAt);
            }

            if (string.IsNullOrWhiteSpace(item.StorageRootFullPath) || string.IsNullOrWhiteSpace(entry.RelativePath))
            {
                return "-";
            }

            string fullPath = Path.GetFullPath(Path.Combine(item.StorageRootFullPath, entry.RelativePath));
            return ElectronicMediaItemSupport.FormatModifiedDate(
                ElectronicMediaItemSupport.ResolveEntryModifiedAt(fullPath, entry.EntryKind));
        }

        private string GetDefaultSourceType()
        {
            return SourceTypeOptions.FirstOrDefault() ?? string.Empty;
        }

        private string GetDefaultArchivePurpose()
        {
            return ArchivePurposeOptions.FirstOrDefault() ?? string.Empty;
        }

        private void ApplySourceTypeSelection()
        {
            if (CurrentRecord == null)
            {
                if (string.IsNullOrWhiteSpace(SelectedSourceType))
                {
                    SelectedSourceType = GetDefaultSourceType();
                }

                return;
            }

            SelectedSourceType = string.IsNullOrWhiteSpace(CurrentRecord.SourceType)
                ? GetDefaultSourceType()
                : CurrentRecord.SourceType;
        }

        private void ApplyArchivePurposeSelection()
        {
            if (CurrentRecord == null)
            {
                if (string.IsNullOrWhiteSpace(SelectedArchivePurpose))
                {
                    SelectedArchivePurpose = GetDefaultArchivePurpose();
                }

                return;
            }

            SelectedArchivePurpose = string.IsNullOrWhiteSpace(CurrentRecord.ArchivePurpose)
                ? GetDefaultArchivePurpose()
                : CurrentRecord.ArchivePurpose;
        }

        private static void ApplyOptions(ObservableCollection<string> target, IReadOnlyCollection<string> values)
        {
            target.Clear();
            foreach (var item in values
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                target.Add(item);
            }
        }

        /// <summary>
        /// 从持久化记录还原密级，并与域值列表对齐，避免 ComboBox 绑定丢失。
        /// </summary>
        private string ResolveConfidentialLevelFromRecord(string? storedLevel)
        {
            string normalized = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(storedLevel);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return ConfidentialLevelOptions.FirstOrDefault() ?? ArchiveRegisterDomainValues.ConfidentialLevelNone;
            }

            return ConfidentialLevelOptions.FirstOrDefault(option =>
                       string.Equals(option, normalized, StringComparison.OrdinalIgnoreCase))
                   ?? normalized;
        }

        #region Attachments
        private async Task LoadAttachments()
        {
            Attachments.Clear();
            if (CurrentRecord != null && !string.IsNullOrEmpty(CurrentRecord.FormNo))
            {
                try { var list = await _archiveRegisterService.GetAttachmentsByFormNoAsync(CurrentRecord.FormNo); foreach (var item in list) Attachments.Add(item); }
                catch (Exception ex) { Debug.WriteLine("加载附件失败: " + ex.Message); }
            }
        }
        private async Task UploadSignedAttachmentAsync()
        {
            if (CurrentRecord == null || string.IsNullOrEmpty(CurrentRecord.FormNo))
            {
                _dialogService.ShowMessage("请先生成或输入表单编号。");
                return;
            }

            if (!CanUploadSignedAttachment)
            {
                _dialogService.ShowMessage("请先执行「审批通过」，再上传签字件。");
                return;
            }

            var dlg = new OpenFileDialog { Multiselect = true, Title = "请选择附件（仅允许登记申请单、资料照片，且各1个）" };
            if (dlg.ShowDialog() == true)
            {
                foreach (var f in dlg.FileNames)
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        var fileContent = await File.ReadAllBytesAsync(f);
                        var result = await _archiveRegisterService.UploadAttachmentFlowAsync(CurrentRecord, _userContextService.CurrentUser, fi.Name, fi.Extension, fi.Length, fileContent);
                        if (result.Success && result.Attachment != null)
                        {
                            Attachments.Add(result.Attachment);
                        }
                        else
                        {
                            _dialogService.ShowMessage(result.Message);
                        }
                    }
                    catch (Exception ex) { _dialogService.ShowError($"上传失败: {ex.Message}"); }
                }

                await RefreshAttachmentRequirementsAsync();
                if (!AttachmentsMeetMandatoryRequirements)
                {
                    _dialogService.ShowMessage("附件已上传，但尚不满足继续办理要求：\n\n" + AttachmentRequirementHint);
                    return;
                }

                if (CurrentRecord.IsApprovedReceived)
                {
                    CurrentRecord.MarkAsSignedUploaded();
                    try
                    {
                        await _archiveRegisterService.SaveOrUpdateAsync(CurrentRecord);
                    }
                    catch (Exception ex)
                    {
                        _dialogService.ShowError("附件已齐全，但状态保存失败：" + ex.Message);
                        return;
                    }

                    OnPropertyChanged(nameof(CurrentRecord));
                    UpdateUIState();
                }

                MarkCommitted();
                _dialogService.ShowMessage("必备附件已齐全，记录状态已更新。下一步：确认办结。");
            }
        }
        private async Task DeleteAttachment(SystemAttachment a)
        {
            if (a == null) return;
            if (_dialogService.ShowConfirm($"确定删除“{a.FileName}”？"))
            {
                try
                {
                    var result = await _archiveRegisterService.DeleteAttachmentFlowAsync(a);
                    if (result.Success)
                    {
                        Attachments.Remove(a);
                        await RefreshAttachmentRequirementsAsync();
                        if (!AttachmentsMeetMandatoryRequirements)
                        {
                            _dialogService.ShowMessage("附件已删除，当前不满足必备附件要求，无法继续后续步骤：\n\n" + AttachmentRequirementHint);
                        }
                    }
                    else
                    {
                        _dialogService.ShowMessage(result.Message);
                    }
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError("删除失败: " + ex.Message);
                }
            }
        }
        private async void ViewAttachment(SystemAttachment a)
        {
            if (a == null) return;
            try
            {
                var result = await _archiveRegisterService.PrepareAttachmentViewFlowAsync(a);
                if (!result.Success || result.Attachment?.FileContent == null)
                {
                    _dialogService.ShowMessage(result.Message);
                    return;
                }

                var full = result.Attachment;
                if (_dialogService.ShowConfirm("直接打开？\n【确定】打开 【取消】另存为"))
                {
                    var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "_" + full.FileName);
                    await File.WriteAllBytesAsync(path, full.FileContent);
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
                else
                {
                    var dlg = new SaveFileDialog { FileName = full.FileName };
                    if (dlg.ShowDialog() == true) await File.WriteAllBytesAsync(dlg.FileName, full.FileContent);
                }
            }
            catch (Exception ex) { _dialogService.ShowError("错误: " + ex.Message); }
        }
        #endregion
    }
}