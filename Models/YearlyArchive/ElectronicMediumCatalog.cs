using System;
using System.Collections.Generic;
using System.Linq;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 从介质袋找到当前唯一介质，再读取该介质上的资料明细。
    /// </summary>
    public static class ElectronicMediumCatalog
    {
        /// <summary>
        /// 返回袋子当前介质上的资料明细。介质导航未加载时返回空列表。
        /// </summary>
        public static List<YearlyElectronicArchiveUnitMediaItemLink> GetItemLinks(YearlyElectronicArchiveUnit unit)
        {
            ArgumentNullException.ThrowIfNull(unit);

            HardDiskMedia.HardDiskMedium? disk = unit.MediumLinks
                .Select(link => link.HardDiskMedium)
                .FirstOrDefault(medium => medium != null);
            if (disk != null)
            {
                return disk.ArchiveItemLinks;
            }

            OpticalDiscMedia.OpticalDiscMedium? disc = unit.DiscLinks
                .Select(link => link.OpticalDiscMedium)
                .FirstOrDefault(medium => medium != null);
            if (disc != null)
            {
                return disc.ArchiveItemLinks;
            }

            return new List<YearlyElectronicArchiveUnitMediaItemLink>();
        }

        /// <summary>
        /// 资料明细当前所在的介质袋。介质与袋子导航都已加载时才有值。
        /// </summary>
        public static YearlyElectronicArchiveUnit? FindCurrentBag(YearlyElectronicArchiveUnitMediaItemLink link)
        {
            ArgumentNullException.ThrowIfNull(link);
            YearlyElectronicArchiveUnit? diskBag = link.HardDiskMedium?.ElectronicArchiveLinks
                .Select(item => item.ElectronicArchiveUnit)
                .FirstOrDefault(unit => unit != null);
            if (diskBag != null)
            {
                return diskBag;
            }

            return link.OpticalDiscMedium?.ElectronicArchiveLinks
                .Select(item => item.ElectronicArchiveUnit)
                .FirstOrDefault(unit => unit != null);
        }

        /// <summary>
        /// 把已有资料明细改挂到目标袋子当前的介质上。
        /// </summary>
        public static void AttachItemLinks(
            YearlyElectronicArchiveUnit unit,
            IEnumerable<YearlyElectronicArchiveUnitMediaItemLink> links,
            string? mediumCode = null)
        {
            ArgumentNullException.ThrowIfNull(links);
            foreach (YearlyElectronicArchiveUnitMediaItemLink link in links.ToList())
            {
                link.HardDiskMedium?.ArchiveItemLinks.Remove(link);
                link.OpticalDiscMedium?.ArchiveItemLinks.Remove(link);
                link.HardDiskMedium = null;
                link.HardDiskMediumId = null;
                link.OpticalDiscMedium = null;
                link.OpticalDiscMediumId = null;
                if (!string.IsNullOrWhiteSpace(mediumCode))
                {
                    link.MediumCode = mediumCode.Trim();
                }

                AddItemLink(unit, link);
            }
        }

        /// <summary>
        /// 把资料明细挂到袋子当前的硬盘或光盘上。
        /// </summary>
        public static void AddItemLink(YearlyElectronicArchiveUnit unit, YearlyElectronicArchiveUnitMediaItemLink link)
        {
            ArgumentNullException.ThrowIfNull(unit);
            ArgumentNullException.ThrowIfNull(link);

            HardDiskMedia.HardDiskMedium? disk = unit.MediumLinks
                .Select(item => item.HardDiskMedium)
                .FirstOrDefault(medium => medium != null);
            if (disk != null)
            {
                link.HardDiskMedium = disk;
                link.OpticalDiscMedium = null;
                link.OpticalDiscMediumId = null;
                if (disk.Id > 0)
                {
                    link.HardDiskMediumId = disk.Id;
                }
                if (!disk.ArchiveItemLinks.Contains(link))
                {
                    disk.ArchiveItemLinks.Add(link);
                }

                return;
            }

            OpticalDiscMedia.OpticalDiscMedium? disc = unit.DiscLinks
                .Select(item => item.OpticalDiscMedium)
                .FirstOrDefault(medium => medium != null);
            if (disc != null)
            {
                link.OpticalDiscMedium = disc;
                link.HardDiskMedium = null;
                link.HardDiskMediumId = null;
                if (disc.Id > 0)
                {
                    link.OpticalDiscMediumId = disc.Id;
                }
                if (!disc.ArchiveItemLinks.Contains(link))
                {
                    disc.ArchiveItemLinks.Add(link);
                }

                return;
            }

            throw new InvalidOperationException("电子介质袋尚未关联硬盘或光盘，不能写入资料明细。");
        }

        /// <summary>
        /// 把资料明细从当前介质移到另一块硬盘。
        /// </summary>
        public static void MoveItemLinksToHardDisk(
            IEnumerable<YearlyElectronicArchiveUnitMediaItemLink> links,
            HardDiskMedia.HardDiskMedium target)
        {
            ArgumentNullException.ThrowIfNull(links);
            ArgumentNullException.ThrowIfNull(target);

            foreach (YearlyElectronicArchiveUnitMediaItemLink link in links.ToList())
            {
                link.HardDiskMedium?.ArchiveItemLinks.Remove(link);
                link.OpticalDiscMedium?.ArchiveItemLinks.Remove(link);
                link.HardDiskMedium = target;
                link.OpticalDiscMedium = null;
                link.OpticalDiscMediumId = null;
                if (target.Id > 0)
                {
                    link.HardDiskMediumId = target.Id;
                }
                link.MediumCode = target.DiskCode?.Trim() ?? string.Empty;
                if (!target.ArchiveItemLinks.Contains(link))
                {
                    target.ArchiveItemLinks.Add(link);
                }
            }
        }

        /// <summary>
        /// 把资料明细从当前介质移到另一张光盘。
        /// </summary>
        public static void MoveItemLinksToOpticalDisc(
            IEnumerable<YearlyElectronicArchiveUnitMediaItemLink> links,
            OpticalDiscMedia.OpticalDiscMedium target)
        {
            ArgumentNullException.ThrowIfNull(links);
            ArgumentNullException.ThrowIfNull(target);

            foreach (YearlyElectronicArchiveUnitMediaItemLink link in links.ToList())
            {
                link.HardDiskMedium?.ArchiveItemLinks.Remove(link);
                link.OpticalDiscMedium?.ArchiveItemLinks.Remove(link);
                link.OpticalDiscMedium = target;
                link.HardDiskMedium = null;
                link.HardDiskMediumId = null;
                if (target.Id > 0)
                {
                    link.OpticalDiscMediumId = target.Id;
                }

                link.MediumCode = target.DiscCode?.Trim() ?? string.Empty;
                if (!target.ArchiveItemLinks.Contains(link))
                {
                    target.ArchiveItemLinks.Add(link);
                }
            }
        }

        /// <summary>
        /// 汇总介质上已加载资料明细对应的项目名称。
        /// </summary>
        public static string SummarizeProjectNames(YearlyElectronicArchiveUnit unit)
        {
            List<string> names = GetItemLinks(unit)
                .Select(link => link.MediaItem?.MediaEntry?.RegisterRecord?.ProjectName?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
            return string.Join("、", names);
        }

        /// <summary>
        /// 为立档事实写入介质主人。
        /// </summary>
        public static void AssignFactMedium(YearlyArchiveFilingFact fact, YearlyElectronicArchiveUnitMediaItemLink link)
        {
            ArgumentNullException.ThrowIfNull(fact);
            ArgumentNullException.ThrowIfNull(link);
            fact.HardDiskMediumId = link.HardDiskMediumId is > 0 ? link.HardDiskMediumId : link.HardDiskMedium?.Id;
            fact.OpticalDiscMediumId = link.OpticalDiscMediumId is > 0 ? link.OpticalDiscMediumId : link.OpticalDiscMedium?.Id;
            if (fact.HardDiskMediumId is 0)
            {
                fact.HardDiskMediumId = null;
            }

            if (fact.OpticalDiscMediumId is 0)
            {
                fact.OpticalDiscMediumId = null;
            }
        }
    }
}
