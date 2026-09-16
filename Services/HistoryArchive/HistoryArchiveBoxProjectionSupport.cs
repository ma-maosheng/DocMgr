using System;
using System.Collections.Generic;
using System.Linq;
using DocMgr.Data;
using DocMgr.Models.HistoryArchive;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Services.HistoryArchive
{
    /// <summary>
    /// 历史台账行盒号/盒规格投影的水合组件。
    /// 权威源为 HistoryArchiveBoxes + HistoryArchiveBoxLedgerLinks（多态关联，无导航属性，须手工拼接）。
    /// 纪律：仓储不得向外返回未水合的台账行，否则界面盒号列为空。
    /// </summary>
    public static class HistoryArchiveBoxProjectionSupport
    {
        /// <summary>多盒关联以中文分号拼接，与既有台账文本格式一致。</summary>
        private const string BoxCodeSeparator = "；";

        /// <summary>
        /// 就地水合台账行的 <c>BoxNumber</c> / <c>BoxSpecification</c> 投影属性。
        /// 盒号按关联表顺序拼接；盒规格取该行关联盒中首个非空规格。
        /// </summary>
        /// <param name="dbContext">数据库上下文。</param>
        /// <param name="materialKind">台账类别（TopoMap / AerialPhoto / OtherMap）。</param>
        /// <param name="rows">台账行集合（含已离库行；无关联时投影为空）。</param>
        public static void Hydrate(AppDbContext dbContext, string materialKind, IReadOnlyCollection<TopoMap> rows)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            HydrateCore(dbContext, materialKind, rows, row => row.Id,
                (row, boxNumber, boxSpecification) =>
                {
                    row.BoxNumber = boxNumber;
                    row.BoxSpecification = boxSpecification;
                });
        }

        /// <inheritdoc cref="Hydrate(AppDbContext, string, IReadOnlyCollection{TopoMap})"/>
        public static void Hydrate(AppDbContext dbContext, string materialKind, IReadOnlyCollection<AerialPhoto> rows)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            HydrateCore(dbContext, materialKind, rows, row => row.Id,
                (row, boxNumber, boxSpecification) =>
                {
                    row.BoxNumber = boxNumber;
                    row.BoxSpecification = boxSpecification;
                });
        }

        /// <inheritdoc cref="Hydrate(AppDbContext, string, IReadOnlyCollection{TopoMap})"/>
        public static void Hydrate(AppDbContext dbContext, string materialKind, IReadOnlyCollection<OtherMap> rows)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            HydrateCore(dbContext, materialKind, rows, row => row.Id,
                (row, boxNumber, boxSpecification) =>
                {
                    row.BoxNumber = boxNumber;
                    row.BoxSpecification = boxSpecification;
                });
        }

        private static void HydrateCore<TEntity>(
            AppDbContext dbContext,
            string materialKind,
            IReadOnlyCollection<TEntity> rows,
            Func<TEntity, int> idSelector,
            Action<TEntity, string, string> apply)
        {
            if (rows.Count == 0)
            {
                return;
            }

            List<int> recordIds = rows.Select(idSelector).Distinct().ToList();

            // 投影水合必须绕开变更跟踪：长生命周期作用域的 DbContext 一旦跟踪过旧盒号实体，
            // 跟踪查询会经身份解析返回内存中的过期 BoxCode（其他作用域迁档提交后不刷新），
            // 导致迁档后按新盒号查询档案盒内容为空。此处为纯读投影，必须直读数据库最新值。
            var boxIdLookup = dbContext.HistoryArchiveBoxes
                .AsNoTracking()
                .ToDictionary(box => box.Id, box => box, EqualityComparer<int>.Default);
            var codesByRecordId = new Dictionary<int, List<string>>();
            var specByRecordId = new Dictionary<int, string>();

            foreach (var link in dbContext.HistoryArchiveBoxLedgerLinks
                .AsNoTracking()
                .Where(link => link.MaterialKind == materialKind && recordIds.Contains(link.RecordId))
                .OrderBy(link => link.Id)
                .ToList())
            {
                if (!boxIdLookup.TryGetValue(link.HistoryArchiveBoxId, out HistoryArchiveBox? box))
                {
                    continue;
                }

                if (!codesByRecordId.TryGetValue(link.RecordId, out List<string>? codes))
                {
                    codes = new List<string>();
                    codesByRecordId[link.RecordId] = codes;
                }

                codes.Add(box.BoxCode);

                if (!specByRecordId.ContainsKey(link.RecordId)
                    || string.IsNullOrWhiteSpace(specByRecordId[link.RecordId]))
                {
                    specByRecordId[link.RecordId] = box.BoxSpecification;
                }
            }

            foreach (TEntity row in rows)
            {
                int recordId = idSelector(row);
                codesByRecordId.TryGetValue(recordId, out List<string>? codes);
                specByRecordId.TryGetValue(recordId, out string? specification);

                string boxNumber = codes is { Count: > 0 }
                    ? string.Join(BoxCodeSeparator, codes.Distinct(StringComparer.OrdinalIgnoreCase))
                    : string.Empty;
                apply(row, boxNumber, specification?.Trim() ?? string.Empty);
            }
        }
    }
}
