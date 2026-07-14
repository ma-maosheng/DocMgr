using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 年度资料流转履历查询服务。
    /// </summary>
    public interface IArchiveMaterialTransactionService
    {
        Task<IReadOnlyList<MaterialTransactionTimelineRow>> GetTimelineByFilingFactIdAsync(int filingFactId);

        Task<IReadOnlyList<MaterialOutboundProcessNodeRow>> GetOutboundProcessNodesByFilingFactIdAsync(int filingFactId);
    }

    /// <summary>
    /// 年度资料流转履历写入器：在各业务办结路径同步留痕。
    /// </summary>
    public interface IArchiveMaterialTransactionWriter
    {
        Task AppendFilingTransactionsAsync(IReadOnlyList<YearlyArchiveFilingFact> facts);

        Task AppendRelocationTransactionsAsync(YearlyArchiveRelocationRecord record);

        Task AppendOutboundCompletionTransactionsAsync(YearlyArchiveOutboundRecord record);

        Task AppendReturnCompletionTransactionsAsync(
            YearlyArchiveReturnRecord returnRecord,
            YearlyArchiveOutboundRecord outboundRecord);
    }
}
