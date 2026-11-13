using DocMgr.Models.ArchiveContainers;
using DocMgr.Repositories.YearlyArchive;
using DocMgr.Models.NetworkTransfer;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Services.Projects;

public partial class ProjectService
{
    /// <summary>
    /// 同一年度另有同名项目时，只同步带项目编号的记录，并返回给界面的说明。
    /// </summary>
    private const string AmbiguousNameOnlyWarning =
        "同一实施年度还有另一条同名项目。已同步建档申请、立档事实、出库单、归还单，以及能通过这些单据确定所属项目的档案盒、电子袋和盘库明细。"
        + "只记下项目名称、无法区分归属的档案盒、电子袋和出入网记录本次没有修改。";

    /// <summary>
    /// 将项目名称、实施年度写回业务副本。档案盒号、电子袋号中的年度不改。
    /// </summary>
    private string? SyncProjectSnapshots(
        int projectId,
        string oldName,
        string oldYear,
        string newName,
        string newYear,
        bool nameChanged,
        bool yearChanged)
    {
        bool ambiguous = HasAnotherProjectWithNameAndYear(projectId, oldName, oldYear);
        List<YearlyArchiveFilingFact> facts = _dbContext.YearlyArchiveFilingFacts
            .Where(fact => fact.ProjectId == projectId)
            .ToList();
        List<int> factIds = facts.Select(fact => fact.Id).ToList();

        if (nameChanged)
        {
            AssignProjectName(_dbContext.YearlyArchiveRegisterRecords.Where(record => record.ProjectId == projectId), newName);
            foreach (YearlyArchiveFilingFact fact in facts)
            {
                AssignText(fact.ProjectName, newName, value => fact.ProjectName = value);
                AssignText(
                    fact.FilingStoragePath,
                    ProjectSnapshotPathSupport.ReplaceProjectSegment(fact.FilingStoragePath, oldName, newName, oldYear),
                    value => fact.FilingStoragePath = value);
            }

            AssignProjectName(_dbContext.YearlyArchiveOutboundRecords.Where(record => record.ProjectId == projectId), newName);
            AssignProjectName(_dbContext.YearlyArchiveReturnRecords.Where(record => record.ProjectId == projectId), newName);
            SyncOutboundItemProjectNames(projectId, factIds, newName);
            SyncRegisterMediaStoragePaths(projectId, oldName, newName, oldYear);
        }

        if (nameChanged)
        {
            HashSet<int> boxIds = CollectLinkedBoxIds(projectId, facts);
            HashSet<int> unitIds = CollectLinkedUnitIds(projectId, facts);
            if (!ambiguous)
            {
                boxIds.UnionWith(FindBoxIdsByNameAndYear(oldName, oldYear));
                unitIds.UnionWith(FindUnitIdsByNameAndYear(oldName, oldYear));
            }

            SyncBoxes(boxIds, newName);
            SyncUnits(unitIds, oldName, newName, oldYear);
            SyncUnitFilingPaths(unitIds, oldName, newName, oldYear);
            SyncElectronicInventoryProjectNames(unitIds, newName);
        }

        SyncSimulatedInventorySnapshots(factIds, newName, newYear, nameChanged, yearChanged);

        bool skippedNameOnly = false;
        if (!ambiguous)
        {
            SyncNetworkSnapshots(oldName, oldYear, newName, newYear, nameChanged, yearChanged);
        }
        else if ((nameChanged && HasNameOnlySnapshots(oldName, oldYear))
            || (yearChanged && HasNetworkSnapshots(oldName, oldYear)))
        {
            skippedNameOnly = true;
        }

        return skippedNameOnly ? AmbiguousNameOnlyWarning : null;
    }

    private void EnsureUniqueNameAndYear(int projectId, string name, string year)
    {
        if (HasAnotherProjectWithNameAndYear(projectId, name, year))
        {
            throw new InvalidOperationException($"实施年度 {year} 已有项目「{name}」，不能重复保存。");
        }
    }

