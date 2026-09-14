using System;
using System.Collections.Generic;
using System.Linq;
using DocMgr.Data;
using DocMgr.Models.HistoryArchive;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Services.HistoryArchive
{
    /// <summary>
    /// 历史档案盒实体与台账关联的维护逻辑（导入/编辑/删除后保持盒表、链接表与台账一致）。
    /// 须在调用方事务内执行，保证台账行、盒实体、关联三者同生共死。
    /// </summary>
    public static class HistoryArchiveBoxLedgerMaintenanceSupport
    {
        private const string DefaultPlacementMode = "SpineOut";

        /// <summary>台账行快照：行ID + 盒号字段 + 盒规格。</summary>
        public readonly record struct LedgerRowSnapshot(int RecordId, string? BoxNumber, string? BoxSpecification);

        /// <summary>
        /// 按台账行同步盒实体与关联：解析盒号 → upsert 盒 → 重建该批行的关联。
        /// 无法解析的盒号跳过（导入路径已由档口守卫保证可解析）。
        /// </summary>
        public static void SyncBoxesAndLinksForRows(
            AppDbContext dbContext,
            string materialKind,
            IReadOnlyList<LedgerRowSnapshot> rows,
            string? operatorName)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentException.ThrowIfNullOrWhiteSpace(materialKind);
            ArgumentNullException.ThrowIfNull(rows);
            if (rows.Count == 0)
            {
                return;
            }

            var parsedRows = new List<(int RecordId, IReadOnlyList<string> BoxCodes)>();
            var allBoxCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var codes = HistoryArchiveBoxCodeSupport.SplitBoxCodes(row.BoxNumber)
                    .Where(code => HistoryArchiveBoxCodeSupport.TryParseBoxCode(
                        code,
                        out string cabinet,
                        out _,
                        out _,
                        out _))
                    .ToList();
                parsedRows.Add((row.RecordId, codes));
                foreach (string code in codes)
                {
                    allBoxCodes.Add(code);
                }
            }

            UpsertBoxes(dbContext, allBoxCodes, rows, operatorName);

            // 新盒先落库取得真实自增 Id；后续链接写入按盒号查库取 Id，未落库会全部跳过
            dbContext.SaveChanges();

            var recordIds = rows.Select(row => row.RecordId).ToList();
            var existingLinks = dbContext.HistoryArchiveBoxLedgerLinks
                .Where(link => link.MaterialKind == materialKind && recordIds.Contains(link.RecordId))
                .ToList();
            var boxIdLookup = dbContext.HistoryArchiveBoxes
                .Where(box => allBoxCodes.Contains(box.BoxCode))
                .ToDictionary(box => box.BoxCode, box => box.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var (recordId, boxCodes) in parsedRows)
            {
                foreach (string code in boxCodes)
                {
                    if (!boxIdLookup.TryGetValue(code, out int boxId))
                    {
                        continue;
                    }

                    bool exists = existingLinks.Any(link =>
                        link.RecordId == recordId
                        && link.HistoryArchiveBoxId == boxId);
                    if (!exists)
                    {
                        dbContext.HistoryArchiveBoxLedgerLinks.Add(new HistoryArchiveBoxLedgerLink
                        {
                            HistoryArchiveBoxId = boxId,
                            MaterialKind = materialKind,
                            RecordId = recordId,
                            CreatedAt = DateTime.Now
                        });
                    }
                }
            }

            // 行的新盒号集合之外的旧关联删除（盒号被编辑的场景）
            var validPairs = new HashSet<(int BoxId, int RecordId)>();
            foreach (var (recordId, boxCodes) in parsedRows)
            {
                foreach (string code in boxCodes)
                {
                    if (boxIdLookup.TryGetValue(code, out int boxId))
                    {
                        validPairs.Add((boxId, recordId));
                    }
                }
            }

            foreach (var link in existingLinks)
            {
                if (!validPairs.Contains((link.HistoryArchiveBoxId, link.RecordId)))
                {
                    dbContext.HistoryArchiveBoxLedgerLinks.Remove(link);
                }
            }

            // 链接变更落库：后续 CleanupOrphanBoxes 按 ExecuteDelete 直查数据库判定孤儿，
            // 未落库的链接会让刚建的盒被误判为孤儿删除
            dbContext.SaveChanges();
        }

        /// <summary>删除台账行对应的关联（整表重建/删行/删表前调用）。</summary>
        public static void RemoveLinksForRecords(
            AppDbContext dbContext,
            string materialKind,
            IReadOnlyList<int> recordIds)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            if (recordIds == null || recordIds.Count == 0)
            {
                return;
            }

            dbContext.HistoryArchiveBoxLedgerLinks
                .Where(link => link.MaterialKind == materialKind && recordIds.Contains(link.RecordId))
                .ExecuteDelete();
        }

        /// <summary>清理孤儿盒：无任何台账关联的盒实体删除（盒生命周期随台账存在）。</summary>
        public static void CleanupOrphanBoxes(AppDbContext dbContext)
        {
            ArgumentNullException.ThrowIfNull(dbContext);

            dbContext.HistoryArchiveBoxes
                .Where(box => !dbContext.HistoryArchiveBoxLedgerLinks
                    .Any(link => link.HistoryArchiveBoxId == box.Id))
                .ExecuteDelete();
        }

        private static void UpsertBoxes(
            AppDbContext dbContext,
            HashSet<string> allBoxCodes,
            IReadOnlyList<LedgerRowSnapshot> rows,
            string? operatorName)
        {
            if (allBoxCodes.Count == 0)
            {
                return;
            }

            var specByBoxCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                string spec = row.BoxSpecification?.Trim() ?? string.Empty;
                foreach (string code in HistoryArchiveBoxCodeSupport.SplitBoxCodes(row.BoxNumber))
                {
                    if (!specByBoxCode.ContainsKey(code) || string.IsNullOrWhiteSpace(specByBoxCode[code]))
                    {
                        specByBoxCode[code] = spec;
                    }
                }
            }

            var existingBoxes = dbContext.HistoryArchiveBoxes
                .Where(box => allBoxCodes.Contains(box.BoxCode))
                .ToDictionary(box => box.BoxCode, StringComparer.OrdinalIgnoreCase);

            foreach (string code in allBoxCodes)
            {
                if (!HistoryArchiveBoxCodeSupport.TryParseBoxCode(
                        code,
                        out _,
                        out _,
                        out _,
                        out string normalizedBoxCode))
                {
                    continue;
                }

                specByBoxCode.TryGetValue(normalizedBoxCode, out string? spec);
                string boxSpec = spec ?? string.Empty;
                if (existingBoxes.TryGetValue(normalizedBoxCode, out var box))
                {
                    bool changed = SetIfChanged(box.BoxSpecification, boxSpec, value => box.BoxSpecification = value);

                    if (changed && string.IsNullOrWhiteSpace(box.ArchivedBy))
                    {
                        box.ArchivedBy = ResolveOperator(operatorName);
                    }

                    continue;
                }

                dbContext.HistoryArchiveBoxes.Add(new HistoryArchiveBox
                {
                    BoxCode = normalizedBoxCode,
                    BoxSpecification = boxSpec,
                    PlacementMode = DefaultPlacementMode,
                    LifecycleStatus = HistoryArchiveDisposalDomainValues.LifecycleInStock,
                    ArchivedBy = ResolveOperator(operatorName),
                    ArchivedDate = DateTime.Now
                });
            }
        }

        private static string ResolveOperator(string? operatorName)
        {
            string trimmed = operatorName?.Trim() ?? string.Empty;
            return trimmed.Length > 0 ? trimmed : "System";
        }

        private static bool SetIfChanged(string current, string target, Action<string> setter)
        {
            if (string.Equals(current?.Trim(), target?.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            setter(target ?? string.Empty);
            return true;
        }
    }
}
