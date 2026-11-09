using DocMgr.Models.NetworkTransfer;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Services.Projects;

public partial class ProjectService
{
    /// <inheritdoc />
    public IReadOnlySet<int> FindReferencedProjectIds(IReadOnlyList<ProjectInfo> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        if (projects.Count == 0)
        {
            return new HashSet<int>();
        }

        HashSet<int> referencedById = _dbContext.YearlyArchiveRegisterRecords
            .AsNoTracking()
            .Where(record => record.ProjectId != null)
            .Select(record => record.ProjectId!.Value)
            .Union(_dbContext.YearlyArchiveFilingFacts
                .Where(fact => fact.ProjectId != null)
                .Select(fact => fact.ProjectId!.Value))
            .Union(_dbContext.YearlyArchiveOutboundRecords
                .Where(record => record.ProjectId != null)
                .Select(record => record.ProjectId!.Value))
            .Union(_dbContext.YearlyArchiveReturnRecords
                .Where(record => record.ProjectId != null)
                .Select(record => record.ProjectId!.Value))
            .ToHashSet();

        HashSet<string> unboundRegisterNames = _dbContext.YearlyArchiveRegisterRecords
            .AsNoTracking()
            .Where(record => record.ProjectId == null)
            .Select(record => record.ProjectName)
            .Distinct()
            .AsEnumerable()
            .Select(name => name?.Trim() ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);

        HashSet<(string Name, string Year)> nameYearPairs = _dbContext.YearlyArchiveBoxes
            .AsNoTracking()
            .Select(box => new { box.ProjectName, box.Year })
            .Union(_dbContext.YearlyElectronicArchiveUnits.Select(unit => new { unit.ProjectName, unit.Year }))
            .Union(_dbContext.YearlyArchiveInventoryRegisterItems.Select(item => new { item.ProjectName, item.Year }))
            .Union(_dbContext.NetworkInboundRecords.Select(record => new { record.ProjectName, record.Year }))
            .Union(_dbContext.NetworkOutboundRecords.Select(record => new { record.ProjectName, record.Year }))
            .Union(_dbContext.NetworkOutboundItems.Select(item => new { item.ProjectName, item.Year }))
            .Union(_dbContext.NetworkOnNetAssets.Select(asset => new { asset.ProjectName, asset.Year }))
            .AsEnumerable()
            .Select(pair => (pair.ProjectName?.Trim() ?? string.Empty, pair.Year?.Trim() ?? string.Empty))
            .ToHashSet();

        var referenced = new HashSet<int>();
        foreach (ProjectInfo project in projects)
        {
            string name = project.ProjectName?.Trim() ?? string.Empty;
            string year = project.ImplementYear?.Trim() ?? string.Empty;
            if (referencedById.Contains(project.Id)
                || unboundRegisterNames.Contains(name)
                || nameYearPairs.Contains((name, year)))
            {
                referenced.Add(project.Id);
            }
        }

        return referenced;
    }
}
