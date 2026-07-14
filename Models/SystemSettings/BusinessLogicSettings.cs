namespace DocMgr.Models.SystemSettings
{
    /// <summary>
    /// 系统级业务逻辑设置（单例行，Id 固定为 1）。
    /// </summary>
    public class BusinessLogicSettings
    {
        public const int SingletonId = 1;

        public int Id { get; set; } = SingletonId;

        /// <summary>
        /// 申请单逾期设置，取值见 <see cref="ApplicationOverdueDomainValues"/>。
        /// </summary>
        public string ApplicationOverdueSetting { get; set; } = ApplicationOverdueDomainValues.Default;

        public DateTime UpdatedAt { get; set; }
    }
}
