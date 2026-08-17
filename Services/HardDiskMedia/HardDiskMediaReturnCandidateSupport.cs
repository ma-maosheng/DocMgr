using DocMgr.Models.HardDiskMedia;

namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 介质归还登记候选项匹配辅助逻辑。
    /// </summary>
    internal static class HardDiskMediaReturnCandidateSupport
    {
        public static bool MatchesCandidateSource(
            HardDiskMediaReturnCandidate candidate,
            int? sourceApplicationId,
            int? sourceOutboundRecordId,
            int? sourceNetworkOutboundRecordId = null)
        {
            ArgumentNullException.ThrowIfNull(candidate);

            if (sourceNetworkOutboundRecordId is > 0
                && candidate.SourceNetworkOutboundRecordId == sourceNetworkOutboundRecordId)
            {
                return true;
            }

            if (sourceOutboundRecordId is > 0
                && candidate.SourceOutboundRecordId == sourceOutboundRecordId)
            {
                return true;
            }

            if (sourceApplicationId is > 0
                && candidate.SourceApplicationId == sourceApplicationId)
            {
                return true;
            }

            return false;
        }
    }
}
