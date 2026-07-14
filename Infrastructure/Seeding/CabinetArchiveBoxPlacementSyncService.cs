using System;
using System.Collections.Generic;
using System.Linq;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HistoryArchive;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Infrastructure.Seeding;

public static class CabinetArchiveBoxPlacementSyncService
{
    private const string DefaultPlacementMode = "SpineOut";
    private const string MixedSourceType = "Mixed";
    private const string SystemUpdater = "System";
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// 将历史存档资料中的档案盒位置与规格同步到统一摆放登记表。
    /// </summary>
    public static void SyncHistoryArchivePlacements(ICabinetArchiveBoxPlacementSyncRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        string nowText = DateTime.Now.ToString(TimestampFormat);
        var placementLookup = repository.GetPlacements()
            .ToDictionary(item => item.BoxCode, StringComparer.OrdinalIgnoreCase);

        bool changed = false;
        foreach (var seed in BuildPlacementSeeds(repository)
            .GroupBy(item => item.BoxCode, StringComparer.OrdinalIgnoreCase)
            .Select(CreatePlacementSnapshot))
        {
            if (!placementLookup.TryGetValue(seed.BoxCode, out var entity))
            {
                repository.AddPlacement(new CabinetArchiveBoxPlacement
                {
                    BoxCode = seed.BoxCode,
                    BoxSpecification = seed.BoxSpecification,
                    CabinetName = seed.CabinetName,
                    FaceCode = seed.FaceCode,
                    SlotCode = seed.SlotCode,
                    PlacementMode = DefaultPlacementMode,
                    SourceType = seed.SourceType,
                    SourceRecordKey = seed.SourceRecordKey,
                    CreatedAt = nowText,
                    UpdatedAt = nowText,
                    UpdatedBy = SystemUpdater
                });
                changed = true;
                continue;
            }

            bool entityChanged = false;
            entityChanged |= SetIfDifferent(entity.BoxSpecification, seed.BoxSpecification, value => entity.BoxSpecification = value);
            entityChanged |= SetIfDifferent(entity.CabinetName, seed.CabinetName, value => entity.CabinetName = value);
            entityChanged |= SetIfDifferent(entity.FaceCode, seed.FaceCode, value => entity.FaceCode = value);
            entityChanged |= SetIfDifferent(entity.SlotCode, seed.SlotCode, value => entity.SlotCode = value);
            entityChanged |= SetIfDifferent(entity.SourceType, seed.SourceType, value => entity.SourceType = value);
            entityChanged |= SetIfDifferent(entity.SourceRecordKey, seed.SourceRecordKey, value => entity.SourceRecordKey = value);

            if (string.IsNullOrWhiteSpace(entity.PlacementMode))
            {
                entity.PlacementMode = DefaultPlacementMode;
                entityChanged = true;
            }

            if (string.IsNullOrWhiteSpace(entity.CreatedAt))
            {
                entity.CreatedAt = nowText;
                entityChanged = true;
            }

            if (entityChanged)
            {
                entity.UpdatedAt = nowText;
                if (string.IsNullOrWhiteSpace(entity.UpdatedBy))
                {
                    entity.UpdatedBy = SystemUpdater;
                }

                changed = true;
            }
        }

        if (changed)
        {
            repository.SaveChanges();
        }
    }

    private static bool SetIfDifferent(string currentValue, string targetValue, Action<string> setter)
    {
        ArgumentNullException.ThrowIfNull(setter);

        if (string.Equals(currentValue, targetValue, StringComparison.Ordinal))
        {
            return false;
        }

        setter(targetValue);
        return true;
    }

    private static IEnumerable<PlacementSeed> BuildPlacementSeeds(ICabinetArchiveBoxPlacementSyncRepository repository)
    {
        foreach (var seed in repository.GetTopoMaps().AsEnumerable().SelectMany(ExpandTopoMapPlacements))
        {
            yield return seed;
        }

        foreach (var seed in repository.GetAerialPhotos().AsEnumerable().SelectMany(ExpandAerialPhotoPlacements))
        {
            yield return seed;
        }

        foreach (var seed in repository.GetOtherMaps().AsEnumerable().SelectMany(ExpandOtherMapPlacements))
        {
            yield return seed;
        }
    }

