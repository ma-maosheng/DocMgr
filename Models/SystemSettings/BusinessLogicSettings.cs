namespace DocMgr.Models.SystemSettings
{
    /// <summary>
    /// 系统级业务逻辑设置（单例行，Id 固定为 1）。
    /// </summary>
    public class BusinessLogicSettings
    {
        public const int SingletonId = 1;

        /// <summary>
        /// 主键。新建时保持 0，由数据库 IDENTITY 生成（首行即为 1）；勿在客户端写死显式 Id。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 申请单逾期设置，取值见 <see cref="ApplicationOverdueDomainValues"/>。
        /// </summary>
        public string ApplicationOverdueSetting { get; set; } = ApplicationOverdueDomainValues.Default;

        public DateTime UpdatedAt { get; set; }
    }
}
