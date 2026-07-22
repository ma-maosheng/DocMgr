using DocMgr.Data;
using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.Cabinets;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.YearlyArchive;
using DocMgr.Services.YearlyArchive;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Tools.ArchiveEmptiedContainerVerify;

/// <summary>
/// 自动验证「空盒/空袋与检索三端对齐」计划中的 5 项检查。
/// 用法：dotnet run --project tools/ArchiveEmptiedContainerVerify -- [可选: DocMgr.db 路径]
/// </summary>
internal static class Program
{
    private static int _passed;
    private static int _failed;

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== 空盒/空袋三端对齐 · 自动验证 ===");
        Console.WriteLine();

        Verify1_SimulatedNoReturnEmptiesAsTransferred();
        Verify2_SimulatedLossEmptiesAsDestroyed();
        await Verify3_Historical001RepairAndInArchiveSearchAsync(args);
        Verify4_ElectronicNoReturnBagEmpties();
        Verify5_PendingReturnKeepsSlot();

        Console.WriteLine();
        Console.WriteLine($"结果：通过 {_passed}，失败 {_failed}");
        return _failed == 0 ? 0 : 1;
    }

    /// <summary>1. 模拟不还致空盒 → 应释档；事实 → Transferred。</summary>
    private static void Verify1_SimulatedNoReturnEmptiesAsTransferred()
    {
        var fact = NewSimulatedFact(FilingFactLifecycleStatus.InArchive, contentCount: 1, location: "柜A-1-1-01");
        var rows = new[]
        {
            new YearlyArchiveBoxMediaItemRow
            {
                Fact = fact,
                PendingReturnCopyCount = 0,
                NoReturnCopyCount = 1,
                LostCopyCount = 0,
            }
        };

        var totals = ArchiveSimulatedBoxSlotOccupancySupport.AggregateRows(rows);
        var breakdown = ArchiveBoxMediaItemCopyCountSupport.Resolve(fact, 0, 1, 0);
        string? status = ArchiveEmptiedContainerFactLifecycleSupport.ResolveTerminalLifecycleStatus(breakdown);
        ArchiveEmptiedContainerFactLifecycleSupport.ApplyOnContainerEmptied(fact, breakdown, DateTime.Now);

        Assert(
            "1.模拟不还·应释放占格",
            totals.ShouldReleaseSlot,
            $"ShouldReleaseSlot={totals.ShouldReleaseSlot}");
        Assert(
            "1.模拟不还·终态=Transferred",
            status == FilingFactLifecycleStatus.Transferred,
            $"status={status}");
        Assert(
            "1.模拟不还·事实已写 Transferred 且位置已清",
            fact.LifecycleStatus == FilingFactLifecycleStatus.Transferred
            && string.IsNullOrEmpty(fact.CurrentStorageLocation),
            $"Lifecycle={fact.LifecycleStatus}, Loc='{fact.CurrentStorageLocation}'");
    }

    /// <summary>2. 归还全灭失致空盒 → 事实 Destroyed；不作为在库。</summary>
    private static void Verify2_SimulatedLossEmptiesAsDestroyed()
    {
        var fact = NewSimulatedFact(FilingFactLifecycleStatus.Borrowed, contentCount: 1, location: "柜A-1-1-03");
        var breakdown = ArchiveBoxMediaItemCopyCountSupport.Resolve(fact, pendingReturnCopyCount: 0, noReturnCopyCount: 0, lostCopyCount: 1);
        string? status = ArchiveEmptiedContainerFactLifecycleSupport.ResolveTerminalLifecycleStatus(breakdown);
        ArchiveEmptiedContainerFactLifecycleSupport.ApplyOnContainerEmptied(fact, breakdown, DateTime.Now);

        var totals = ArchiveSimulatedBoxSlotOccupancySupport.AggregateRows(
        [
            new YearlyArchiveBoxMediaItemRow
            {
                Fact = fact,
                PendingReturnCopyCount = 0,
                NoReturnCopyCount = 0,
                LostCopyCount = 1,
            }
        ]);

        Assert(
            "2.灭失空盒·应释放占格",
            totals.ShouldReleaseSlot,
            $"ShouldReleaseSlot={totals.ShouldReleaseSlot}");
        Assert(
            "2.灭失空盒·终态=Destroyed",
            status == FilingFactLifecycleStatus.Destroyed
            && fact.LifecycleStatus == FilingFactLifecycleStatus.Destroyed,
            $"status={status}, fact={fact.LifecycleStatus}");
        Assert(
            "2.灭失空盒·非在库",
            fact.LifecycleStatus != FilingFactLifecycleStatus.InArchive,
            $"Lifecycle={fact.LifecycleStatus}");
    }

    /// <summary>3. 历史 001 纠偏后，筛在库不应再命中已清空盒；002 仍可在库。</summary>
    private static async Task Verify3_Historical001RepairAndInArchiveSearchAsync(string[] args)
    {
        string? dbPath = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? Path.GetFullPath(args[0])
            : ResolveDefaultDatabasePath();

        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
        {
            Assert("3.历史001·找到数据库", false, $"未找到 DocMgr.db（尝试路径：{dbPath ?? "(null)"}）");
            return;
        }

        string workDb = Path.Combine(Path.GetTempPath(), $"DocMgr_verify3_{Guid.NewGuid():N}.db");
        try
        {
            File.Copy(dbPath, workDb, overwrite: true);
            // 去掉 WAL 残留，避免读到不一致快照
            foreach (string suffix in new[] { "-wal", "-shm" })
            {
                string side = dbPath + suffix;
                if (File.Exists(side))
                {
                    File.Copy(side, workDb + suffix, overwrite: true);
                }
            }

            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = workDb,
                Mode = SqliteOpenMode.ReadWrite,
            }.ToString());
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using (var db = new AppDbContext(options))
            {
                var outboundRepo = new ArchiveOutboundRepository(db);
                var repair = new ArchiveEmptiedContainerLegacyRepairService(db, outboundRepo);
                int repaired = await repair.RepairAsync();
                Console.WriteLine($"  [信息] 历史纠偏改写 {repaired} 条立档事实（工作副本）");
            }

            await using (var db = new AppDbContext(options))
            {
                var boxes = await db.YearlyArchiveBoxes.AsNoTracking()
                    .Where(b => b.ArchiveSequenceNo.Contains("2026-001")
                                || b.ArchiveSequenceNo.Contains("2026-002"))
                    .ToListAsync();

                var box001 = boxes.FirstOrDefault(b => b.ArchiveSequenceNo.Contains("2026-001"));
                var box002 = boxes.FirstOrDefault(b => b.ArchiveSequenceNo.Contains("2026-002"));

                Assert(
                    "3.历史·存在001且非InUse",
                    box001 != null
                    && !string.Equals(box001.ContainerLifecycleStatus, ArchiveContainerLifecycleStatus.InUse, StringComparison.Ordinal),
                    box001 == null
                        ? "无001"
                        : $"{box001.ArchiveSequenceNo} status={box001.ContainerLifecycleStatus}");

                Assert(
                    "3.历史·002仍InUse",
                    box002 != null
                    && string.Equals(box002.ContainerLifecycleStatus, ArchiveContainerLifecycleStatus.InUse, StringComparison.Ordinal),
                    box002 == null
                        ? "无002"
                        : $"{box002.ArchiveSequenceNo} status={box002.ContainerLifecycleStatus}");

                var factRepo = new ArchiveFilingFactRepository(db);
                var inArchiveHits = await factRepo.SearchByRegisterCriteriaAsync(
                    ArchiveRegisterDomainValues.MediaKindSimulated,
                    new RegisterDirectionSearchCriteria
                    {
                        Year = "2026",
                        LifecycleStatus = FilingFactLifecycleStatus.InArchive,
                    });

                bool has001 = inArchiveHits.Any(f =>
                    f.ContainerCode.Contains("2026-001", StringComparison.Ordinal)
                    || f.CurrentContainerCode.Contains("2026-001", StringComparison.Ordinal));
                bool has002 = inArchiveHits.Any(f =>
                    f.ContainerCode.Contains("2026-002", StringComparison.Ordinal)
                    || f.CurrentContainerCode.Contains("2026-002", StringComparison.Ordinal));

                Assert(
                    "3.历史·在库检索不含001",
                    !has001,
                    $"命中数={inArchiveHits.Count}, 含001={has001}");
                Assert(
                    "3.历史·在库检索含002",
                    has002,
                    $"命中数={inArchiveHits.Count}, 含002={has002}");

                if (box001 != null)
                {
                    var facts001 = await db.YearlyArchiveFilingFacts.AsNoTracking()
                        .Where(f => f.MediaKind == ArchiveRegisterDomainValues.MediaKindSimulated
                                    && (f.ContainerId == box001.Id
                                        || f.ContainerCode == box001.ArchiveSequenceNo
                                        || f.CurrentContainerCode == box001.ArchiveSequenceNo))
                        .ToListAsync();
                    bool anyStillInArchive = facts001.Any(f =>
                        string.Equals(f.LifecycleStatus, FilingFactLifecycleStatus.InArchive, StringComparison.Ordinal));
                    Assert(
                        "3.历史·001下模拟事实已非InArchive",
                        !anyStillInArchive,
                        facts001.Count == 0
                            ? "无模拟事实"
                            : string.Join("; ", facts001.Select(f => $"#{f.Id}:{f.LifecycleStatus}")));
                }
            }
        }
        catch (Exception ex)
        {
            Assert("3.历史001·执行异常", false, ex.Message);
        }
        finally
        {
            TryDelete(workDb);
            TryDelete(workDb + "-wal");
            TryDelete(workDb + "-shm");
        }
    }

    /// <summary>4. 电子提档不还致空袋 → 应释档；事实 Transferred。</summary>
    private static void Verify4_ElectronicNoReturnBagEmpties()
    {
        var fact = NewElectronicFact(FilingFactLifecycleStatus.InArchive, location: "柜E-1-1-01");
        var rows = new[]
        {
            new YearlyArchiveBoxMediaItemRow
            {
                Fact = fact,
                PendingReturnCopyCount = 0,
                NoReturnCopyCount = 1,
                LostCopyCount = 0,
            }
        };

        var totals = ArchiveSimulatedBoxSlotOccupancySupport.AggregateElectronicRows(rows);
        var breakdown = ArchiveBoxMediaItemCopyCountSupport.Resolve(fact, 0, 1, 0);
        ArchiveEmptiedContainerFactLifecycleSupport.ApplyOnContainerEmptied(fact, breakdown, DateTime.Now);

        Assert(
            "4.电子不还·应释放占格",
            totals.ShouldReleaseSlot,
            $"ShouldReleaseSlot={totals.ShouldReleaseSlot}");
        Assert(
            "4.电子不还·事实=Transferred 且位置已清",
            fact.LifecycleStatus == FilingFactLifecycleStatus.Transferred
            && string.IsNullOrEmpty(fact.CurrentStorageLocation),
            $"Lifecycle={fact.LifecycleStatus}, Loc='{fact.CurrentStorageLocation}'");
    }

    /// <summary>5. 有待还 → 不释档；可保持借出中。</summary>
    private static void Verify5_PendingReturnKeepsSlot()
    {
        var fact = NewSimulatedFact(FilingFactLifecycleStatus.Borrowed, contentCount: 1, location: "柜A-1-1-02");
        var rows = new[]
        {
            new YearlyArchiveBoxMediaItemRow
            {
                Fact = fact,
                PendingReturnCopyCount = 1,
                NoReturnCopyCount = 0,
                LostCopyCount = 0,
            }
        };

        var totals = ArchiveSimulatedBoxSlotOccupancySupport.AggregateRows(rows);
        var breakdown = ArchiveBoxMediaItemCopyCountSupport.Resolve(fact, 1, 0, 0);
        string? status = ArchiveEmptiedContainerFactLifecycleSupport.ResolveTerminalLifecycleStatus(breakdown);

        Assert(
            "5.有待还·不释放占格",
            !totals.ShouldReleaseSlot && totals.HasPendingReturn,
            $"ShouldRelease={totals.ShouldReleaseSlot}, Pending={totals.PendingReturn}");
        Assert(
            "5.有待还·不改写终态",
            status == null,
            $"status={status}");
        Assert(
            "5.有待还·仍可借出中",
            fact.LifecycleStatus == FilingFactLifecycleStatus.Borrowed,
            $"Lifecycle={fact.LifecycleStatus}");
    }

    private static YearlyArchiveFilingFact NewSimulatedFact(string lifecycle, int contentCount, string location) =>
        new()
        {
            MediaKind = ArchiveRegisterDomainValues.MediaKindSimulated,
            ContainerKind = ArchiveContainerKind.ArchiveBox,
            ContentCount = contentCount,
            LifecycleStatus = lifecycle,
            CurrentStorageLocation = location,
            StorageLocation = location,
            ContainerCode = "年度模拟-验-001",
            CurrentContainerCode = "年度模拟-验-001",
            FormNo = "验-001",
            ItemName = "验证子项",
            BorrowHintLevel = FilingFactBorrowHintLevel.OriginalBorrowed,
            BorrowHintText = "原件借出中",
        };

    private static YearlyArchiveFilingFact NewElectronicFact(string lifecycle, string location) =>
        new()
        {
            MediaKind = ArchiveRegisterDomainValues.MediaKindElectronic,
            ContainerKind = ArchiveContainerKind.ElectronicBag,
            ContentCount = 1,
            LifecycleStatus = lifecycle,
            CurrentStorageLocation = location,
            StorageLocation = location,
            ContainerCode = "年度电子-验-001",
            CurrentContainerCode = "年度电子-验-001",
            FormNo = "验-E-001",
            ItemName = "电子验证子项",
        };

    private static string? ResolveDefaultDatabasePath()
    {
        string baseDir = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "bin", "Debug", "net8.0-windows", "DocMgr.db")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "DocMgr.db")),
            Path.Combine(baseDir, "DocMgr.db"),
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void Assert(string name, bool condition, string detail)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"[通过] {name}");
        }
        else
        {
            _failed++;
            Console.WriteLine($"[失败] {name} — {detail}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore temp cleanup
        }
    }
}