    private static IEnumerable<PlacementSeed> ExpandTopoMapPlacements(TopoMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        foreach (var parsed in EnumerateParsedArchiveBoxes(map.BoxNumber))
        {
            yield return new PlacementSeed(
                parsed.BoxCode,
                map.BoxSpecification?.Trim() ?? string.Empty,
                parsed.CabinetName,
                parsed.Face.ToString(),
                parsed.SlotCode,
                "TopoMap",
                map.Id.ToString());
        }
    }

    private static IEnumerable<PlacementSeed> ExpandAerialPhotoPlacements(AerialPhoto photo)
    {
        ArgumentNullException.ThrowIfNull(photo);

        foreach (var parsed in EnumerateParsedArchiveBoxes(photo.BoxNumber))
        {
            yield return new PlacementSeed(
                parsed.BoxCode,
                photo.BoxSpecification?.Trim() ?? string.Empty,
                parsed.CabinetName,
                parsed.Face.ToString(),
                parsed.SlotCode,
                "AerialPhoto",
                photo.Id.ToString());
        }
    }

    private static IEnumerable<PlacementSeed> ExpandOtherMapPlacements(OtherMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        foreach (var parsed in EnumerateParsedArchiveBoxes(map.BoxNumber))
        {
            yield return new PlacementSeed(
                parsed.BoxCode,
                map.BoxSpecification?.Trim() ?? string.Empty,
                parsed.CabinetName,
                parsed.Face.ToString(),
                parsed.SlotCode,
                "OtherMap",
                map.Id.ToString());
        }
    }

    private static PlacementSeed CreatePlacementSnapshot(IGrouping<string, PlacementSeed> group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var first = group.First();
        string boxSpecification = group
            .Select(item => item.BoxSpecification)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
            ?? string.Empty;
        var sourceTypes = group
            .Select(item => item.SourceType)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(text => text, StringComparer.OrdinalIgnoreCase)
            .ToList();
        string sourceType = sourceTypes.Count switch
        {
            0 => string.Empty,
            1 => sourceTypes[0],
            _ => MixedSourceType
        };
        string sourceRecordKey = string.Join("|", group
            .Select(item => $"{item.SourceType}:{item.SourceRecordKey}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(text => text, StringComparer.OrdinalIgnoreCase));

        return first with
        {
            BoxSpecification = boxSpecification,
            SourceType = sourceType,
            SourceRecordKey = sourceRecordKey
        };
    }

    private static IEnumerable<ParsedArchiveBox> EnumerateParsedArchiveBoxes(string? source)
    {
        foreach (var boxCode in SplitArchiveBoxCodes(source))
        {
            var parsed = ParseArchiveBox(boxCode);
            if (parsed != null)
            {
                yield return parsed;
            }
        }
    }

    private static IEnumerable<string> SplitArchiveBoxCodes(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return Enumerable.Empty<string>();
        }

        return source
            .Split([';', '；', ',', '，', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static ParsedArchiveBox? ParseArchiveBox(string? boxCode)
    {
        if (string.IsNullOrWhiteSpace(boxCode))
        {
            return null;
        }

        var segments = boxCode.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 4)
        {
            return null;
        }

        var cabinetAndFace = segments[0];
        if (cabinetAndFace.Length < 2)
        {
            return null;
        }

        CabinetFace? face = cabinetAndFace[^1] switch
        {
            'A' or 'a' => CabinetFace.A,
            'B' or 'b' => CabinetFace.B,
            _ => null
        };

        if (face == null
            || !int.TryParse(segments[1], out var layerIndex)
            || !int.TryParse(segments[2], out var columnIndex))
        {
            return null;
        }

        return new ParsedArchiveBox(
            CabinetNameNormalizer.Normalize(cabinetAndFace[..^1]),
            face.Value,
            $"{layerIndex}-{columnIndex}",
            boxCode.Trim());
    }

    private sealed record PlacementSeed(
        string BoxCode,
        string BoxSpecification,
        string CabinetName,
        string FaceCode,
        string SlotCode,
        string SourceType,
        string SourceRecordKey);

    private sealed record ParsedArchiveBox(
        string CabinetName,
        CabinetFace Face,
        string SlotCode,
        string BoxCode);
}
