namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 申请单统一工作流状态（库内 int 存储，展示文案与硬盘出库申请单一字不差）。
    /// 适用于：硬盘出库/归还、资料立档、资料借出、资料归还。
    /// </summary>
    public static class ApplicationWorkflowStatus
    {
        public const int Draft = 0;
        public const int Submitted = 1;
        public const int Approved = 2;
        public const int SignedUploaded = 3;
        public const int Completed = 4;
        public const int Withdrawn = 5;
        public const int ForceWithdrawn = 6;

        /// <summary>兼容别名：未提交 = 草稿。</summary>
        public const int Unsubmitted = Draft;

        /// <summary>兼容别名：撤回作废。</summary>
        public const int WithdrawnVoid = Withdrawn;

        /// <summary>兼容别名：强制作废。</summary>
        public const int ForceVoided = ForceWithdrawn;

        public const string TextDraft = "当前草稿-待提交";
        public const string TextSubmitted = "已提交-待审批";
        public const string TextApproved = "已审批-待实物交接";
        public const string TextSignedUploaded = "已实物交接-待上传签批交接单";
        public const string TextCompleted = "已办结（业务已闭环）";
        public const string TextWithdrawn = "已作废（撤回）";
        public const string TextForceWithdrawn = "已作废（强制）";

        /// <summary>历史短文案，仅用于启动期数据归一化。</summary>
        public const string LegacyTextDraft = "未提交";
        public const string LegacyTextSubmitted = "已提交";
        public const string LegacyTextApproved = "已审批";
        public const string LegacyTextSignedUploaded = "已上传签字件";
        public const string LegacyTextCompleted = "已办结";
        public const string LegacyTextWithdrawn = "已撤回作废";
        public const string LegacyTextForceWithdrawn = "已强制作废";

        /// <summary>全部状态选项（按业务顺序），供筛选下拉使用。</summary>
        public static IReadOnlyList<(int Value, string Label)> AllOptions { get; } =
        [
            (Draft, TextDraft),
            (Submitted, TextSubmitted),
            (Approved, TextApproved),
            (SignedUploaded, TextSignedUploaded),
            (Completed, TextCompleted),
            (Withdrawn, TextWithdrawn),
            (ForceWithdrawn, TextForceWithdrawn),
        ];

        /// <summary>将状态码转为用户可见文案。</summary>
        public static string ToDisplay(int status) => status switch
        {
            Draft => TextDraft,
            Submitted => TextSubmitted,
            Approved => TextApproved,
            SignedUploaded => TextSignedUploaded,
            Completed => TextCompleted,
            Withdrawn => TextWithdrawn,
            ForceWithdrawn => TextForceWithdrawn,
            _ => "未知"
        };

        /// <summary>是否为有效状态码。</summary>
        public static bool IsDefined(int status) =>
            status is >= Draft and <= ForceWithdrawn;

        /// <summary>
        /// 将历史字符串状态（含旧短文案与现行中文常量）解析为 int。
        /// 无法识别时返回 null。
        /// </summary>
        public static int? TryParseStoredText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string trimmed = value.Trim();
            return trimmed switch
            {
                TextDraft or LegacyTextDraft or "草稿" => Draft,
                TextSubmitted or LegacyTextSubmitted or "已登记" or "已登记归还信息" => Submitted,
                TextApproved or LegacyTextApproved => Approved,
                TextSignedUploaded or LegacyTextSignedUploaded or "已办结审批" => SignedUploaded,
                TextCompleted or LegacyTextCompleted or "已办结出库" => Completed,
                TextWithdrawn or LegacyTextWithdrawn or "已作废" => Withdrawn,
                TextForceWithdrawn or LegacyTextForceWithdrawn => ForceWithdrawn,
                _ => null
            };
        }

        /// <summary>
        /// 资料归还旧 4 态 → 统一 7 态。
        /// 旧：0草稿 / 1已登记 / 2已办结 / 3已作废。
        /// </summary>
        public static int NormalizeLegacyReturnStatus(int status) => status switch
        {
            0 => Draft,
            1 => Submitted,
            2 => Completed,
            3 => Withdrawn,
            _ when IsDefined(status) => status,
            _ => Draft
        };
    }
}
