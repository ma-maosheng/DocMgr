namespace DocMgr.Models.YearlyArchive
{
    public static class ArchiveRegisterDomainValues
    {
        public const string SourceTypeInternal = "内部";
        public const string SourceTypeExternal = "外来";

        public const string MediaKindElectronic = "电子";
        public const string MediaKindSimulated = "模拟";

        public const string ElectronicMediaTypeUsbDrive = "U盘";
        public const string ElectronicMediaTypeOpticalDisc = "光盘";
        public const string ElectronicMediaTypeHardDisk = "硬盘";
        public const string ElectronicMediaTypeInnerNetwork = "内网";

        public const string ElectronicDispositionReturn = "介质带回";
        public const string ElectronicDispositionRetain = "介质留存";
        public const string ElectronicDispositionNone = "无需处置";
        public const string SimulatedDispositionRetain = ElectronicDispositionRetain;

        public const string ItemTypeData = "资料";
        public const string ItemTypeProof = "证明";

        public const string ElectronicMaterialCategoryDocument = "文档类";
        public const string ElectronicMaterialCategoryData = "数据类";

        public const string ElectronicDataOrganizationFormDirectory = "目录型";
        public const string ElectronicDataOrganizationFormFile = "文件型";

        public const string ElectronicEntryKindDirectory = "目录";
        public const string ElectronicEntryKindFile = "文件";

        public const string ElectronicMaterialCategoryDocumentScope =
            "MaterialCategory=" + ElectronicMaterialCategoryDocument;
        public const string ElectronicMaterialCategoryDataScope =
            "MaterialCategory=" + ElectronicMaterialCategoryData;

        public const string ConfidentialLevelNone = "否";
        public const string LegacyConfidentialLevelNone = "无";

        public static string NormalizeConfidentialLevel(string? value)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            return string.Equals(normalized, LegacyConfidentialLevelNone, StringComparison.OrdinalIgnoreCase)
                ? ConfidentialLevelNone
                : normalized;
        }

        public static IReadOnlyList<string> ElectronicMediaKinds { get; } = [MediaKindElectronic];
        public static IReadOnlyList<string> SimulatedMediaKinds { get; } = [MediaKindSimulated];
        public static IReadOnlyList<string> DataItemTypes { get; } = [ItemTypeData];
        public static IReadOnlyList<string> ProofItemTypes { get; } = [ItemTypeProof];
    }
}
