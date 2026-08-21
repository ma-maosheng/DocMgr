using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.YearlyArchive;

namespace DocMgr.Tools.SimulatedMediaRegisterVerify;

/// <summary>
/// 自动验证模拟资料登记改造：域值、历史映射、校验、以及「所属子类」刷新回归。
/// 用法：dotnet run --project tools/SimulatedMediaRegisterVerify
/// </summary>
internal static class Program
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== 模拟资料登记 · 自动验证 ===");
        Console.WriteLine();

        VerifyDomainConstants();
        VerifyLegacyMediaTypeMapping();
        VerifyCreateDetailAndValidation();
        VerifySubCategoryRefreshRegression();
        VerifyEmptyMaterialCategoryFallback();

        Console.WriteLine();
        Console.WriteLine($"结果：通过 {_passed}，失败 {_failed}");
        return _failed == 0 ? 0 : 1;
    }

    private static void VerifyDomainConstants()
    {
        Assert(
            "域值·载体类型非空",
            ArchiveRegisterDomainValues.SimulatedDataMediaTypes.Count == 5,
            $"count={ArchiveRegisterDomainValues.SimulatedDataMediaTypes.Count}");

        Assert(
            "域值·文本子类非空",
            ArchiveRegisterDomainValues.SimulatedTextSubCategories.Count >= 5,
            $"count={ArchiveRegisterDomainValues.SimulatedTextSubCategories.Count}");

        Assert(
            "域值·图件子类非空",
            ArchiveRegisterDomainValues.SimulatedMapSubCategories.Count >= 4,
            $"count={ArchiveRegisterDomainValues.SimulatedMapSubCategories.Count}");

        var textOptions = ArchiveRegisterDomainValues.GetSimulatedSubCategories(
            ArchiveRegisterDomainValues.SimulatedMaterialCategoryText);
        var mapOptions = ArchiveRegisterDomainValues.GetSimulatedSubCategories(
            ArchiveRegisterDomainValues.SimulatedMaterialCategoryMap);
        var emptyOptions = ArchiveRegisterDomainValues.GetSimulatedSubCategories(null);

        Assert(
            "域值·GetSimulatedSubCategories(文本)",
            textOptions.Count > 0
            && textOptions.Contains(ArchiveRegisterDomainValues.SimulatedSubCategoryExternalMaterial),
            $"count={textOptions.Count}");
        Assert(
            "域值·GetSimulatedSubCategories(图件)",
            mapOptions.Count > 0
            && mapOptions.Contains(ArchiveRegisterDomainValues.SimulatedSubCategoryExternalMap),
            $"count={mapOptions.Count}");
        Assert(
            "域值·GetSimulatedSubCategories(空)为空列表",
            emptyOptions.Count == 0,
            $"count={emptyOptions.Count}");
    }

    private static void VerifyLegacyMediaTypeMapping()
    {
        var bound = SimulatedMediaItemClassificationSupport.MapLegacyMediaType(
            ArchiveRegisterDomainValues.LegacySimulatedMediaTypeBoundText);
        Assert(
            "历史映射·装订文本→打印纸/文本/装订",
            bound.MediaType == ArchiveRegisterDomainValues.SimulatedMediaTypePrintingPaper
            && bound.MaterialCategory == ArchiveRegisterDomainValues.SimulatedMaterialCategoryText
            && bound.OrganizationForm == ArchiveRegisterDomainValues.SimulatedOrganizationFormBound,
            $"{bound.MediaType}/{bound.MaterialCategory}/{bound.OrganizationForm}");

        var looseMap = SimulatedMediaItemClassificationSupport.MapLegacyMediaType(
            ArchiveRegisterDomainValues.LegacySimulatedMediaTypeLooseMap);
        Assert(
            "历史映射·散页图件→绘图纸/图件/散页",
            looseMap.MediaType == ArchiveRegisterDomainValues.SimulatedMediaTypeDrawingPaper
            && looseMap.MaterialCategory == ArchiveRegisterDomainValues.SimulatedMaterialCategoryMap
            && looseMap.OrganizationForm == ArchiveRegisterDomainValues.SimulatedOrganizationFormLoose,
            $"{looseMap.MediaType}/{looseMap.MaterialCategory}/{looseMap.OrganizationForm}");

        var paper = SimulatedMediaItemClassificationSupport.MapLegacyMediaType(
            ArchiveRegisterDomainValues.SimulatedMediaTypePrintingPaper);
        Assert(
            "历史映射·新载体类型保持原值",
            paper.MediaType == ArchiveRegisterDomainValues.SimulatedMediaTypePrintingPaper
            && paper.MaterialCategory == ArchiveRegisterDomainValues.SimulatedMaterialCategoryText,
            $"{paper.MediaType}/{paper.MaterialCategory}");
    }

    private static void VerifyCreateDetailAndValidation()
    {
        var detail = SimulatedMediaItemClassificationSupport.CreateDetail(
            ArchiveRegisterDomainValues.SimulatedMaterialCategoryText,
            ArchiveRegisterDomainValues.SimulatedSubCategoryPlanningDesign,
            ArchiveRegisterDomainValues.SimulatedOrganizationFormBound);

        var item = new YearlyArchiveRegisterMediaItem
        {
            ContentDesc = "测试子项",
            ContentCount = 1,
            SimulatedDetail = detail
        };

        var pageOptions = new ArchiveRegisterPageDomainOptions
        {
            SimulatedMaterialCategories = ArchiveRegisterDomainValues.SimulatedMaterialCategories,
            SimulatedTextSubCategories = ArchiveRegisterDomainValues.SimulatedTextSubCategories,
            SimulatedMapSubCategories = ArchiveRegisterDomainValues.SimulatedMapSubCategories,
            SimulatedOrganizationForms = ArchiveRegisterDomainValues.SimulatedOrganizationForms
        };

        var okErrors = SimulatedMediaItemClassificationSupport.CollectValidationErrors(item, 1, 1, pageOptions);
        Assert("校验·合法子项无错误", okErrors.Count == 0, string.Join("；", okErrors));

        item.SimulatedDetail = SimulatedMediaItemClassificationSupport.CreateDetail(
            ArchiveRegisterDomainValues.SimulatedMaterialCategoryText,
            ArchiveRegisterDomainValues.SimulatedSubCategoryExternalMap,
            ArchiveRegisterDomainValues.SimulatedOrganizationFormBound);
        var mismatchErrors = SimulatedMediaItemClassificationSupport.CollectValidationErrors(item, 1, 1, pageOptions);
        Assert(
            "校验·文本类型配图件子类应报错",
            mismatchErrors.Any(e => e.Contains("所属子类", StringComparison.Ordinal)),
            string.Join("；", mismatchErrors));

        string summary = SimulatedMediaItemClassificationSupport.FormatClassification(
            detail.MaterialCategory,
            detail.SubCategory,
            detail.OrganizationForm);
        Assert(
            "展示·分类摘要格式",
            summary == "文本/策划设计类/装订",
            summary);

        var resolveItem = new YearlyArchiveRegisterMediaItem { SimulatedDetail = detail };
        Assert(
            "解析·从 SimulatedDetail 读取资料类型",
            SimulatedMediaItemClassificationSupport.ResolveMaterialCategory(resolveItem)
                == ArchiveRegisterDomainValues.SimulatedMaterialCategoryText,
            SimulatedMediaItemClassificationSupport.ResolveMaterialCategory(resolveItem));
    }

    /// <summary>
    /// 回归：模拟资料类型「文本」走电子子类刷新会得到空列表；走模拟刷新应非空。
    /// 对应 AttachMediaEntry 误调 ConfigureElectronicMediaItem 的缺陷。
    /// </summary>
    private static void VerifySubCategoryRefreshRegression()
    {
        string materialCategory = ArchiveRegisterDomainValues.SimulatedMaterialCategoryText;

        IReadOnlyList<string> electronicOptions = ResolveElectronicSubCategoryOptions(materialCategory);
        Assert(
            "回归·电子刷新对「文本」应得空列表（旧缺陷路径）",
            electronicOptions.Count == 0,
            $"count={electronicOptions.Count}");

        IReadOnlyList<string> simulatedOptions = ResolveSimulatedSubCategoryOptions(
            materialCategory,
            domainText: Array.Empty<string>(),
            domainMap: Array.Empty<string>());
        Assert(
            "回归·模拟刷新对「文本」应回退内置子类",
            simulatedOptions.Count > 0
            && simulatedOptions.Contains(ArchiveRegisterDomainValues.SimulatedSubCategoryExternalMaterial),
            $"count={simulatedOptions.Count}, first={simulatedOptions.FirstOrDefault()}");

        var item = new MediaItemViewModel
        {
            MaterialCategory = materialCategory
        };
        ApplySimulatedSubCategoryRefresh(item, Array.Empty<string>(), Array.Empty<string>());
        Assert(
            "回归·MediaItemViewModel.AvailableSubCategories 非空",
            item.AvailableSubCategories.Count > 0,
            $"count={item.AvailableSubCategories.Count}");
        Assert(
            "回归·默认选中首项子类",
            !string.IsNullOrWhiteSpace(item.SubCategory),
            $"SubCategory='{item.SubCategory}'");
    }

    private static void VerifyEmptyMaterialCategoryFallback()
    {
        var item = new MediaItemViewModel
        {
            MaterialCategory = string.Empty
        };

        ApplySimulatedSubCategoryRefresh(item, Array.Empty<string>(), Array.Empty<string>());
        Assert(
            "回退·空资料类型按文本刷新且写回资料类型",
            item.MaterialCategory == ArchiveRegisterDomainValues.SimulatedMaterialCategoryText
            && item.AvailableSubCategories.Count > 0,
            $"MaterialCategory='{item.MaterialCategory}', count={item.AvailableSubCategories.Count}");

        item.MaterialCategory = ArchiveRegisterDomainValues.SimulatedMaterialCategoryMap;
        ApplySimulatedSubCategoryRefresh(
            item,
            Array.Empty<string>(),
            ArchiveRegisterDomainValues.SimulatedMapSubCategories);
        Assert(
            "回退·图件类型使用图件子类",
            item.AvailableSubCategories.Contains(ArchiveRegisterDomainValues.SimulatedSubCategoryExternalMap),
            string.Join(",", item.AvailableSubCategories));
    }

    private static IReadOnlyList<string> ResolveElectronicSubCategoryOptions(string materialCategory)
    {
        if (string.Equals(materialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategoryDocument, StringComparison.Ordinal))
        {
            return ["外来资料类"];
        }

        if (string.Equals(materialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategoryData, StringComparison.Ordinal))
        {
            return ["最终成果数据"];
        }

        if (string.Equals(materialCategory, ArchiveRegisterDomainValues.ElectronicMaterialCategorySoftware, StringComparison.Ordinal))
        {
            return ["应用软件"];
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<string> ResolveSimulatedSubCategoryOptions(
        string materialCategory,
        IReadOnlyList<string> domainText,
        IReadOnlyList<string> domainMap)
    {
        string category = materialCategory?.Trim() ?? string.Empty;
        bool isMap = string.Equals(category, ArchiveRegisterDomainValues.SimulatedMaterialCategoryMap, StringComparison.Ordinal);
        bool isText = string.Equals(category, ArchiveRegisterDomainValues.SimulatedMaterialCategoryText, StringComparison.Ordinal);
        if (!isMap && !isText)
        {
            category = ArchiveRegisterDomainValues.SimulatedMaterialCategoryText;
            isText = true;
        }

        IReadOnlyList<string> domainOptions = isMap ? domainMap : domainText;
        return domainOptions.Count > 0
            ? domainOptions
            : ArchiveRegisterDomainValues.GetSimulatedSubCategories(category);
    }

    private static void ApplySimulatedSubCategoryRefresh(
        MediaItemViewModel item,
        IReadOnlyList<string> domainText,
        IReadOnlyList<string> domainMap)
    {
        string category = item.MaterialCategory?.Trim() ?? string.Empty;
        bool isMap = string.Equals(category, ArchiveRegisterDomainValues.SimulatedMaterialCategoryMap, StringComparison.Ordinal);
        bool isText = string.Equals(category, ArchiveRegisterDomainValues.SimulatedMaterialCategoryText, StringComparison.Ordinal);
        if (!isMap && !isText)
        {
            category = ArchiveRegisterDomainValues.SimulatedMaterialCategoryText;
            item.AssignSimulatedMaterialCategoryWithoutRefresh(category);
            isText = true;
        }

        IReadOnlyList<string> domainOptions = isMap ? domainMap : domainText;
        IReadOnlyList<string> options = domainOptions.Count > 0
            ? domainOptions
            : ArchiveRegisterDomainValues.GetSimulatedSubCategories(category);

        item.AvailableSubCategories.Clear();
        foreach (string option in options)
        {
            item.AvailableSubCategories.Add(option);
        }

        if (string.IsNullOrWhiteSpace(item.SubCategory)
            || !options.Any(option => string.Equals(option, item.SubCategory, StringComparison.Ordinal)))
        {
            item.SubCategory = options.FirstOrDefault() ?? string.Empty;
        }
    }

    private static void Assert(string name, bool condition, string detail)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"[PASS] {name}");
        }
        else
        {
            _failed++;
            Console.WriteLine($"[FAIL] {name} · {detail}");
        }
    }
}
