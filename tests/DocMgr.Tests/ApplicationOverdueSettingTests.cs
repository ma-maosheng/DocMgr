using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Tests;

public class ApplicationOverdueSettingTests
{
    private static readonly DateTime ApplyDate = new(2026, 11, 1, 15, 30, 0);

    [Fact]
    public void 产品默认逾期是7天()
    {
        Assert.Equal(ApplicationOverdueDomainValues.Days7, ApplicationOverdueDomainValues.Default);
        Assert.Equal(6, ApplicationOverdueSettingSupport.GetAdminForceVoidWaitDays(ApplicationOverdueDomainValues.Default));
        Assert.Equal("7天", ApplicationOverdueSettingSupport.GetDisplayLabel(ApplicationOverdueDomainValues.Default));
        Assert.Equal("当天", ApplicationOverdueSettingSupport.GetDisplayLabel(null));
    }

    [Theory]
    [InlineData("Days7", 6)]
    [InlineData("  Days7  ", 6)]
    [InlineData("Days30", 29)]
    [InlineData("SameDay", 0)]
    [InlineData("不认识", 0)]
    [InlineData("", 0)]
    public void 等待天数按设置计算_无法识别时按当天(string setting, int waitDays)
    {
        Assert.Equal(waitDays, ApplicationOverdueSettingSupport.GetAdminForceVoidWaitDays(setting));
    }

    [Fact]
    public void 七天从申请日当天起算到第7个自然日()
    {
        Assert.Equal(new DateTime(2026, 11, 7), ApplicationOverdueSettingSupport.ResolveEligibleOn(ApplyDate, "Days7"));
        Assert.False(ApplicationOverdueSettingSupport.IsEligibleForAdminForceVoid(ApplyDate, "Days7", new DateTime(2026, 11, 6)));
        Assert.True(ApplicationOverdueSettingSupport.IsEligibleForAdminForceVoid(ApplyDate, "Days7", new DateTime(2026, 11, 7, 23, 0, 0)));
    }

    [Fact]
    public void 当天设置在申请日当天即可作废_前一天不行()
    {
        Assert.False(ApplicationOverdueSettingSupport.IsEligibleForAdminForceVoid(ApplyDate, "SameDay", new DateTime(2026, 10, 31)));
        Assert.True(ApplicationOverdueSettingSupport.IsEligibleForAdminForceVoid(ApplyDate, "SameDay", ApplyDate));
    }

    [Fact]
    public void 三十天在第30个自然日达到时限()
    {
        Assert.Equal(new DateTime(2026, 11, 30), ApplicationOverdueSettingSupport.ResolveEligibleOn(ApplyDate, "Days30"));
        Assert.Contains("30天", ApplicationOverdueSettingSupport.BuildNotEligibleMessage("Days30"));
    }

    [Fact]
    public void 登记申请日期优先用申请日_空则用创建日()
    {
        var withApplicantDate = new YearlyArchiveRegisterRecord
        {
            ApplicantDate = new DateTime(2026, 3, 2),
            CreatedDate = new DateTime(2026, 1, 1)
        };
        var withoutApplicantDate = new YearlyArchiveRegisterRecord
        {
            ApplicantDate = DateTime.MinValue,
            CreatedDate = new DateTime(2026, 1, 8)
        };

        Assert.Equal(new DateTime(2026, 3, 2), ApplicationOverdueSettingSupport.ResolveRegisterApplyDate(withApplicantDate));
        Assert.Equal(new DateTime(2026, 1, 8), ApplicationOverdueSettingSupport.ResolveRegisterApplyDate(withoutApplicantDate));
    }

    [Fact]
    public void 出库申请日期优先用提交时间()
    {
        var submitted = new YearlyArchiveOutboundRecord
        {
            SubmittedAt = new DateTime(2026, 4, 3, 9, 0, 0),
            ApplyDate = new DateTime(2026, 4, 1)
        };

        Assert.Equal(submitted.SubmittedAt, ApplicationOverdueSettingSupport.ResolveOutboundApplyDate(submitted));
    }
}
