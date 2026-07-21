namespace DocMgr.Models.YearlyArchive

{

    /// <summary>

    /// 资料归还（提档原件收回）领域常量。

    /// </summary>

    public static class ArchiveReturnDomainValues

    {

        /// <summary>归还单附件业务类型。</summary>

        public const string BusinessTypeAttachment = "ArchiveReturn";



        /// <summary>归还物状态：完整。</summary>

        public const string ConditionComplete = "Complete";



        /// <summary>归还物状态：灭失（模拟介质、光盘等不细分类型）。</summary>

        public const string ConditionLoss = "Loss";



        /// <summary>硬盘归还灭失子类型：硬盘灭失。</summary>

        public const string HardDiskLoss = "HardDiskLoss";



        /// <summary>硬盘归还灭失子类型：硬盘损坏。</summary>

        public const string HardDiskDamaged = "HardDiskDamaged";



        /// <summary>硬盘归还灭失子类型：硬盘正常资料不可读。</summary>

        public const string HardDiskUnreadable = "HardDiskUnreadable";



        /// <summary>旧版：完好（迁移映射为 <see cref="ConditionComplete"/>）。</summary>

        public const string ConditionIntact = "Intact";



        /// <summary>旧版：破损（硬盘迁移为 <see cref="HardDiskDamaged"/>，其余为 <see cref="ConditionLoss"/>）。</summary>

        public const string ConditionDamaged = "Damaged";



        /// <summary>旧版：缺失（迁移映射为 <see cref="ConditionLoss"/> 或硬盘灭失）。</summary>

        public const string ConditionMissing = "Missing";



        /// <summary>灭失归还报备签字件附件类别。</summary>

        public const string AttachmentKindSignedAbnormalReturnReport = "SignedAbnormalReturnReport";

        /// <summary>签批交接单附件类别。</summary>
        public const string AttachmentKindSignedHandover = "签批交接单";



        public static IReadOnlyList<(string Value, string Display)> CompleteAndLossOptions { get; } =

        [

            (ConditionComplete, "完整"),

            (ConditionLoss, "灭失"),

        ];



        public static IReadOnlyList<(string Value, string Display)> HardDiskConditionOptions { get; } =

        [

            (ConditionComplete, "完整"),

            (HardDiskLoss, "硬盘灭失"),

            (HardDiskDamaged, "硬盘损坏"),

            (HardDiskUnreadable, "硬盘正常资料不可读"),

        ];



        /// <summary>是否存在灭失归还明细（灭失份数 &gt; 0）。</summary>
        public static bool HasAbnormalReturnItems(IEnumerable<YearlyArchiveReturnItem> items) =>
            items.Any(HasLossReturnCopies);

        /// <summary>明细是否存在灭失份数。</summary>
        public static bool HasLossReturnCopies(YearlyArchiveReturnItem item) =>
            ResolveLossCopyCount(item) > 0;

        /// <summary>明细是否完好全额归还。</summary>
        public static bool IsFullyIntactReturn(YearlyArchiveReturnItem item) =>
            ResolveLossCopyCount(item) == 0;

        /// <summary>解析借出份数。</summary>
        public static int ResolveBorrowedCopyCount(YearlyArchiveReturnItem item) =>
            Math.Max(1, item.ReturnCopyCount);

        /// <summary>解析完好归还份数。</summary>
        public static int ResolveIntactReturnCopyCount(YearlyArchiveReturnItem item)
        {
            int borrowed = ResolveBorrowedCopyCount(item);
            if (item.IntactReturnCopyCount > 0 || item.LossCopyCount > 0)
            {
                return Math.Clamp(item.IntactReturnCopyCount, 0, borrowed);
            }

            if (IsLossCondition(item.ItemCondition))
            {
                return 0;
            }

            return borrowed;
        }

        /// <summary>解析灭失份数。</summary>
        public static int ResolveLossCopyCount(YearlyArchiveReturnItem item)
        {
            int borrowed = ResolveBorrowedCopyCount(item);
            if (item.IntactReturnCopyCount > 0 || item.LossCopyCount > 0)
            {
                if (item.LossCopyCount > 0)
                {
                    return Math.Clamp(item.LossCopyCount, 0, borrowed);
                }

                return Math.Max(0, borrowed - ResolveIntactReturnCopyCount(item));
            }

            return IsLossCondition(item.ItemCondition) ? borrowed : 0;
        }

        /// <summary>规范化明细份数字段并同步旧版 <see cref="YearlyArchiveReturnItem.ItemCondition"/>。</summary>
        public static void NormalizeReturnCopyCounts(YearlyArchiveReturnItem item)
        {
            int borrowed = ResolveBorrowedCopyCount(item);
            item.ReturnCopyCount = borrowed;
            int intact = ResolveIntactReturnCopyCount(item);
            item.IntactReturnCopyCount = intact;
            item.LossCopyCount = borrowed - intact;
            SyncItemConditionFromCopyCounts(item);
        }

        /// <summary>根据份数同步旧版归还物状态字段（兼容台账查询）。</summary>
        public static void SyncItemConditionFromCopyCounts(YearlyArchiveReturnItem item)
        {
            item.ItemCondition = ResolveLossCopyCount(item) > 0
                ? ConditionLoss
                : ConditionComplete;
        }

        /// <summary>构建归还份数摘要（打印/回执用）。</summary>
        public static string BuildReturnCopyCountSummary(YearlyArchiveReturnItem item)
        {
            int borrowed = ResolveBorrowedCopyCount(item);
            int intact = ResolveIntactReturnCopyCount(item);
            int loss = ResolveLossCopyCount(item);
            if (loss <= 0)
            {
                return $"借出{borrowed}份，完好归还{intact}份";
            }

            return $"借出{borrowed}份，完好归还{intact}份，灭失{loss}份";
        }



        /// <summary>归还物状态是否为灭失（非完整）。</summary>

        public static bool IsAbnormalCondition(string? value) => IsLossCondition(value);



        /// <summary>归还物状态是否为完整。</summary>

        public static bool IsCompleteCondition(string? value)

        {

            string normalized = NormalizeStoredCondition(value);

            return string.Equals(normalized, ConditionComplete, StringComparison.Ordinal)

                || string.Equals(normalized, ConditionIntact, StringComparison.Ordinal);

        }



        /// <summary>归还物状态是否为灭失（含硬盘灭失子类型）。</summary>

        public static bool IsLossCondition(string? value)

        {

            string normalized = NormalizeStoredCondition(value);

            return normalized.Length > 0

                && !IsCompleteCondition(normalized);

        }



        /// <summary>按介质类型返回可选归还状态。</summary>

        public static IReadOnlyList<(string Value, string Display)> GetConditionOptions(string? mediaKind, string? storageCarrierType)

        {

            if (IsElectronicHardDisk(mediaKind, storageCarrierType))

            {

                return HardDiskConditionOptions;

            }



            return CompleteAndLossOptions;

        }



        /// <summary>校验归还状态是否适用于指定介质。</summary>

        public static bool IsValidCondition(string? value, string? mediaKind, string? storageCarrierType)

        {

            string normalized = NormalizeStoredCondition(value);

            if (string.IsNullOrEmpty(normalized))

            {

                return false;

            }



            var allowed = GetConditionOptions(mediaKind, storageCarrierType);

            return allowed.Any(option => string.Equals(option.Value, normalized, StringComparison.Ordinal));

        }



        /// <summary>将旧版归还状态迁移为新版存储值。</summary>

        public static string NormalizeStoredCondition(string? value, string? mediaKind = null, string? storageCarrierType = null)

        {

            if (string.IsNullOrWhiteSpace(value))

            {

                return string.Empty;

            }



            string trimmed = value.Trim();

            if (string.Equals(trimmed, ConditionIntact, StringComparison.Ordinal))

            {

                return ConditionComplete;

            }



            if (string.Equals(trimmed, ConditionMissing, StringComparison.Ordinal))

            {

                return IsElectronicHardDisk(mediaKind, storageCarrierType)

                    ? HardDiskLoss

                    : ConditionLoss;

            }



            if (string.Equals(trimmed, ConditionDamaged, StringComparison.Ordinal))

            {

                return IsElectronicHardDisk(mediaKind, storageCarrierType)

                    ? HardDiskDamaged

                    : ConditionLoss;

            }



            return trimmed;

        }



        /// <summary>取归还物状态的中文显示。</summary>

        public static string GetConditionDisplay(string? value, string? mediaKind = null, string? storageCarrierType = null)

        {

            string normalized = NormalizeStoredCondition(value, mediaKind, storageCarrierType);

            if (string.IsNullOrEmpty(normalized))

            {

                return string.Empty;

            }



            foreach (var option in HardDiskConditionOptions)

            {

                if (string.Equals(option.Value, normalized, StringComparison.Ordinal))

                {

                    return option.Display;

                }

            }



            foreach (var option in CompleteAndLossOptions)

            {

                if (string.Equals(option.Value, normalized, StringComparison.Ordinal))

                {

                    return option.Display;

                }

            }



            return normalized;

        }



        /// <summary>判断是否为电子硬盘载体。</summary>

        public static bool IsElectronicHardDisk(string? mediaKind, string? storageCarrierType)

        {

            if (!string.Equals(

                    mediaKind?.Trim(),

                    ArchiveRegisterDomainValues.MediaKindElectronic,

                    StringComparison.Ordinal))

            {

                return false;

            }



            string carrier = storageCarrierType?.Trim() ?? string.Empty;

            if (carrier.Length == 0)

            {

                return false;

            }



            if (carrier.Contains("光盘", StringComparison.OrdinalIgnoreCase))

            {

                return false;

            }



            return carrier.Contains("硬盘", StringComparison.OrdinalIgnoreCase)

                || string.Equals(carrier, ArchiveRegisterDomainValues.ElectronicMediaTypeHardDisk, StringComparison.OrdinalIgnoreCase);

        }

    }

}