    private bool HasAnotherProjectWithNameAndYear(int projectId, string name, string year)
    {
        return _dbContext.ProjectInfos
            .AsNoTracking()
            .Where(item => item.Id != projectId)
            .AsEnumerable()
            .Any(item => string.Equals(item.ProjectName?.Trim(), name, StringComparison.Ordinal)
                && string.Equals(item.ImplementYear?.Trim(), year, StringComparison.Ordinal));
    }

    private void EnsureProjectUnused(ProjectInfo project)
    {
        string name = project.ProjectName?.Trim() ?? string.Empty;
        string year = project.ImplementYear?.Trim() ?? string.Empty;
        var parts = new List<string>();
        AddUsageCount(parts, CountRegisterRecords(project.Id, name), "建档申请");
        AddUsageCount(parts, _dbContext.YearlyArchiveFilingFacts.Count(fact => fact.ProjectId == project.Id), "立档事实");
        AddUsageCount(parts, _dbContext.YearlyArchiveOutboundRecords.Count(record => record.ProjectId == project.Id), "资料出库单");
        AddUsageCount(parts, _dbContext.YearlyArchiveReturnRecords.Count(record => record.ProjectId == project.Id), "资料归还单");
        AddUsageCount(parts, CollectLinkedBoxIds(project.Id, LoadFacts(project.Id)).Union(FindBoxIdsByNameAndYear(name, year)).Count(), "档案盒");
        AddUsageCount(parts, CollectLinkedUnitIds(project.Id, LoadFacts(project.Id)).Union(FindUnitIdsByNameAndYear(name, year)).Count(), "电子档案袋");
        AddUsageCount(parts, CountInventoryItems(project.Id, name, year), "盘库登记明细");
        AddUsageCount(parts, _dbContext.NetworkInboundRecords.Count(record => record.ProjectName == name && record.Year == year), "入网申请");
        AddUsageCount(parts, _dbContext.NetworkOutboundRecords.Count(record => record.ProjectName == name && record.Year == year), "出网申请");
        AddUsageCount(parts, _dbContext.NetworkOutboundItems.Count(item => item.ProjectName == name && item.Year == year), "出网明细");
        AddUsageCount(parts, _dbContext.NetworkOnNetAssets.Count(asset => asset.ProjectName == name && asset.Year == year), "在网台账");

        if (parts.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"项目「{name}」（{year}）已被使用，不能删除：{string.Join("，", parts)}。");
    }

    private List<YearlyArchiveFilingFact> LoadFacts(int projectId)
    {
        return _dbContext.YearlyArchiveFilingFacts
            .AsNoTracking()
            .Where(fact => fact.ProjectId == projectId)
            .ToList();
    }

    private int CountRegisterRecords(int projectId, string name)
    {
        return _dbContext.YearlyArchiveRegisterRecords.Count(record =>
            record.ProjectId == projectId
            || (record.ProjectId == null && record.ProjectName == name));
    }

    private int CountInventoryItems(int projectId, string name, string year)
    {
        return _dbContext.YearlyArchiveInventoryRegisterItems.Count(item =>
            (item.ProjectName == name && item.Year == year)
            || _dbContext.YearlyArchiveFilingFacts.Any(fact => fact.Id == item.FilingFactId && fact.ProjectId == projectId));
    }

    private static void AddUsageCount(List<string> parts, int count, string label)
    {
        if (count > 0)
        {
            parts.Add($"{label} {count} 条");
        }
    }

    private void SyncOutboundItemProjectNames(int projectId, IReadOnlyCollection<int> factIds, string newName)
    {
        List<YearlyArchiveOutboundItem> items = _dbContext.YearlyArchiveOutboundItems
            .Where(item =>
                _dbContext.YearlyArchiveOutboundRecords.Any(record =>
                    record.Id == item.OutboundRecordId && record.ProjectId == projectId)
                || (item.FilingFactId > 0 && factIds.Contains(item.FilingFactId)))
            .ToList();
        foreach (YearlyArchiveOutboundItem item in items)
        {
            AssignText(item.ItemProjectName, newName, value => item.ItemProjectName = value);
        }
    }

