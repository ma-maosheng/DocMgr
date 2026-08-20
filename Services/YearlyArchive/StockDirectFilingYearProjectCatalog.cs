using System;
using System.Collections.Generic;
using System.Linq;
using DocMgr.Data;
using DocMgr.Models.Projects;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 存量/存档直办共用：实施年度与项目名称开放域选项
    /// （项目信息 ∪ 模拟盒 ∪ 电子袋）。
    /// </summary>
    public interface IStockDirectFilingYearProjectCatalog
    {
        /// <summary>
        /// 已登记实施年度并集（四位年份，升序）。
        /// </summary>
        IReadOnlyList<string> ListRegisteredYears();

        /// <summary>
        /// 指定年度下已登记项目名称并集（升序）。
        /// </summary>
        IReadOnlyList<string> ListRegisteredProjectNames(string year);

        /// <summary>
        /// 指定年度下项目明细：优先 <see cref="ProjectInfo"/>；
        /// 仅出现在模拟盒/电子袋中的名称以占位项返回（Id=0）。
        /// </summary>
        IReadOnlyList<ProjectInfo> ListRegisteredProjects(string year);
    }

    /// <inheritdoc/>
    public sealed class StockDirectFilingYearProjectCatalog : IStockDirectFilingYearProjectCatalog
    {
        private readonly AppDbContext _dbContext;

        public StockDirectFilingYearProjectCatalog(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> ListRegisteredYears()
        {
            var years = new HashSet<string>(StringComparer.Ordinal);

            foreach (string? year in _dbContext.ProjectInfos.AsNoTracking()
                         .Select(item => item.ImplementYear))
            {
                TryAddYear(years, year);
            }

            foreach (string? year in _dbContext.YearlyArchiveBoxes.AsNoTracking()
                         .Select(item => item.Year))
            {
                TryAddYear(years, year);
            }

            foreach (string? year in _dbContext.YearlyElectronicArchiveUnits.AsNoTracking()
                         .Select(item => item.Year))
            {
                TryAddYear(years, year);
            }

            return years
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToList();
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> ListRegisteredProjectNames(string year)
        {
            string normalizedYear = year?.Trim() ?? string.Empty;
            if (!IsFourDigitYear(normalizedYear))
            {
                return Array.Empty<string>();
            }

            return ListRegisteredProjects(normalizedYear)
                .Select(item => item.ProjectName?.Trim() ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }

        /// <inheritdoc/>
        public IReadOnlyList<ProjectInfo> ListRegisteredProjects(string year)
        {
            string normalizedYear = year?.Trim() ?? string.Empty;
            if (!IsFourDigitYear(normalizedYear))
            {
                return Array.Empty<ProjectInfo>();
            }

            var fromProjects = _dbContext.ProjectInfos.AsNoTracking()
                .Where(item => item.ImplementYear == normalizedYear)
                .ToList()
                .Where(item => string.Equals(item.ImplementYear?.Trim(), normalizedYear, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(item.ProjectName))
                .GroupBy(item => item.ProjectName.Trim(), StringComparer.Ordinal)
                .Select(group => group
                    .OrderByDescending(item => item.Id)
                    .First())
                .ToDictionary(item => item.ProjectName.Trim(), item => item, StringComparer.Ordinal);

            var namesFromContainers = new HashSet<string>(StringComparer.Ordinal);

            foreach (string? name in _dbContext.YearlyArchiveBoxes.AsNoTracking()
                         .Where(item => item.Year == normalizedYear)
                         .Select(item => item.ProjectName))
            {
                string trimmed = name?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    namesFromContainers.Add(trimmed);
                }
            }

            foreach (string? name in _dbContext.YearlyElectronicArchiveUnits.AsNoTracking()
                         .Where(item => item.Year == normalizedYear)
                         .Select(item => item.ProjectName))
            {
                string trimmed = name?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    namesFromContainers.Add(trimmed);
                }
            }

            foreach (string name in namesFromContainers)
            {
                if (fromProjects.ContainsKey(name))
                {
                    continue;
                }

                fromProjects[name] = new ProjectInfo
                {
                    Id = 0,
                    ProjectName = name,
                    ProjectCode = string.Empty,
                    ImplementYear = normalizedYear,
                    Remark = "来自立档容器（模拟盒/电子袋）"
                };
            }

            return fromProjects.Values
                .OrderBy(item => item.ProjectName, StringComparer.Ordinal)
                .ThenBy(item => item.ProjectCode, StringComparer.Ordinal)
                .ToList();
        }

        private static void TryAddYear(ISet<string> years, string? year)
        {
            string trimmed = year?.Trim() ?? string.Empty;
            if (IsFourDigitYear(trimmed))
            {
                years.Add(trimmed);
            }
        }

        private static bool IsFourDigitYear(string year)
            => year.Length == 4 && year.All(char.IsDigit);
    }
}
