namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 保存/登记归还单请求。
    /// </summary>
    public sealed class SaveReturnRequest
    {
        public YearlyArchiveReturnRecord Record { get; set; } = new();

        public List<YearlyArchiveReturnItem> Items { get; set; } = new();

        /// <summary>true=提交登记（置“已登记”）；false=保存草稿。</summary>
        public bool SubmitForRegistration { get; set; }
    }
}