    private void SyncRegisterMediaStoragePaths(int projectId, string oldName, string newName, string oldYear)
    {
        List<YearlyArchiveRegisterMediaItem> items = (
            from item in _dbContext.YearlyArchiveRegisterMediaItems
            join media in _dbContext.YearlyArchiveRegisterMedias on item.YearlyArchiveRegisterMediaId equals media.Id
            join record in _dbContext.YearlyArchiveRegisterRecords on media.YearlyArchiveRegisterRecordId equals record.Id
            where record.ProjectId == projectId
            select item).ToList();
        foreach (YearlyArchiveRegisterMediaItem item in items)
        {
            AssignText(
                item.StoragePath,
                ProjectSnapshotPathSupport.ReplaceProjectSegment(item.StoragePath, oldName, newName, oldYear),
                value => item.StoragePath = value);
        }
    }

    private HashSet<int> CollectLinkedBoxIds(int projectId, IEnumerable<YearlyArchiveFilingFact> facts)
    {
        var ids = new HashSet<int>(
            facts.Where(fact => fact.ContainerKind == ArchiveContainerKind.ArchiveBox && fact.ContainerId > 0)
                .Select(fact => fact.ContainerId));
        List<int> linkedIds = (
            from link in _dbContext.YearlyArchiveBoxMediaItemLinks
            join item in _dbContext.YearlyArchiveRegisterMediaItems on link.YearlyArchiveRegisterMediaItemId equals item.Id
            join media in _dbContext.YearlyArchiveRegisterMedias on item.YearlyArchiveRegisterMediaId equals media.Id
            join record in _dbContext.YearlyArchiveRegisterRecords on media.YearlyArchiveRegisterRecordId equals record.Id
            where record.ProjectId == projectId
            select link.YearlyArchiveBoxId).ToList();
        ids.UnionWith(linkedIds);
        return ids;
    }

    private HashSet<int> CollectLinkedUnitIds(int projectId, IEnumerable<YearlyArchiveFilingFact> facts)
    {
        var ids = new HashSet<int>(
            facts.Where(fact => fact.ContainerKind == ArchiveContainerKind.ElectronicBag && fact.ContainerId > 0)
                .Select(fact => fact.ContainerId));
        List<int> linkedIds = (
            from link in _dbContext.YearlyElectronicArchiveUnitMediaItemLinks
            join item in _dbContext.YearlyArchiveRegisterMediaItems on link.YearlyArchiveRegisterMediaItemId equals item.Id
            join media in _dbContext.YearlyArchiveRegisterMedias on item.YearlyArchiveRegisterMediaId equals media.Id
            join record in _dbContext.YearlyArchiveRegisterRecords on media.YearlyArchiveRegisterRecordId equals record.Id
            where record.ProjectId == projectId
            select link.HardDiskMediumId != null
                ? _dbContext.YearlyElectronicArchiveUnitMediumLinks
                    .Where(mediumLink => mediumLink.HardDiskMediumId == link.HardDiskMediumId)
                    .Select(mediumLink => mediumLink.YearlyElectronicArchiveUnitId)
                    .FirstOrDefault()
                : _dbContext.YearlyElectronicArchiveUnitDiscLinks
                    .Where(discLink => discLink.OpticalDiscMediumId == link.OpticalDiscMediumId)
                    .Select(discLink => discLink.YearlyElectronicArchiveUnitId)
                    .FirstOrDefault()).ToList();
        ids.UnionWith(linkedIds);
        return ids;
    }

    private List<int> FindBoxIdsByNameAndYear(string name, string year)
    {
        return _dbContext.YearlyArchiveBoxes
            .Where(box => box.ProjectName == name && box.Year == year)
            .Select(box => box.Id)
            .ToList();
    }

    private List<int> FindUnitIdsByNameAndYear(string name, string year)
    {
        return ElectronicArchiveProjectLookup.QueryContainerIds(_dbContext, null, name, year).ToList();
    }

    private void SyncBoxes(IReadOnlyCollection<int> boxIds, string newName)
    {
        if (boxIds.Count == 0)
        {
            return;
        }

        List<YearlyArchiveBox> boxes = _dbContext.YearlyArchiveBoxes
            .Where(box => boxIds.Contains(box.Id))
            .ToList();
        foreach (YearlyArchiveBox box in boxes)
        {
            AssignText(box.ProjectName, newName, value => box.ProjectName = value);
        }
    }

