namespace DocMgr.ViewModels.Shared
{
    /// <summary>
    /// 资料明细面板：智能折叠与分页约定。
    /// </summary>
    public static class ItemDetailsPanelDomainValues
    {
        /// <summary>不超过该条数时默认展开明细。</summary>
        public const int SmartExpandThreshold = 5;

        /// <summary>超过该条数时启用分页。</summary>
        public const int DefaultPageSize = 20;
    }
}
