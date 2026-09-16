using System;
using System.Collections.Generic;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 登记申请与档案盒/电子立档单元关联的现算投影。
    /// 权威源为子项级链接（YearlyArchiveBoxMediaItemLinks / YearlyElectronicArchiveUnitMediaItemLinks），
    /// 登记单级关联由 <c>MediaItem → MediaEntry → RegisterRecord</c> 传递推导，不再落库存储。
    /// </summary>
    public static class ArchiveContainerRegisterRecordProjectionSupport
    {
        /// <summary>
        /// 从档案盒已加载的子项链接推导其关联的登记申请（去重）。
        /// 依赖 Include 链：MediaItemLinks → MediaItem → MediaEntry → RegisterRecord。
        /// </summary>
        public static List<YearlyArchiveRegisterRecord> ProjectBoxRegisterRecords(YearlyArchiveBox box)
        {
            ArgumentNullException.ThrowIfNull(box);

            var records = new List<YearlyArchiveRegisterRecord>();
            var seenIds = new HashSet<int>();
            foreach (var link in box.MediaItemLinks)
            {
                var record = link.MediaItem?.MediaEntry?.RegisterRecord;
                if (record == null || !seenIds.Add(record.Id))
                {
                    continue;
                }

                records.Add(record);
            }

            return records;
        }

        /// <summary>
        /// 从电子立档单元已加载的子项链接推导其关联的登记申请（去重）。
        /// 依赖 Include 链：MediaItemLinks → MediaItem → MediaEntry → RegisterRecord。
        /// </summary>
        public static List<YearlyArchiveRegisterRecord> ProjectUnitRegisterRecords(YearlyElectronicArchiveUnit unit)
        {
            ArgumentNullException.ThrowIfNull(unit);

            var records = new List<YearlyArchiveRegisterRecord>();
            var seenIds = new HashSet<int>();
            foreach (var link in unit.MediaItemLinks)
            {
                var record = link.MediaItem?.MediaEntry?.RegisterRecord;
                if (record == null || !seenIds.Add(record.Id))
                {
                    continue;
                }

                records.Add(record);
            }

            return records;
        }

        /// <summary>
        /// 从登记申请已加载的介质明细推导其关联的档案盒（去重）。
        /// 依赖 Include 链：MediaEntries → Items → ArchiveBoxLinks → ArchiveBox。
        /// </summary>
        public static List<YearlyArchiveBox> ProjectRecordArchiveBoxes(YearlyArchiveRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var boxes = new List<YearlyArchiveBox>();
            var seenIds = new HashSet<int>();
            foreach (var media in record.MediaEntries)
            {
                foreach (var item in media.Items)
                {
                    foreach (var link in item.ArchiveBoxLinks)
                    {
                        var box = link.ArchiveBox;
                        if (box == null || !seenIds.Add(box.Id))
                        {
                            continue;
                        }

                        boxes.Add(box);
                    }
                }
            }

            return boxes;
        }

        /// <summary>
        /// 从登记申请已加载的介质明细推导其关联的电子立档单元（去重）。
        /// 依赖 Include 链：MediaEntries → Items → ElectronicArchiveUnitMediaItemLinks → ElectronicArchiveUnit。
        /// </summary>
        public static List<YearlyElectronicArchiveUnit> ProjectRecordElectronicUnits(YearlyArchiveRegisterRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var units = new List<YearlyElectronicArchiveUnit>();
            var seenIds = new HashSet<int>();
            foreach (var media in record.MediaEntries)
            {
                foreach (var item in media.Items)
                {
                    foreach (var link in item.ElectronicArchiveUnitMediaItemLinks)
                    {
                        var unit = link.ElectronicArchiveUnit;
                        if (unit != null && seenIds.Add(unit.Id))
                        {
                            units.Add(unit);
                        }
                    }
                }
            }

            return units;
        }

        /// <summary>
        /// 判断登记介质条目是否已整体立档（内存版，需已加载 Items 与其 ElectronicArchiveUnitMediaItemLinks）。
        /// 语义与原 YearlyElectronicArchiveUnitMediaLinks 写入条件一致：条目有子项且全部子项均已入电子立档单元。
        /// </summary>
        public static bool IsMediaEntryArchived(YearlyArchiveRegisterMedia mediaEntry)
        {
            ArgumentNullException.ThrowIfNull(mediaEntry);

            return mediaEntry.Items.Count > 0
                && mediaEntry.Items.All(item => item.ElectronicArchiveUnitMediaItemLinks.Any());
        }
    }
}
