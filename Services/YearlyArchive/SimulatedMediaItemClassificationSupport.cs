using System;
using System.Collections.Generic;
using System.Linq;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 模拟资料子项分类：载体类型、资料类型、所属子类、组织形式的映射、校验与展示格式。
    /// </summary>
    public static class SimulatedMediaItemClassificationSupport
    {
        public sealed record Classification(
            string MediaType,
            string MaterialCategory,
            string SubCategory,
            string OrganizationForm);

        /// <summary>
        /// 将历史复合介质类型拆成载体 + 子项分类（有损）。
        /// </summary>
        public static Classification MapLegacyMediaType(string? legacyMediaType)
        {
            string normalized = legacyMediaType?.Trim() ?? string.Empty;
            if (ArchiveRegisterDomainValues.IsSimulatedDataMediaType(normalized))
            {
                return new Classification(
                    normalized,
                    ArchiveRegisterDomainValues.SimulatedMaterialCategoryText,
                    ArchiveRegisterDomainValues.SimulatedSubCategoryOther,
                    ArchiveRegisterDomainValues.SimulatedOrganizationFormBound);
            }

            if (string.Equals(normalized, ArchiveRegisterDomainValues.LegacySimulatedMediaTypeBoundText, StringComparison.Ordinal))
            {
                return new Classification(
                    ArchiveRegisterDomainValues.SimulatedMediaTypePrintingPaper,
                    ArchiveRegisterDomainValues.SimulatedMaterialCategoryText,
                    ArchiveRegisterDomainValues.SimulatedSubCategoryOther,
                    ArchiveRegisterDomainValues.SimulatedOrganizationFormBound);
            }

            if (string.Equals(normalized, ArchiveRegisterDomainValues.LegacySimulatedMediaTypeLooseText, StringComparison.Ordinal))
            {
                return new Classification(
                    ArchiveRegisterDomainValues.SimulatedMediaTypePrintingPaper,
                    ArchiveRegisterDomainValues.SimulatedMaterialCategoryText,
                    ArchiveRegisterDomainValues.SimulatedSubCategoryOther,
                    ArchiveRegisterDomainValues.SimulatedOrganizationFormLoose);
            }

            if (string.Equals(normalized, ArchiveRegisterDomainValues.LegacySimulatedMediaTypeLooseMap, StringComparison.Ordinal)
                || string.Equals(normalized, ArchiveRegisterDomainValues.LegacySimulatedMediaTypeLargeMap, StringComparison.Ordinal))
            {
                return new Classification(
                    ArchiveRegisterDomainValues.SimulatedMediaTypeDrawingPaper,
                    ArchiveRegisterDomainValues.SimulatedMaterialCategoryMap,
                    ArchiveRegisterDomainValues.SimulatedSubCategoryOtherMap,
                    ArchiveRegisterDomainValues.SimulatedOrganizationFormLoose);
            }

            return new Classification(
                ArchiveRegisterDomainValues.SimulatedMediaTypePrintingPaper,
                ArchiveRegisterDomainValues.SimulatedMaterialCategoryText,
                ArchiveRegisterDomainValues.SimulatedSubCategoryOther,
                ArchiveRegisterDomainValues.SimulatedOrganizationFormLoose);
        }

        public static YearlyArchiveRegisterSimulatedMediaItemDetail CreateDetail(
            string? materialCategory,
            string? subCategory,
            string? organizationForm)
        {
            return new YearlyArchiveRegisterSimulatedMediaItemDetail
            {
                MaterialCategory = materialCategory?.Trim() ?? string.Empty,
                SubCategory = subCategory?.Trim() ?? string.Empty,
                OrganizationForm = organizationForm?.Trim() ?? string.Empty
            };
        }

        public static IReadOnlyList<string> CollectValidationErrors(
            YearlyArchiveRegisterMediaItem item,
            int mediaSequence,
            int itemSequence,
            ArchiveRegisterPageDomainOptions pageDomainOptions)
        {
            var errors = new List<string>();
            var prefix = $"• 第{mediaSequence}条模拟介质第{itemSequence}个子项";
            var detail = item.SimulatedDetail;

            if (string.IsNullOrWhiteSpace(item.ContentDesc))
            {
                errors.Add($"{prefix}【子项资料名称】未填写");
            }

            if (item.ContentCount < 1)
            {
                errors.Add($"{prefix}【份数】必须大于 0");
            }

            if (detail == null)
            {
                errors.Add($"{prefix}缺少模拟资料分类（资料类型、所属子类、组织形式）");
                return errors;
            }

            if (!ArchiveRegisterBusinessRules.IsAllowedDomainValue(
                    detail.MaterialCategory,
                    pageDomainOptions.SimulatedMaterialCategories.Count > 0
                        ? pageDomainOptions.SimulatedMaterialCategories
                        : ArchiveRegisterDomainValues.SimulatedMaterialCategories))
            {
                errors.Add($"{prefix}【资料类型】不在域值定义中");
            }

            var subOptions = string.Equals(
                    detail.MaterialCategory?.Trim(),
                    ArchiveRegisterDomainValues.SimulatedMaterialCategoryMap,
                    StringComparison.Ordinal)
                ? (pageDomainOptions.SimulatedMapSubCategories.Count > 0
                    ? pageDomainOptions.SimulatedMapSubCategories
                    : ArchiveRegisterDomainValues.SimulatedMapSubCategories)
                : (pageDomainOptions.SimulatedTextSubCategories.Count > 0
                    ? pageDomainOptions.SimulatedTextSubCategories
                    : ArchiveRegisterDomainValues.SimulatedTextSubCategories);

            if (!ArchiveRegisterBusinessRules.IsAllowedDomainValue(detail.SubCategory, subOptions))
            {
                errors.Add($"{prefix}【所属子类】不在「{detail.MaterialCategory}」对应域值中");
            }

            if (!ArchiveRegisterBusinessRules.IsAllowedDomainValue(
                    detail.OrganizationForm,
                    pageDomainOptions.SimulatedOrganizationForms.Count > 0
                        ? pageDomainOptions.SimulatedOrganizationForms
                        : ArchiveRegisterDomainValues.SimulatedOrganizationForms))
            {
                errors.Add($"{prefix}【组织形式】不在域值定义中（允许：散页、装订）");
            }

            return errors;
        }

        public static string FormatSummary(
            string? mediaKind,
            string? mediaType,
            string? materialCategory = null,
            string? subCategory = null,
            string? organizationForm = null)
        {
            var parts = new List<string>();
            AppendIfNotEmpty(parts, mediaKind);
            AppendIfNotEmpty(parts, mediaType);
            AppendIfNotEmpty(parts, materialCategory);
            AppendIfNotEmpty(parts, subCategory);
            AppendIfNotEmpty(parts, organizationForm);
            return string.Join("/", parts);
        }

        public static string FormatClassification(
            string? materialCategory,
            string? subCategory,
            string? organizationForm)
        {
            var parts = new List<string>();
            AppendIfNotEmpty(parts, materialCategory);
            AppendIfNotEmpty(parts, subCategory);
            AppendIfNotEmpty(parts, organizationForm);
            return string.Join("/", parts);
        }

        public static string FormatClassificationFromItem(YearlyArchiveRegisterMediaItem? item)
        {
            return FormatClassification(
                ResolveMaterialCategory(item),
                ResolveSubCategory(item),
                ResolveOrganizationFormDisplay(item));
        }

        public static IReadOnlyDictionary<int, string> MapClassificationByFilingFactId(
            IEnumerable<YearlyArchiveFilingFact> facts,
            IReadOnlyDictionary<int, YearlyArchiveRegisterMediaItem> mediaItemsById)
        {
            var result = new Dictionary<int, string>();
            foreach (YearlyArchiveFilingFact fact in facts)
            {
                if (fact.Id <= 0 || fact.MediaItemId <= 0)
                {
                    continue;
                }

                if (!mediaItemsById.TryGetValue(fact.MediaItemId, out YearlyArchiveRegisterMediaItem? item))
                {
                    continue;
                }

                string classification = FormatClassificationFromItem(item);
                if (!string.IsNullOrWhiteSpace(classification))
                {
                    result[fact.Id] = classification;
                }
            }

            return result;
        }

        public static string ResolveOrganizationFormDisplay(YearlyArchiveRegisterMediaItem? item)
        {
            string fromSimulated = item?.SimulatedDetail?.OrganizationForm?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(fromSimulated))
            {
                return fromSimulated;
            }

            return item?.ElectronicDetail?.DataOrganizationForm?.Trim() ?? string.Empty;
        }

        public static string ResolveMaterialCategory(YearlyArchiveRegisterMediaItem? item)
        {
            string fromSimulated = item?.SimulatedDetail?.MaterialCategory?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(fromSimulated))
            {
                return fromSimulated;
            }

            return item?.ElectronicDetail?.MaterialCategory?.Trim() ?? string.Empty;
        }

        public static string ResolveSubCategory(YearlyArchiveRegisterMediaItem? item)
        {
            string fromSimulated = item?.SimulatedDetail?.SubCategory?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(fromSimulated))
            {
                return fromSimulated;
            }

            return item?.ElectronicDetail?.SubCategory?.Trim() ?? string.Empty;
        }

        private static void AppendIfNotEmpty(List<string> parts, string? value)
        {
            string trimmed = value?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                parts.Add(trimmed);
            }
        }
    }
}