    private void SyncUnits(IReadOnlyCollection<int> unitIds, string oldName, string newName, string oldYear)
    {
        if (unitIds.Count == 0)
        {
            return;
        }

        List<YearlyElectronicArchiveUnit> units = _dbContext.YearlyElectronicArchiveUnits
            .Where(unit => unitIds.Contains(unit.Id))
            .ToList();
        foreach (YearlyElectronicArchiveUnit unit in units)
        {
            AssignText(
                unit.StoragePath,
                ProjectSnapshotPathSupport.ReplaceProjectSegment(unit.StoragePath, oldName, newName, oldYear),
                value => unit.StoragePath = value);
        }
    }

    private void SyncUnitFilingPaths(IReadOnlyCollection<int> unitIds, string oldName, string newName, string oldYear)
    {
        if (unitIds.Count == 0)
        {
            return;
        }

        List<YearlyElectronicArchiveUnitMediaItemLink> links = _dbContext.YearlyElectronicArchiveUnitMediaItemLinks
            .Where(link =>
                (link.HardDiskMediumId != null && _dbContext.YearlyElectronicArchiveUnitMediumLinks.Any(mediumLink =>
                    unitIds.Contains(mediumLink.YearlyElectronicArchiveUnitId)
                    && mediumLink.HardDiskMediumId == link.HardDiskMediumId))
                || (link.OpticalDiscMediumId != null && _dbContext.YearlyElectronicArchiveUnitDiscLinks.Any(discLink =>
                    unitIds.Contains(discLink.YearlyElectronicArchiveUnitId)
                    && discLink.OpticalDiscMediumId == link.OpticalDiscMediumId)))
            .ToList();
        foreach (YearlyElectronicArchiveUnitMediaItemLink link in links)
        {
            AssignText(
                link.FilingStoragePath,
                ProjectSnapshotPathSupport.ReplaceProjectSegment(link.FilingStoragePath, oldName, newName, oldYear),
                value => link.FilingStoragePath = value);
        }
    }

    private void SyncElectronicInventoryProjectNames(IReadOnlyCollection<int> unitIds, string newName)
    {
        if (unitIds.Count == 0)
        {
            return;
        }

        List<YearlyArchiveInventoryRegisterItem> items = _dbContext.YearlyArchiveInventoryRegisterItems
            .Where(item => item.ElectronicArchiveUnitId > 0 && unitIds.Contains(item.ElectronicArchiveUnitId))
            .ToList();
        foreach (YearlyArchiveInventoryRegisterItem item in items)
        {
            AssignText(item.ProjectName, newName, value => item.ProjectName = value);
        }
    }

    private void SyncSimulatedInventorySnapshots(
        IReadOnlyCollection<int> factIds,
        string newName,
        string newYear,
        bool nameChanged,
        bool yearChanged)
    {
        if (factIds.Count == 0 || (!nameChanged && !yearChanged))
        {
            return;
        }

        List<YearlyArchiveInventoryRegisterItem> items = _dbContext.YearlyArchiveInventoryRegisterItems
            .Where(item => item.FilingFactId > 0 && factIds.Contains(item.FilingFactId))
            .ToList();
        foreach (YearlyArchiveInventoryRegisterItem item in items)
        {
            if (nameChanged)
            {
                AssignText(item.ProjectName, newName, value => item.ProjectName = value);
            }

            if (yearChanged)
            {
                AssignText(item.Year, newYear, value => item.Year = value);
            }
        }
    }

