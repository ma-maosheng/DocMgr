using System;
using System.Collections.Generic;
using System.Linq;
using DocMgr.Data;
using DocMgr.Models.ArchiveContainers;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.YearlyArchive
{
    /// <summary>
    /// 电子介质袋上的项目摘要。项目挂在立档事实和介质上，不挂在袋子上。
    /// </summary>
    public sealed class ElectronicArchiveProjectSummary
    {
        public string ProjectNames { get; init; } = string.Empty;

        public string ImplementYears { get; init; } = string.Empty;
    }

    /// <summary>
    /// 按立档事实识别电子介质上的项目。有项目编号时用编号，否则用实施年度加项目名称。
    /// </summary>
    internal static class ElectronicArchiveProjectLookup
    {
        public static IQueryable<int> QueryContainerIds(
            AppDbContext db,
            int? projectId,
            string? projectName,
            string? implementYear)
        {
            ArgumentNullException.ThrowIfNull(db);
            string name = projectName?.Trim() ?? string.Empty;
            string year = implementYear?.Trim() ?? string.Empty;
            bool hasId = projectId is > 0;
            bool hasName = !string.IsNullOrWhiteSpace(name);
            bool hasYear = !string.IsNullOrWhiteSpace(year);

            return db.YearlyArchiveFilingFacts
                .AsNoTracking()
                .Where(fact => fact.ContainerKind == ArchiveContainerKind.ElectronicBag && fact.ContainerId > 0)
                .Where(fact =>
                    (hasId && fact.ProjectId == projectId)
                    || (!hasId && hasName && hasYear && fact.ProjectId != null && db.ProjectInfos.Any(project =>
                        project.Id == fact.ProjectId
                        && project.ProjectName == name
                        && project.ImplementYear == year))
                    || (!hasId && hasName && !hasYear && fact.ProjectName == name)
                    || (!hasId && hasName && fact.ProjectId == null && fact.ProjectName == name && hasYear))
                .Select(fact => fact.ContainerId)
                .Distinct();
        }

        public static Dictionary<int, ElectronicArchiveProjectSummary> LoadByContainerIds(
            AppDbContext db,
            IReadOnlyCollection<int> unitIds)
        {
            ArgumentNullException.ThrowIfNull(db);
            if (unitIds == null || unitIds.Count == 0)
            {
                return new Dictionary<int, ElectronicArchiveProjectSummary>();
            }

            var rows = (
                from fact in db.YearlyArchiveFilingFacts.AsNoTracking()
                where fact.ContainerKind == ArchiveContainerKind.ElectronicBag
                      && unitIds.Contains(fact.ContainerId)
                join project in db.ProjectInfos.AsNoTracking()
                    on fact.ProjectId equals project.Id into projects
                from project in projects.DefaultIfEmpty()
                select new
                {
                    fact.ContainerId,
                    Name = fact.ProjectName,
                    Year = project != null ? project.ImplementYear : string.Empty
                }).ToList();

            return rows
                .GroupBy(row => row.ContainerId)
                .ToDictionary(group => group.Key, group => BuildSummary(group.Select(row => (row.Year, row.Name))));
        }

        private static ElectronicArchiveProjectSummary BuildSummary(IEnumerable<(string? Year, string? Name)> rows)
        {
            var pairs = rows
                .Select(row => (Year: row.Year?.Trim() ?? string.Empty, Name: row.Name?.Trim() ?? string.Empty))
                .Where(row => !string.IsNullOrWhiteSpace(row.Name))
                .Distinct()
                .OrderBy(row => row.Year, StringComparer.Ordinal)
                .ThenBy(row => row.Name, StringComparer.Ordinal)
                .ToList();

            return new ElectronicArchiveProjectSummary
            {
                ProjectNames = string.Join("、", pairs.Select(row => row.Name).Distinct(StringComparer.Ordinal)),
                ImplementYears = string.Join("、", pairs.Select(row => row.Year).Where(year => year.Length > 0).Distinct(StringComparer.Ordinal))
            };
        }
    }
}
