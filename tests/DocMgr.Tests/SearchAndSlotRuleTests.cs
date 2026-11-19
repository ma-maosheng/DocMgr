using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Tests;

public class SearchAndSlotRuleTests
{
    [Theory]
    [InlineData(null, "%")]
    [InlineData("  ", "%")]
    [InlineData("报告", "%报告%")]
    [InlineData("*报告", "%报告")]
    [InlineData("a?b", "a_b")]
    [InlineData(@"100%", @"%100\%%")]
    [InlineData(@"\*", @"%\*%")]
    public void 检索通配符转成SqlLike(string? input, string expected)
    {
        Assert.Equal(expected, SearchWildcardPatternSupport.ToSqlLikePattern(input));
    }

    [Fact]
    public void 转义后的星号不算通配符()
    {
        Assert.False(SearchWildcardPatternSupport.ContainsWildcard(@"\*"));
        Assert.True(SearchWildcardPatternSupport.ContainsWildcard("报告*"));
    }

    [Fact]
    public void 同一档口不同盒序号归成同一个档口键()
    {
        Assert.Equal("资料柜A-1-2", ArchiveSlotLocationSupport.BuildSlotKey("资料柜A-1-2-03"));
        Assert.True(ArchiveSlotLocationSupport.IsSameSlot("资料柜A-1-2-03", "资料柜a-1-2-15"));
        Assert.False(ArchiveSlotLocationSupport.IsSameSlot("资料柜A-1-2-03", "资料柜A-1-3-03"));
    }

    [Fact]
    public void 完整位置规范为面大写且序号两位()
    {
        Assert.Equal("资料柜A-1-2-03", ArchiveSlotLocationSupport.NormalizeFullLocationCode("资料柜a-1-2-3"));
        Assert.Equal(string.Empty, ArchiveSlotLocationSupport.NormalizeFullLocationCode("资料室"));
    }

    [Fact]
    public void 档口序号取最小空位_用尽时抛出()
    {
        Assert.Equal(2, ArchiveSlotLocationSupport.ResolveMinimumAvailableSequence([1, 3]));

        var full = Enumerable.Range(1, 99);
        var error = Assert.Throws<InvalidOperationException>(() => ArchiveSlotLocationSupport.ResolveMinimumAvailableSequence(full));
        Assert.Contains("序号已用尽", error.Message);
    }

    [Fact]
    public void 审批规则无条件时全部命中_双条件按与或()
    {
        var values = new Dictionary<string, string>
        {
            ["密级"] = "内部",
            ["介质"] = "电子"
        };

        Assert.True(ApprovalWorkflowMatchingSupport.IsRuleMatch(new ApprovalWorkflowRule(), values));

        var andRule = new ApprovalWorkflowRule
        {
            ConditionLogic = ApprovalWorkflowDomainValues.ConditionLogicAnd,
            Condition1FieldKey = "密级",
            Condition1Value = "内部",
            Condition2FieldKey = "介质",
            Condition2Value = "模拟"
        };
        Assert.False(ApprovalWorkflowMatchingSupport.IsRuleMatch(andRule, values));

        andRule.ConditionLogic = ApprovalWorkflowDomainValues.ConditionLogicOr;
        Assert.True(ApprovalWorkflowMatchingSupport.IsRuleMatch(andRule, values));
    }
}
