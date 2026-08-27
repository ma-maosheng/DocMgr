using DocMgr.Models.HistoryArchive;

namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 历史存档盒内容行：对应 TopoMap / AerialPhoto / OtherMap 台账表的实际展示字段。
    /// </summary>
    public sealed class HistoryArchiveBoxContentFields
    {
        public const string SourceTopoMap = "地形图";
        public const string SourceAerialPhoto = "航摄影像";
        public const string SourceOtherMap = "其他图件";

        public string Scale { get; init; } = string.Empty;

        public string MapNumber { get; init; } = string.Empty;

        public string CurrentMapNumber { get; init; } = string.Empty;

        public string MapName { get; init; } = string.Empty;

        public int SheetCount { get; init; }

        public string CreationDate { get; init; } = string.Empty;

        public string SurveyDate { get; init; } = string.Empty;

        public string CoordinateSystem { get; init; } = string.Empty;

        public string ElevationDatum { get; init; } = string.Empty;

        public string Region { get; init; } = string.Empty;

        public string Category { get; init; } = string.Empty;

        public string SurveyArea { get; init; } = string.Empty;

        public string PhotographyDate { get; init; } = string.Empty;

        public string BoxContents { get; init; } = string.Empty;

        public int PhotoCount { get; init; }

        public string SequenceNumber { get; init; } = string.Empty;

        public string MaterialCategory { get; init; } = string.Empty;

        public string StartYear { get; init; } = string.Empty;

        public string EndYear { get; init; } = string.Empty;

        public string BoxSpecification { get; init; } = string.Empty;

        public string Registrant { get; init; } = string.Empty;

        public string RegistrationDate { get; init; } = string.Empty;

        public string Remark { get; init; } = string.Empty;

        public static HistoryArchiveBoxContentFields FromTopoMap(TopoMap map, string currentMapNumber)
        {
            ArgumentNullException.ThrowIfNull(map);
            return new HistoryArchiveBoxContentFields
            {
                Scale = Trim(map.Scale),
                MapNumber = Trim(map.MapNumber),
                CurrentMapNumber = Trim(currentMapNumber),
                MapName = Trim(map.MapName),
                SheetCount = Math.Max(0, map.SheetCount),
                CreationDate = Trim(map.CreationDate),
                SurveyDate = Trim(map.SurveyDate),
                CoordinateSystem = Trim(map.CoordinateSystem),
                ElevationDatum = Trim(map.ElevationDatum),
                Region = Trim(map.Region),
                BoxSpecification = Trim(map.BoxSpecification),
                Registrant = Trim(map.Registrant),
                RegistrationDate = Trim(map.RegistrationDate),
                Remark = Trim(map.Remark),
            };
        }

        public static HistoryArchiveBoxContentFields FromAerialPhoto(AerialPhoto photo)
        {
            ArgumentNullException.ThrowIfNull(photo);
            return new HistoryArchiveBoxContentFields
            {
                Category = Trim(photo.Category),
                SurveyArea = Trim(photo.SurveyArea),
                Scale = Trim(photo.Scale),
                PhotographyDate = Trim(photo.PhotographyDate),
                BoxContents = Trim(photo.BoxContents),
                PhotoCount = Math.Max(0, photo.PhotoCount),
                BoxSpecification = Trim(photo.BoxSpecification),
                Registrant = Trim(photo.Registrant),
                RegistrationDate = Trim(photo.RegistrationDate),
                Remark = Trim(photo.Remark),
            };
        }

        public static HistoryArchiveBoxContentFields FromOtherMap(OtherMap map)
        {
            ArgumentNullException.ThrowIfNull(map);
            return new HistoryArchiveBoxContentFields
            {
                SequenceNumber = Trim(map.SequenceNumber),
                MaterialCategory = Trim(map.MaterialCategory),
                StartYear = Trim(map.StartYear),
                EndYear = Trim(map.EndYear),
                MapName = Trim(map.MapName),
                BoxSpecification = Trim(map.BoxSpecification),
                Registrant = Trim(map.Registrant),
                RegistrationDate = Trim(map.RegistrationDate),
                Remark = Trim(map.Remark),
            };
        }

        public static bool IsTopoMap(string? sourceType) =>
            string.Equals(sourceType?.Trim(), SourceTopoMap, StringComparison.OrdinalIgnoreCase);

        public static bool IsAerialPhoto(string? sourceType) =>
            string.Equals(sourceType?.Trim(), SourceAerialPhoto, StringComparison.OrdinalIgnoreCase);

        public static bool IsOtherMap(string? sourceType) =>
            string.Equals(sourceType?.Trim(), SourceOtherMap, StringComparison.OrdinalIgnoreCase);

        private static string Trim(string? value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
