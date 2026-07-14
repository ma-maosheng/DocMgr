using DocMgr.Config;
using DocMgr.Data;
using DocMgr.Infrastructure.Schema;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Tools.SchemaDictionarySync;

internal static class Program
{
    private const string SchemaRelativePath = ".cursor/schema/SchemaDictionary.yaml";
    private const string RuleRelativePath = ".cursor/rules/schema-dictionary.mdc";

    public static int Main(string[] args)
    {
        try
        {
            var repoRoot = ResolveRepoRoot(args);
            if (args.Any(arg => string.Equals(arg, "apply-db", StringComparison.OrdinalIgnoreCase)))
            {
                return ApplyDictionaryToDatabase(repoRoot);
            }

            return SyncSchemaDictionary(repoRoot);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Schema dictionary sync failed: {ex.Message}");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int SyncSchemaDictionary(string repoRoot)
    {
        var schemaPath = Path.Combine(repoRoot, SchemaRelativePath);
        var rulePath = Path.Combine(repoRoot, RuleRelativePath);

        using var dbContext = SchemaDictionaryCatalog.CreateDesignTimeContext();
        var snapshots = SchemaDictionaryCatalog.GetEntitySnapshots(dbContext);
        var document = SchemaDictionaryYaml.LoadOrCreate(schemaPath);
        var result = SchemaDictionarySyncService.Sync(snapshots, document);

        SchemaDictionaryYaml.Save(schemaPath, result.Document);
        var snapshotMap = snapshots.ToDictionary(snapshot => snapshot.EntityName, StringComparer.Ordinal);
        File.WriteAllText(rulePath, SchemaDictionaryRuleGenerator.Generate(result.Document, snapshotMap));

        var runtimeCopyPath = SchemaDictionaryPathSupport.GetRuntimeDictionaryCopyPath();
        SchemaDictionaryYaml.Save(runtimeCopyPath, result.Document);

        Console.WriteLine("Schema dictionary sync completed.");
        Console.WriteLine($"  Repo root   : {repoRoot}");
        Console.WriteLine($"  YAML        : {schemaPath}");
        Console.WriteLine($"  Cursor rule : {rulePath}");
        Console.WriteLine($"  Runtime copy: {runtimeCopyPath}");
        Console.WriteLine($"  Entities    : {snapshots.Count}");
        Console.WriteLine($"  Added tables: {result.AddedTables}");
        Console.WriteLine($"  Added fields: {result.AddedFields}");
        Console.WriteLine($"  Deprecated  : {result.DeprecatedTables}");
        Console.WriteLine($"  Needs review: {result.NeedsReviewFields.Count}");

        if (result.NeedsReviewFields.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Fields marked needsReview (edit SchemaDictionary.yaml):");
            foreach (var fieldKey in result.NeedsReviewFields.Take(30))
            {
                Console.WriteLine($"  - {fieldKey}");
            }

            if (result.NeedsReviewFields.Count > 30)
            {
                Console.WriteLine($"  ... and {result.NeedsReviewFields.Count - 30} more");
            }
        }

        return 0;
    }

    private static int ApplyDictionaryToDatabase(string repoRoot)
    {
        var appOutputDirectory = ResolveAppOutputDirectory(repoRoot);
        var databaseOptions = DocMgrDatabaseConfiguration.Load(appOutputDirectory);
        var databaseSettings = new DocMgrDatabaseSettings(databaseOptions);
        Console.WriteLine($"Using database: {databaseOptions.DatabasePath}");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite(databaseSettings.ConnectionString);

        using var dbContext = new AppDbContext(optionsBuilder.Options);
        dbContext.Database.Migrate();

        var dictionaryPath = Path.Combine(repoRoot, SchemaRelativePath);
        var maintenance = new SchemaDictionaryMaintenanceService(dbContext);
        var result = maintenance.ApplyDictionaryDisplayNamesToDatabaseAsync(dictionaryPath).GetAwaiter().GetResult();

        Console.WriteLine("Apply dictionary to database completed.");
        Console.WriteLine($"  Dictionary : {result.DictionaryPath}");
        Console.WriteLine($"  Updated    : {result.UpdatedFields}");
        Console.WriteLine($"  Created    : {result.CreatedFields}");
        Console.WriteLine($"  Skipped    : {result.SkippedEntries}");
        Console.WriteLine($"  Summary    : {result.Summary}");
        return 0;
    }

    private static string ResolveAppOutputDirectory(string repoRoot)
    {
        var candidates = new[]
        {
            Path.Combine(repoRoot, "bin", "Debug", "net8.0-windows"),
            Path.Combine(repoRoot, "bin", "Release", "net8.0-windows"),
            repoRoot
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "appsettings.json")))
            {
                return candidate;
            }
        }

        return repoRoot;
    }
    private static string ResolveRepoRoot(string[] args)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], "--repo-root", StringComparison.OrdinalIgnoreCase))
            {
                var explicitRoot = Path.GetFullPath(args[index + 1]);
                if (!File.Exists(Path.Combine(explicitRoot, "DocMgr.sln")))
                {
                    throw new InvalidOperationException($"DocMgr.sln not found under: {explicitRoot}");
                }

                return explicitRoot;
            }
        }

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "DocMgr.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root containing DocMgr.sln.");
    }
}
