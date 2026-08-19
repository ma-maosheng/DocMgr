using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Repositories.YearlyArchive;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    public sealed class FilingFactWriter : IFilingFactWriter
    {
        private readonly IArchiveFilingFactRepository _filingFactRepository;
        private readonly IArchiveMaterialTransactionWriter _materialTransactionWriter;
        private readonly IArchiveMaterialTransactionRepository _materialTransactionRepository;

        public FilingFactWriter(
            IArchiveFilingFactRepository filingFactRepository,
            IArchiveMaterialTransactionWriter materialTransactionWriter,
            IArchiveMaterialTransactionRepository materialTransactionRepository)
        {
            _filingFactRepository = filingFactRepository;
            _materialTransactionWriter = materialTransactionWriter;
            _materialTransactionRepository = materialTransactionRepository;
        }

        public async Task WriteForSimulatedLinksAsync(
            YearlyArchiveBox box,
            IReadOnlyList<YearlyArchiveBoxMediaItemLink> links,
            IReadOnlyList<YearlyArchiveRegisterMediaItem> mediaItems,
            DateTime filedAt,
            string filedBy)
        {
            ArgumentNullException.ThrowIfNull(box);
            ArgumentNullException.ThrowIfNull(links);
            ArgumentNullException.ThrowIfNull(mediaItems);

            if (links.Count == 0)
            {
                return;
            }

            var mediaItemLookup = mediaItems.ToDictionary(item => item.Id);
            var facts = new List<YearlyArchiveFilingFact>();
            int year = filedAt.Year;
            int nextSequence = await GetNextSequenceAsync(mediaKind: ArchiveRegisterDomainValues.MediaKindSimulated, year);

            foreach (var link in links)
            {
                if (link.Id <= 0)
                {
                    continue;
                }

                if (await _filingFactRepository.ExistsBySourceLinkAsync(
                        FilingFactSourceLinkType.BoxMediaItemLink,
                        link.Id))
                {
                    continue;
                }

                if (!mediaItemLookup.TryGetValue(link.YearlyArchiveRegisterMediaItemId, out var mediaItem))
                {
                    continue;
                }

                var fact = ArchiveFilingFactRepository.BuildSimulatedFactFromLink(link, box, mediaItem);
                fact.FiledAt = filedAt;
                fact.FiledBy = filedBy.Trim();
                fact.FilingFactNo = BuildFilingFactNo(fact.MediaKind, year, nextSequence++);
                facts.Add(fact);
            }

            if (facts.Count == 0)
            {
                return;
            }

            _filingFactRepository.AddFilingFacts(facts);
            await _filingFactRepository.SaveChangesAsync();
            await _materialTransactionWriter.AppendFilingTransactionsAsync(facts);
            await _materialTransactionRepository.SaveChangesAsync();
        }

        public async Task WriteForElectronicLinksAsync(
            YearlyElectronicArchiveUnit unit,
            IReadOnlyList<YearlyElectronicArchiveUnitMediaItemLink> links,
            DateTime filedAt,
            string filedBy,
            int? numberingYear = null)
        {
            ArgumentNullException.ThrowIfNull(unit);
            ArgumentNullException.ThrowIfNull(links);

            if (links.Count == 0)
            {
                return;
            }

            var facts = new List<YearlyArchiveFilingFact>();
            int year = numberingYear ?? filedAt.Year;
            int nextSequence = await GetNextSequenceAsync(ArchiveRegisterDomainValues.MediaKindElectronic, year);

            foreach (var link in links)
            {
                if (link.Id <= 0)
                {
                    continue;
                }

                if (await _filingFactRepository.ExistsBySourceLinkAsync(
                        FilingFactSourceLinkType.ElectronicMediaItemLink,
                        link.Id))
                {
                    continue;
                }

                var mediaItem = link.MediaItem;
                if (mediaItem == null)
                {
                    continue;
                }

                var fact = ArchiveFilingFactRepository.BuildElectronicFactFromLink(link, unit, mediaItem);
                fact.FiledAt = filedAt;
                fact.FiledBy = filedBy.Trim();
                fact.FilingFactNo = BuildFilingFactNo(fact.MediaKind, year, nextSequence++);
                fact.ArchiveCopyRole = FilingFactArchiveCopyRole.Original;
                facts.Add(fact);
            }

            if (facts.Count == 0)
            {
                return;
            }

            _filingFactRepository.AddFilingFacts(facts);
            await _filingFactRepository.SaveChangesAsync();
            await _materialTransactionWriter.AppendFilingTransactionsAsync(facts);
            await _materialTransactionRepository.SaveChangesAsync();
        }

        public async Task WriteBackupElectronicLinksAsync(
            YearlyElectronicArchiveUnit unit,
            IReadOnlyList<BackupElectronicLinkWriteItem> links,
            IReadOnlyDictionary<int, int> primaryFilingFactIdByOriginalLinkId,
            DateTime filedAt,
            string filedBy,
            string backupRemark)
        {
            ArgumentNullException.ThrowIfNull(unit);
            ArgumentNullException.ThrowIfNull(links);
            ArgumentNullException.ThrowIfNull(primaryFilingFactIdByOriginalLinkId);

            if (links.Count == 0)
            {
                return;
            }

            var facts = new List<YearlyArchiveFilingFact>();
            int year = filedAt.Year;
            int nextSequence = await GetNextSequenceAsync(ArchiveRegisterDomainValues.MediaKindElectronic, year);

            foreach (var item in links)
            {
                var link = item.Link;
                if (link.Id <= 0)
                {
                    continue;
                }

                if (await _filingFactRepository.ExistsBySourceLinkAsync(
                        FilingFactSourceLinkType.ElectronicMediaItemLink,
                        link.Id))
                {
                    continue;
                }

                var mediaItem = link.MediaItem;
                if (mediaItem == null)
                {
                    continue;
                }

                if (!primaryFilingFactIdByOriginalLinkId.TryGetValue(item.OriginalSourceLinkId, out int primaryFactId)
                    || primaryFactId <= 0)
                {
                    throw new InvalidOperationException(
                        $"未找到原件 link [{item.OriginalSourceLinkId}] 对应的立档事实。");
                }

                var fact = ArchiveFilingFactRepository.BuildElectronicFactFromLink(link, unit, mediaItem);
                fact.FiledAt = filedAt;
                fact.FiledBy = filedBy.Trim();
                fact.FilingFactNo = BuildFilingFactNo(fact.MediaKind, year, nextSequence++);
                fact.PrimaryFilingFactId = primaryFactId;
                fact.ArchiveCopyRole = FilingFactArchiveCopyRole.Backup;
                fact.LifecycleRemark = backupRemark.Trim();
                facts.Add(fact);
            }

            if (facts.Count == 0)
            {
                return;
            }

            _filingFactRepository.AddFilingFacts(facts);
            await _filingFactRepository.SaveChangesAsync();
            await _materialTransactionWriter.AppendFilingTransactionsAsync(facts);
            await _materialTransactionRepository.SaveChangesAsync();
        }

        private async Task<int> GetNextSequenceAsync(string mediaKind, int year)
        {
            string prefix = $"立档-{mediaKind}-{year}-";
            string? lastNo = await _filingFactRepository.GetLastFilingFactNoByPrefixAsync(prefix);
            if (string.IsNullOrWhiteSpace(lastNo) || lastNo.Length <= prefix.Length
                || !int.TryParse(lastNo[prefix.Length..], out int parsed) || parsed <= 0)
            {
                return 1;
            }

            return parsed + 1;
        }

        private static string BuildFilingFactNo(string mediaKind, int year, int sequence)
            => $"立档-{mediaKind}-{year}-{sequence:D6}";
    }
}
