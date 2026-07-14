namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料登记页所需的字段域值集合。
    /// </summary>
    public sealed class ArchiveRegisterPageDomainOptions
    {
        /// <summary>
        /// 资料来源。
        /// </summary>
        public IReadOnlyList<string> SourceTypes { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 归档目的。
        /// </summary>
        public IReadOnlyList<string> ArchivePurposes { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 模拟介质类别。
        /// </summary>
        public IReadOnlyList<string> SimulatedMediaKinds { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 资料子项类型。
        /// </summary>
        public IReadOnlyList<string> DataItemTypes { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 证明材料子项类型。
        /// </summary>
        public IReadOnlyList<string> ProofItemTypes { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 电子资料介质类型。
        /// </summary>
        public IReadOnlyList<string> DataElectronicMediaTypes { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 模拟资料介质类型。
        /// </summary>
        public IReadOnlyList<string> DataSimulatedMediaTypes { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 模拟证明材料介质类型。
        /// </summary>
        public IReadOnlyList<string> ProofSimulatedMediaTypes { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 电子资料处置方式。
        /// </summary>
        public IReadOnlyList<string> DataElectronicDispositions { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 模拟资料处置方式。
        /// </summary>
        public IReadOnlyList<string> DataSimulatedDispositions { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 电子资料类型。
        /// </summary>
        public IReadOnlyList<string> ElectronicMaterialCategories { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 文档类所属子类。
        /// </summary>
        public IReadOnlyList<string> ElectronicDocumentSubCategories { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 数据类所属子类。
        /// </summary>
        public IReadOnlyList<string> ElectronicDataSubCategories { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 电子资料数据组织形式。
        /// </summary>
        public IReadOnlyList<string> ElectronicDataOrganizationForms { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 资料子项密级选项。
        /// </summary>
        public IReadOnlyList<string> ConfidentialLevels { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 生产管理科意见。
        /// </summary>
        public IReadOnlyList<string> ProdOpinionOptions { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 科研开发室意见。
        /// </summary>
        public IReadOnlyList<string> RndOpinionOptions { get; init; } = Array.Empty<string>();

        /// <summary>
        /// 分管领导意见。
        /// </summary>
        public IReadOnlyList<string> DeputyOpinionOptions { get; init; } = Array.Empty<string>();
    }
}
