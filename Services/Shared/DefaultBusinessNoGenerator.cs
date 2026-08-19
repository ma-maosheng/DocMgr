using DocMgr.Repositories.Interfaces;

namespace DocMgr.Services.Shared
{
    /// <summary>
    /// 默认业务编号生成器。
    /// </summary>
    public sealed class DefaultBusinessNoGenerator : IBusinessNoGenerator
    {
        private readonly IArchiveRegisterRepository _archiveRegisterRepository;
        private readonly IArchiveOutboundRepository _archiveOutboundRepository;
        private readonly IArchiveReturnRepository _archiveReturnRepository;
        private readonly IHardDiskMediaRepository _hardDiskMediaRepository;
        private readonly IHardDiskInventoryRegisterRepository _hardDiskInventoryRegisterRepository;
        private readonly IHardDiskDisposalRepository _hardDiskDisposalRepository;
        private readonly IArchiveInventoryRegisterRepository _archiveInventoryRegisterRepository;
        private readonly IArchiveDisposalRepository _archiveDisposalRepository;
        private readonly INetworkTransferRepository _networkTransferRepository;

        public DefaultBusinessNoGenerator(
            IArchiveRegisterRepository archiveRegisterRepository,
            IArchiveOutboundRepository archiveOutboundRepository,
            IArchiveReturnRepository archiveReturnRepository,
            IHardDiskMediaRepository hardDiskMediaRepository,
            IHardDiskInventoryRegisterRepository hardDiskInventoryRegisterRepository,
            IHardDiskDisposalRepository hardDiskDisposalRepository,
            IArchiveInventoryRegisterRepository archiveInventoryRegisterRepository,
            IArchiveDisposalRepository archiveDisposalRepository,
            INetworkTransferRepository networkTransferRepository)
        {
            _archiveRegisterRepository = archiveRegisterRepository;
            _archiveOutboundRepository = archiveOutboundRepository;
            _archiveReturnRepository = archiveReturnRepository;
            _hardDiskMediaRepository = hardDiskMediaRepository;
            _hardDiskInventoryRegisterRepository = hardDiskInventoryRegisterRepository;
            _hardDiskDisposalRepository = hardDiskDisposalRepository;
            _archiveInventoryRegisterRepository = archiveInventoryRegisterRepository;
            _archiveDisposalRepository = archiveDisposalRepository;
            _networkTransferRepository = networkTransferRepository;
        }

        /// <summary>
        /// 根据业务编号类别生成下一编号。
        /// </summary>
        public async Task<string> GenerateNextNoAsync(
            BusinessNoCategory category,
            int? numberingYear = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rule = DefaultBusinessPolicyProvider.GetRule(category);
            int yearSegment = numberingYear ?? DateTime.Now.Year;
            string prefix = $"{rule.Prefix}-{yearSegment}-";

            string? lastNo = await GetLastBusinessNoByPrefixAsync(category, prefix).ConfigureAwait(false);
            int nextSequence = 1;

            if (!string.IsNullOrWhiteSpace(lastNo) && lastNo.Length > prefix.Length)
            {
                string sequenceText = lastNo[prefix.Length..];
                if (int.TryParse(sequenceText, out int parsedSequence) && parsedSequence > 0)
                {
                    nextSequence = parsedSequence + 1;
                }
            }

            return $"{prefix}{nextSequence.ToString($"D{rule.SequenceLength}")}";
        }

        private Task<string?> GetLastBusinessNoByPrefixAsync(BusinessNoCategory category, string prefix)
        {
            return category switch
            {
                BusinessNoCategory.AssetReturnRegister => GetLastReturnRegisterBusinessNoAsync(prefix),
                BusinessNoCategory.AssetInboundApply or
                BusinessNoCategory.AssetOutboundApply or
                BusinessNoCategory.AssetDestroyApply => GetLastArchiveBusinessNoAsync(prefix),
                BusinessNoCategory.DiskInboundRegister or
                BusinessNoCategory.DiskOutboundApply => _hardDiskMediaRepository.GetLastApplicationNoByPrefixAsync(prefix),
                BusinessNoCategory.DiskInventoryRegister => _hardDiskInventoryRegisterRepository.GetLastRegisterNoByPrefixAsync(prefix),
                BusinessNoCategory.DiskDisposalApply => _hardDiskDisposalRepository.GetLastDisposalNoByPrefixAsync(prefix),
                BusinessNoCategory.ArchiveInventoryRegister => _archiveInventoryRegisterRepository.GetLastRegisterNoByPrefixAsync(prefix),
                BusinessNoCategory.ArchiveDisposalApply => _archiveDisposalRepository.GetLastDisposalNoByPrefixAsync(prefix),
                BusinessNoCategory.NetworkInboundApply => _networkTransferRepository.GetLastInboundNoByPrefixAsync(prefix),
                BusinessNoCategory.NetworkOutboundApply => _networkTransferRepository.GetLastOutboundNoByPrefixAsync(prefix),
                BusinessNoCategory.NetworkDisposalApply => _networkTransferRepository.GetLastDisposalNoByPrefixAsync(prefix),
                _ => throw new ArgumentException($"不支持的业务编号类别：{category}", nameof(category))
            };
        }

        private async Task<string?> GetLastReturnRegisterBusinessNoAsync(string prefix)
        {
            var registerFormNos = await _archiveRegisterRepository.GetFormNosByPrefixAsync(prefix).ConfigureAwait(false);
            var returnNos = await _archiveReturnRepository.GetReturnNosByPrefixAsync(prefix).ConfigureAwait(false);

            var combined = registerFormNos.Concat(returnNos).ToList();
            if (combined.Count == 0)
            {
                return null;
            }

            return combined
                .OrderByDescending(no => no, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private async Task<string?> GetLastArchiveBusinessNoAsync(string prefix)
        {
            var registerFormNos = await _archiveRegisterRepository.GetFormNosByPrefixAsync(prefix).ConfigureAwait(false);
            var outboundNos = await _archiveOutboundRepository.GetOutboundNosByPrefixAsync(prefix).ConfigureAwait(false);

            var combined = registerFormNos.Concat(outboundNos).ToList();
            if (combined.Count == 0)
            {
                return null;
            }

            return combined
                .OrderByDescending(no => no, StringComparer.Ordinal)
                .FirstOrDefault();
        }
    }
}
