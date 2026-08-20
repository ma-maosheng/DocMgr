namespace DocMgr.Models.YearlyArchive
{
    public static class ArchiveRegisterDomainValues
    {
        public const string SourceTypeInternal = "内部";
        public const string SourceTypeExternal = "外来";
        /// <summary>资料室管理员对过往年度在库硬盘直接登记并立档，无需申请审批。</summary>
        public const string SourceTypeStockDirect = "存量直办";

        /// <summary>存量直办提供单位，固定为资料室。</summary>
        public const string ProvideUnitArchiveRoom = "资料室";

        public const string MediaKindElectronic = "电子";
        public const string MediaKindSimulated = "模拟";
        public const string MediaKindBlankHardDisk = "空白盘";
        /// <summary>裸损坏硬盘（损坏硬盘专用档口）。</summary>
        public const string MediaKindDamagedHardDisk = "损坏盘";
        /// <summary>裸损坏数据光盘（损坏光盘专用档口）。</summary>
        public const string MediaKindDamagedOpticalDisc = "损坏光盘";

        public const string ElectronicMediaTypeUsbDrive = "U盘";
        public const string ElectronicMediaTypeOpticalDisc = "光盘";
        public const string ElectronicMediaTypeHardDisk = "硬盘";
        public const string ElectronicMediaTypeInnerNetwork = "内网";

        /// <summary>历史模拟介质类型：装订文本（迁移后拆为载体+分类）。</summary>
        public const string LegacySimulatedMediaTypeBoundText = "装订文本";

        /// <summary>历史模拟介质类型：散页文本。</summary>
        public const string LegacySimulatedMediaTypeLooseText = "散页文本";

        /// <summary>历史模拟介质类型：散页图件。</summary>
        public const string LegacySimulatedMediaTypeLooseMap = "散页图件";

        /// <summary>历史模拟介质类型：大幅图件。</summary>
        public const string LegacySimulatedMediaTypeLargeMap = "大幅图件";

        /// <summary>历史模拟介质类型：其他。</summary>
        public const string LegacySimulatedMediaTypeOther = "其他";

        public const string SimulatedMediaTypePrintingPaper = "打印纸";
        public const string SimulatedMediaTypeDrawingPaper = "绘图纸";
        public const string SimulatedMediaTypePhotoPaper = "打印相纸";
        public const string SimulatedMediaTypePhotosensitiveFilm = "感光胶片";
        public const string SimulatedMediaTypePhotosensitivePaper = "感光相纸";

        public const string SimulatedMaterialCategoryText = "文本";
        public const string SimulatedMaterialCategoryMap = "图件";

        public const string SimulatedOrganizationFormLoose = "散页";
        public const string SimulatedOrganizationFormBound = "装订";

        public const string SimulatedSubCategoryExternalMaterial = "外来资料类";
        public const string SimulatedSubCategoryPlanningDesign = "策划设计类";
        public const string SimulatedSubCategoryInspectionRecord = "检查记录类";
        public const string SimulatedSubCategorySummaryReport = "总结报告类";
        public const string SimulatedSubCategoryOther = "其他";
        public const string SimulatedSubCategoryExternalMap = "外来图件类";
        public const string SimulatedSubCategoryProcessMap = "过程图件类";
        public const string SimulatedSubCategoryResultMap = "成果图件类";
        public const string SimulatedSubCategoryOtherMap = "其他";

        public const string SimulatedMaterialCategoryTextScope =
            "MaterialCategory=" + SimulatedMaterialCategoryText;
        public const string SimulatedMaterialCategoryMapScope =
            "MaterialCategory=" + SimulatedMaterialCategoryMap;

        public const string ElectronicDispositionReturn = "介质带回";
        public const string ElectronicDispositionRetain = "介质留存";
        public const string ElectronicDispositionNone = "无需处置";
        public const string SimulatedDispositionRetain = ElectronicDispositionRetain;

        public const string ItemTypeData = "资料";
        public const string ItemTypeProof = "证明";

        /// <summary>证明材料名称为「无」时表示未附证明材料。</summary>
        public const string ProofMaterialNoneText = "无";

        /// <summary>附件类别：签批交接单。</summary>
        public const string AttachmentKindSignedHandoverForm = "SignedHandoverForm";

        /// <summary>附件类别：资料照片。</summary>
        public const string AttachmentKindMaterialPhoto = "MaterialPhoto";

        /// <summary>附件类别：证明材料扫描件。</summary>
        public const string AttachmentKindProofMaterialScan = "ProofMaterialScan";

        /// <summary>附件类别：其他附件（可选）。</summary>
        public const string AttachmentKindOther = "Other";

        /// <summary>历史附件文件名关键字：登记申请单（兼容旧数据）。</summary>
        public const string LegacyAttachmentFileNameRegisterForm = "登记申请单";

        /// <summary>历史附件文件名关键字：签批交接单。</summary>
        public const string LegacyAttachmentFileNameSignedHandover = "签批交接单";

        /// <summary>历史附件文件名关键字：资料照片。</summary>
        public const string LegacyAttachmentFileNameMaterialPhoto = "资料照片";

        /// <summary>历史附件文件名关键字：证明材料。</summary>
        public const string LegacyAttachmentFileNameProofMaterial = "证明材料";

        public const string ElectronicMaterialCategoryDocument = "文档类";
        public const string ElectronicMaterialCategoryData = "数据类";
        /// <summary>存量直办默认资料子类（数据类域值）。</summary>
        public const string DefaultStockDirectSubCategory = "最终成果数据";
        public const string ElectronicMaterialCategorySoftware = "软件类";

        /// <summary>目录型：子项有统一根目录；根下明细可为目录、文件或二者混合。</summary>
        public const string ElectronicDataOrganizationFormDirectory = "目录型";
        /// <summary>文件型：无统一根目录，全部明细必须为文件。</summary>
        public const string ElectronicDataOrganizationFormFile = "文件型";

        public const string ElectronicEntryKindDirectory = "目录";
        public const string ElectronicEntryKindFile = "文件";

        public const string ElectronicMaterialCategoryDocumentScope =
            "MaterialCategory=" + ElectronicMaterialCategoryDocument;
        public const string ElectronicMaterialCategoryDataScope =
            "MaterialCategory=" + ElectronicMaterialCategoryData;
        public const string ElectronicMaterialCategorySoftwareScope =
            "MaterialCategory=" + ElectronicMaterialCategorySoftware;

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

        /// <summary>模拟资料载体类型（纸基/胶片）。</summary>
        public static IReadOnlyList<string> SimulatedDataMediaTypes { get; } =
        [
            SimulatedMediaTypePrintingPaper,
            SimulatedMediaTypeDrawingPaper,
            SimulatedMediaTypePhotoPaper,
            SimulatedMediaTypePhotosensitiveFilm,
            SimulatedMediaTypePhotosensitivePaper
        ];

        /// <summary>存档文本直办与建档申请共用模拟资料载体类型。</summary>
        public static IReadOnlyList<string> StockTextArchiveMediaTypes => SimulatedDataMediaTypes;

        public static IReadOnlyList<string> SimulatedMaterialCategories { get; } =
            [SimulatedMaterialCategoryText, SimulatedMaterialCategoryMap];

        public static IReadOnlyList<string> SimulatedTextSubCategories { get; } =
        [
            SimulatedSubCategoryExternalMaterial,
            SimulatedSubCategoryPlanningDesign,
            SimulatedSubCategoryInspectionRecord,
            SimulatedSubCategorySummaryReport,
            SimulatedSubCategoryOther
        ];

        public static IReadOnlyList<string> SimulatedMapSubCategories { get; } =
        [
            SimulatedSubCategoryExternalMap,
            SimulatedSubCategoryProcessMap,
            SimulatedSubCategoryResultMap,
            SimulatedSubCategoryOtherMap
        ];

        public static IReadOnlyList<string> SimulatedOrganizationForms { get; } =
            [SimulatedOrganizationFormLoose, SimulatedOrganizationFormBound];

        /// <summary>是否为模拟资料允许的载体类型。</summary>
        public static bool IsSimulatedDataMediaType(string? mediaType)
        {
            string normalized = mediaType?.Trim() ?? string.Empty;
            return SimulatedDataMediaTypes.Any(item =>
                string.Equals(item, normalized, StringComparison.Ordinal));
        }

        /// <summary>是否为存档文本直办允许的模拟介质类型。</summary>
        public static bool IsStockTextArchiveMediaType(string? mediaType) =>
            IsSimulatedDataMediaType(mediaType);

        public static IReadOnlyList<string> GetSimulatedSubCategories(string? materialCategory)
        {
            return string.Equals(materialCategory?.Trim(), SimulatedMaterialCategoryMap, StringComparison.Ordinal)
                ? SimulatedMapSubCategories
                : string.Equals(materialCategory?.Trim(), SimulatedMaterialCategoryText, StringComparison.Ordinal)
                    ? SimulatedTextSubCategories
                    : Array.Empty<string>();
        }

        /// <summary>申请人是否声明附有证明材料（<see cref="YearlyArchiveRegisterRecord.ProofMaterialNote"/> 不为「无」）。</summary>
        public static bool HasProofMaterial(string? proofMaterialNote)
        {
            string note = proofMaterialNote?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(note)
                && !string.Equals(note, ProofMaterialNoneText, StringComparison.Ordinal);
        }

        /// <summary>规范化证明材料备注：有名称则保留，否则为「无」。</summary>
        public static string NormalizeProofMaterialNote(string? proofMaterialNote)
        {
            string note = proofMaterialNote?.Trim() ?? string.Empty;
            return HasProofMaterial(note) ? note : ProofMaterialNoneText;
        }

        /// <summary>申请时已声明有证明材料时，办结前须上传证明材料扫描件。</summary>
        public static bool RequiresProofMaterialAttachment(string? proofMaterialNote) =>
            HasProofMaterial(proofMaterialNote);

        /// <summary>解析建档审批附件类别（优先 FileCategory；空类别时按历史文件名关键字兼容）。</summary>
        public static string ResolveAttachmentKind(string? fileCategory, string? fileName)
        {
            string category = fileCategory?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(category))
            {
                return category;
            }

            string name = fileName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                return AttachmentKindOther;
            }

            if (name.Contains(LegacyAttachmentFileNameProofMaterial, StringComparison.OrdinalIgnoreCase))
            {
                return AttachmentKindProofMaterialScan;
            }

            if (name.Contains(LegacyAttachmentFileNameMaterialPhoto, StringComparison.OrdinalIgnoreCase))
            {
                return AttachmentKindMaterialPhoto;
            }

            if (name.Contains(LegacyAttachmentFileNameRegisterForm, StringComparison.OrdinalIgnoreCase)
                || name.Contains(LegacyAttachmentFileNameSignedHandover, StringComparison.OrdinalIgnoreCase))
            {
                return AttachmentKindSignedHandoverForm;
            }

            return AttachmentKindOther;
        }

        /// <summary>是否为建档审批允许的附件类别。</summary>
        public static bool IsKnownAttachmentKind(string? attachmentKind)
        {
            string kind = attachmentKind?.Trim() ?? string.Empty;
            return string.Equals(kind, AttachmentKindSignedHandoverForm, StringComparison.Ordinal)
                || string.Equals(kind, AttachmentKindMaterialPhoto, StringComparison.Ordinal)
                || string.Equals(kind, AttachmentKindProofMaterialScan, StringComparison.Ordinal)
                || string.Equals(kind, AttachmentKindOther, StringComparison.Ordinal);
        }

        public static string GetAttachmentKindDisplayName(string? attachmentKind) =>
            attachmentKind?.Trim() switch
            {
                AttachmentKindSignedHandoverForm => "签批交接单",
                AttachmentKindMaterialPhoto => "资料照片",
                AttachmentKindProofMaterialScan => "证明材料",
                AttachmentKindOther => "其他附件",
                _ => "附件"
            };
    }
}
