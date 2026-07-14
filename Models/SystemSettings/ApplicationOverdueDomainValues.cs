namespace DocMgr.Models.SystemSettings
{
    /// <summary>
    /// 申请单逾期强制作废时限域值。
    /// </summary>
    public static class ApplicationOverdueDomainValues
    {
        public const string SameDay = "SameDay";
        public const string Days7 = "Days7";
        public const string Days30 = "Days30";

        public const string Default = SameDay;

        public static IReadOnlyList<ApplicationOverdueOption> AllOptions { get; } =
        [
            new ApplicationOverdueOption(SameDay, "当天"),
            new ApplicationOverdueOption(Days7, "7天"),
            new ApplicationOverdueOption(Days30, "30天")
        ];
    }

    /// <summary>
    /// 申请单逾期设置下拉选项。
    /// </summary>
    public sealed class ApplicationOverdueOption
    {
        public ApplicationOverdueOption(string code, string label)
        {
            Code = code;
            Label = label;
        }

        public string Code { get; }

        public string Label { get; }
    }
}