    private void SyncNetworkSnapshots(
        string oldName,
        string oldYear,
        string newName,
        string newYear,
        bool nameChanged,
        bool yearChanged)
    {
        foreach (NetworkInboundRecord record in _dbContext.NetworkInboundRecords
                     .Where(record => record.ProjectName == oldName && record.Year == oldYear)
                     .ToList())
        {
            AssignNetworkHeader(record.ProjectName, record.Year, record.MaterialPath, newName, newYear, oldName, oldYear, nameChanged, yearChanged,
                value => record.ProjectName = value,
                value => record.Year = value,
                value => record.MaterialPath = value);
        }

        foreach (NetworkOutboundRecord record in _dbContext.NetworkOutboundRecords
                     .Where(record => record.ProjectName == oldName && record.Year == oldYear)
                     .ToList())
        {
            AssignNetworkHeader(record.ProjectName, record.Year, record.MaterialPath, newName, newYear, oldName, oldYear, nameChanged, yearChanged,
                value => record.ProjectName = value,
                value => record.Year = value,
                value => record.MaterialPath = value);
        }

        foreach (NetworkOutboundItem item in _dbContext.NetworkOutboundItems
                     .Where(item => item.ProjectName == oldName && item.Year == oldYear)
                     .ToList())
        {
            if (nameChanged)
            {
                AssignText(item.ProjectName, newName, value => item.ProjectName = value);
            }

            if (yearChanged)
            {
                AssignText(item.Year, newYear, value => item.Year = value);
            }
        }

        foreach (NetworkOnNetAsset asset in _dbContext.NetworkOnNetAssets
                     .Where(asset => asset.ProjectName == oldName && asset.Year == oldYear)
                     .ToList())
        {
            if (nameChanged)
            {
                AssignText(asset.ProjectName, newName, value => asset.ProjectName = value);
            }

            if (yearChanged)
            {
                AssignText(asset.Year, newYear, value => asset.Year = value);
            }
        }
    }

    private bool HasNameOnlySnapshots(string name, string year)
    {
        return _dbContext.YearlyArchiveBoxes.Any(box => box.ProjectName == name && box.Year == year)
            || ElectronicArchiveProjectLookup.QueryContainerIds(_dbContext, null, name, year).Any()
            || HasNetworkSnapshots(name, year);
    }

    private bool HasNetworkSnapshots(string name, string year)
    {
        return _dbContext.NetworkInboundRecords.Any(record => record.ProjectName == name && record.Year == year)
            || _dbContext.NetworkOutboundRecords.Any(record => record.ProjectName == name && record.Year == year)
            || _dbContext.NetworkOutboundItems.Any(item => item.ProjectName == name && item.Year == year)
            || _dbContext.NetworkOnNetAssets.Any(asset => asset.ProjectName == name && asset.Year == year);
    }

    private static void AssignNetworkHeader(
        string currentName,
        string currentYear,
        string currentPath,
        string newName,
        string newYear,
        string oldName,
        string oldYear,
        bool nameChanged,
        bool yearChanged,
        Action<string> setName,
        Action<string> setYear,
        Action<string> setPath)
    {
        if (nameChanged)
        {
            AssignText(currentName, newName, setName);
        }

        if (yearChanged)
        {
            AssignText(currentYear, newYear, setYear);
        }

        if (nameChanged || yearChanged)
        {
            AssignText(
                currentPath,
                ProjectSnapshotPathSupport.ReplaceYearAndProjectPair(currentPath, oldYear, oldName, newYear, newName),
                setPath);
        }
    }

    private static void AssignProjectName(IQueryable<YearlyArchiveRegisterRecord> records, string newName)
    {
        foreach (YearlyArchiveRegisterRecord record in records.ToList())
        {
            AssignText(record.ProjectName ?? string.Empty, newName, value => record.ProjectName = value);
        }
    }

    private static void AssignProjectName(IQueryable<YearlyArchiveOutboundRecord> records, string newName)
    {
        foreach (YearlyArchiveOutboundRecord record in records.ToList())
        {
            AssignText(record.ProjectName, newName, value => record.ProjectName = value);
        }
    }

    private static void AssignProjectName(IQueryable<YearlyArchiveReturnRecord> records, string newName)
    {
        foreach (YearlyArchiveReturnRecord record in records.ToList())
        {
            AssignText(record.ProjectName, newName, value => record.ProjectName = value);
        }
    }

    private static void AssignText(string current, string next, Action<string> assign)
    {
        if (!string.Equals(current, next, StringComparison.Ordinal))
        {
            assign(next);
        }
    }
}
