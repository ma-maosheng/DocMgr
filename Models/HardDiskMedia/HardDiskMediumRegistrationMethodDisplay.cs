namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘登记方式展示辅助。
    /// </summary>
    internal static class HardDiskMediumRegistrationMethodDisplay
    {
        internal static string Format(string? registrationMethod)
        {
            return string.IsNullOrWhiteSpace(registrationMethod)
                ? "(未登记)"
                : registrationMethod.Trim();
        }
    }
}
