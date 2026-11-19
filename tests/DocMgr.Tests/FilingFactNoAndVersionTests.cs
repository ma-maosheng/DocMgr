using DocMgr.Infrastructure;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Tests;

public class FilingFactNoAndVersionTests
{
    [Theory]
    [InlineData("立档-模拟-2024-0001", 2024)]
    [InlineData("  立档-电子-2026-12  ", 2026)]
    [InlineData("立档-模拟-1899-0001", null)]
    [InlineData("立档-模拟-2101-0001", null)]
    [InlineData("模拟-2024-0001", null)]
    [InlineData("", null)]
    public void 立档编号解析档案年度(string filingFactNo, int? year)
    {
        Assert.Equal(year, FilingFactNoSupport.TryParseArchiveYear(filingFactNo));
    }

    [Fact]
    public void 档案年度匹配模式带立档前缀()
    {
        Assert.Equal("立档-%-2026-%", FilingFactNoSupport.BuildArchiveYearLikePattern(2026));
    }

    [Theory]
    [InlineData("盘-出-申-2026-0001", 2026)]
    [InlineData("资-出-申-2025-0099", 2025)]
    [InlineData("盘-出-申-1999-0001", 0)]
    [InlineData("没有年度", 0)]
    public void 硬盘业务编号解析年度(string businessNo, int year)
    {
        bool parsed = HardDiskMediaApplicationNoSupport.TryParseBusinessNoYear(businessNo, out int actual);
        if (year == 0)
        {
            Assert.False(parsed);
            return;
        }

        Assert.True(parsed);
        Assert.Equal(year, actual);
    }

    [Theory]
    [InlineData("1.2", "1.2.0")]
    [InlineData("1.1.4", "1.1.4")]
    [InlineData("  1.1.5 其它说明 ", "1.1.5")]
    public void 安装包版本规范成三段(string text, string canonical)
    {
        Assert.True(AppReleaseVersionSupport.TryNormalize(text, out string actual));
        Assert.Equal(canonical, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("不是版本")]
    public void 无法解析的版本号返回失败(string? text)
    {
        Assert.False(AppReleaseVersionSupport.TryNormalize(text, out _));
    }

    [Fact]
    public void 只有发布版本更高才算需要升级()
    {
        Assert.True(AppReleaseVersionSupport.IsNewer("1.1.5", "1.1.4"));
        Assert.False(AppReleaseVersionSupport.IsNewer("1.1.4", "1.1.4"));
        Assert.False(AppReleaseVersionSupport.IsNewer("1.1.3", "1.1.4"));
        Assert.False(AppReleaseVersionSupport.IsNewer("坏版本", "1.1.4"));
    }
}
