using System.Globalization;
using System.Text.RegularExpressions;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 本机物理磁盘硬件字段规范化：序列号、品牌、容量、硬盘类型与接口类型映射。
    /// </summary>
    public static class LocalPhysicalDiskHardwareSupport
    {
        public const string DiskTypeHdd = "机械硬盘";
        public const string DiskTypeSsd = "固态硬盘";
        public const string DiskTypePortable = "移动硬盘";
        public const string DiskTypeOther = "其他";

        public const string InterfaceSata = "SATA";
        public const string InterfaceSas = "SAS";
        public const string InterfaceUsb = "USB";
        public const string InterfaceTypeC = "Type-C";
        public const string InterfaceOther = "其他";

        /// <summary>
        /// 去掉 WMI 常见填充空格，得到可入库的序列号。
        /// </summary>
        public static string NormalizeSerialNumber(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            string trimmed = raw.Trim().Trim('"', '\'');
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1 && parts.All(part => part.Length == 1))
            {
                return string.Concat(parts);
            }

            int spaceCount = trimmed.Count(ch => ch == ' ');
            if (spaceCount > 0 && spaceCount * 2 >= trimmed.Length)
            {
                return trimmed.Replace(" ", string.Empty, StringComparison.Ordinal);
            }

            return trimmed;
        }

        /// <summary>
        /// 从型号/厂商解析品牌。
        /// </summary>
        public static string ResolveBrand(string? model, string? manufacturer)
        {
            string combined = $"{manufacturer} {model}".Trim();
            if (string.IsNullOrWhiteSpace(combined))
            {
                return string.Empty;
            }

            if (LooksLikeGenericManufacturer(manufacturer) && !string.IsNullOrWhiteSpace(model))
            {
                combined = model.Trim();
            }

            string upper = combined.ToUpperInvariant();
            foreach (var pair in BrandKeywords)
            {
                if (upper.Contains(pair.Key, StringComparison.Ordinal))
                {
                    return pair.Value;
                }
            }

            if (Regex.IsMatch(combined, @"\bST\d", RegexOptions.IgnoreCase))
            {
                return "Seagate";
            }

            var match = Regex.Match(combined.Trim(), @"^(?<brand>[A-Za-z][A-Za-z0-9+\-]+)");
            if (match.Success)
            {
                string token = match.Groups["brand"].Value;
                if (!string.Equals(token, "SSD", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(token, "HDD", StringComparison.OrdinalIgnoreCase)
                    && !LooksLikeGenericManufacturer(token))
                {
                    return token;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 将字节容量转换为登记用数值与单位（优先贴近标称 TB/GB）。
        /// </summary>
        public static void FormatCapacity(ulong bytes, out string value, out string unit, out string text)
        {
            value = string.Empty;
            unit = ElectronicMediaCapacitySupport.DefaultCapacityUnit;
            text = string.Empty;

            if (bytes == 0)
            {
                return;
            }

            double tbSi = bytes / 1_000_000_000_000d;
            int[] tbLabels = [1, 2, 3, 4, 5, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32];
            foreach (int label in tbLabels)
            {
                if (tbSi >= label * 0.90 && tbSi < label * 1.05)
                {
                    value = label.ToString(CultureInfo.InvariantCulture);
                    unit = "TB";
                    text = ElectronicMediaCapacitySupport.CombineCapacityText(value, unit);
                    return;
                }
            }

            double gbSi = bytes / 1_000_000_000d;
            int[] gbLabels = [64, 120, 128, 240, 250, 256, 480, 500, 512, 960, 1000, 1024, 1500, 2000, 2048];
            foreach (int label in gbLabels)
            {
                if (gbSi >= label * 0.90 && gbSi < label * 1.05)
                {
                    if (label >= 1000)
                    {
                        value = (label / 1000).ToString(CultureInfo.InvariantCulture);
                        unit = "TB";
                    }
                    else
                    {
                        value = label.ToString(CultureInfo.InvariantCulture);
                        unit = "GB";
                    }

                    text = ElectronicMediaCapacitySupport.CombineCapacityText(value, unit);
                    return;
                }
            }

            decimal capacityMb = bytes / (1024m * 1024m);
            text = ElectronicMediaCapacitySupport.FormatCapacityMb(capacityMb);
            if (string.Equals(text, "—", StringComparison.Ordinal))
            {
                text = string.Empty;
                return;
            }

            ElectronicMediaCapacitySupport.TrySplitCapacityText(text, out value, out unit);
        }

        /// <summary>
        /// 按总线与介质类型映射硬盘类型域值。USB 外置优先归为移动硬盘。
        /// </summary>
        public static string ResolveDiskType(string? busType, int mediaType, string? model, string? win32MediaType)
        {
            if (IsUsbBus(busType))
            {
                return DiskTypePortable;
            }

            if (mediaType == 4 || ContainsSsdHint(model, win32MediaType))
            {
                return DiskTypeSsd;
            }

            if (mediaType == 3 || ContainsHddHint(model, win32MediaType))
            {
                return DiskTypeHdd;
            }

            if (ContainsSsdHint(model, win32MediaType))
            {
                return DiskTypeSsd;
            }

            return DiskTypeOther;
        }

        /// <summary>
        /// 按总线类型映射接口类型域值。
        /// </summary>
        public static string ResolveInterfaceType(string? busType, string? win32InterfaceType)
        {
            string normalizedBus = (busType ?? string.Empty).Trim();
            if (IsUsbBus(normalizedBus) || ContainsOrdinal(win32InterfaceType, "USB"))
            {
                return InterfaceUsb;
            }

            if (ContainsOrdinal(normalizedBus, "SAS") || ContainsOrdinal(win32InterfaceType, "SAS"))
            {
                return InterfaceSas;
            }

            if (ContainsOrdinal(normalizedBus, "SATA")
                || ContainsOrdinal(normalizedBus, "ATA")
                || ContainsOrdinal(win32InterfaceType, "SATA")
                || ContainsOrdinal(win32InterfaceType, "IDE")
                || ContainsOrdinal(win32InterfaceType, "SCSI")
                || ContainsOrdinal(win32InterfaceType, "HDC"))
            {
                return InterfaceSata;
            }

            return InterfaceOther;
        }

        /// <summary>
        /// 将映射值对齐到已启用域选项；对不上则取「其他」或首项。
        /// </summary>
        public static string MatchDomainOption(IReadOnlyList<string> options, string? mappedValue)
        {
            ArgumentNullException.ThrowIfNull(options);

            string trimmed = mappedValue?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                string? exact = options.FirstOrDefault(item =>
                    string.Equals(item?.Trim(), trimmed, StringComparison.Ordinal));
                if (!string.IsNullOrWhiteSpace(exact))
                {
                    return exact;
                }
            }

            string? other = options.FirstOrDefault(item =>
                string.Equals(item?.Trim(), DiskTypeOther, StringComparison.Ordinal)
                || string.Equals(item?.Trim(), InterfaceOther, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(other))
            {
                return other;
            }

            return options.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item))?.Trim()
                ?? trimmed;
        }

        public static bool IsUsbBus(string? busType)
        {
            if (string.IsNullOrWhiteSpace(busType))
            {
                return false;
            }

            string normalized = busType.Trim();
            return string.Equals(normalized, "USB", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "7", StringComparison.Ordinal);
        }

        public static bool IsVirtualBus(string? busType)
        {
            if (string.IsNullOrWhiteSpace(busType))
            {
                return false;
            }

            string normalized = busType.Trim();
            return ContainsOrdinal(normalized, "FileBackedVirtual")
                || ContainsOrdinal(normalized, "StorageSpaces")
                || string.Equals(normalized, "15", StringComparison.Ordinal)
                || string.Equals(normalized, "16", StringComparison.Ordinal)
                || string.Equals(normalized, "18", StringComparison.Ordinal);
        }

        /// <summary>
        /// 将制造日期或制造年份解析为出厂日期。
        /// 有完整制造日期时沿用该日期；仅有年份时取当年 1 月 1 日。
        /// </summary>
        public static DateTime? ResolveFactoryDate(
            DateTime? hardwareManufactureDate,
            string? serialNumber,
            string? firmwareRevision)
        {
            if (TryNormalizeManufactureDate(hardwareManufactureDate, out DateTime normalizedDate))
            {
                return normalizedDate;
            }

            if (TryResolveManufactureYear(serialNumber, firmwareRevision, out int year))
            {
                return new DateTime(year, 1, 1);
            }

            return null;
        }

        /// <summary>
        /// 从序列号或固件版本中解析制造年份（2005 至明年）。
        /// 不解析型号，避免把容量数字（如 ST2000）当成年份。
        /// </summary>
        public static bool TryResolveManufactureYear(string? serialNumber, string? firmwareRevision, out int year)
        {
            if (TryResolveManufactureYearFromText(serialNumber, allowYyww: true, out year))
            {
                return true;
            }

            return TryResolveManufactureYearFromText(firmwareRevision, allowYyww: false, out year);
        }

        private static bool TryNormalizeManufactureDate(DateTime? hardwareManufactureDate, out DateTime normalizedDate)
        {
            normalizedDate = default;
            if (hardwareManufactureDate == null)
            {
                return false;
            }

            DateTime value = hardwareManufactureDate.Value.Date;
            int year = value.Year;
            if (!IsPlausibleManufactureYear(year) || value > DateTime.Today.AddDays(1))
            {
                return false;
            }

            normalizedDate = value;
            return true;
        }

        private static bool TryResolveManufactureYearFromText(string? source, bool allowYyww, out int year)
        {
            year = 0;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            string text = NormalizeSerialNumber(source);
            if (text.Length == 0)
            {
                return false;
            }

            var fullYears = new List<int>();
            foreach (Match match in Regex.Matches(text, @"20[0-2]\d"))
            {
                if (int.TryParse(match.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int fullYear)
                    && IsPlausibleManufactureYear(fullYear)
                    && !IsCapacityLikeNumber(match.Value))
                {
                    fullYears.Add(fullYear);
                }
            }

            if (TryPickUniqueYear(fullYears, out year))
            {
                return true;
            }

            if (!allowYyww)
            {
                return false;
            }

            var yywwYears = new List<int>();
            foreach (Match match in Regex.Matches(text, @"\d{4}"))
            {
                if (int.TryParse(match.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int fullYear)
                    && match.Value.StartsWith("20", StringComparison.Ordinal)
                    && IsPlausibleManufactureYear(fullYear))
                {
                    continue;
                }

                if (TryParseYywwYear(match.Value, out int yywwYear))
                {
                    yywwYears.Add(yywwYear);
                }
            }

            if (yywwYears.Count == 0)
            {
                return false;
            }

            year = yywwYears[^1];
            return true;
        }

        private static bool TryPickUniqueYear(List<int> years, out int year)
        {
            year = 0;
            IReadOnlyList<int> distinct = years.Distinct().ToList();
            if (distinct.Count != 1)
            {
                return false;
            }

            year = distinct[0];
            return true;
        }

        private static bool TryParseYywwYear(string digits, out int year)
        {
            year = 0;
            if (digits.Length != 4 || IsCapacityLikeNumber(digits))
            {
                return false;
            }

            if (!int.TryParse(digits.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int yy)
                || !int.TryParse(digits.AsSpan(2, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int ww))
            {
                return false;
            }

            if (ww is < 1 or > 53)
            {
                return false;
            }

            int mappedYear = 2000 + yy;
            if (!IsPlausibleManufactureYear(mappedYear))
            {
                return false;
            }

            year = mappedYear;
            return true;
        }

        private static bool IsPlausibleManufactureYear(int year)
        {
            int maxYear = DateTime.Today.Year + 1;
            return year >= 2005 && year <= maxYear;
        }

        private static bool IsCapacityLikeNumber(string digits)
        {
            return digits is "0128" or "0256" or "0512" or "0640"
                or "1000" or "1024" or "1500" or "1600"
                or "2000" or "2048" or "2500" or "3000"
                or "3200" or "4000" or "4096" or "5000"
                or "6000" or "8000" or "8192";
        }

        public static string MapMsftBusType(int busType)
        {
            return busType switch
            {
                7 => "USB",
                10 => "SAS",
                11 => "SATA",
                3 => "ATA",
                1 => "SCSI",
                17 => "NVMe",
                8 => "RAID",
                15 => "FileBackedVirtual",
                16 => "StorageSpaces",
                18 => "FileBackedVirtual",
                _ => busType.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static bool ContainsSsdHint(string? model, string? mediaType)
            => ContainsOrdinal(model, "SSD") || ContainsOrdinal(mediaType, "SSD") || ContainsOrdinal(mediaType, "Solid");

        private static bool ContainsHddHint(string? model, string? mediaType)
            => ContainsOrdinal(model, "HDD")
            || ContainsOrdinal(mediaType, "Fixed")
            || ContainsOrdinal(mediaType, "Hard disk");

        private static bool ContainsOrdinal(string? source, string keyword)
            => !string.IsNullOrWhiteSpace(source)
            && source.Contains(keyword, StringComparison.OrdinalIgnoreCase);

        private static bool LooksLikeGenericManufacturer(string? manufacturer)
        {
            if (string.IsNullOrWhiteSpace(manufacturer))
            {
                return true;
            }

            string normalized = manufacturer.Trim();
            return normalized.Contains("Standard", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("Generic", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "(Standard disk drives)", StringComparison.OrdinalIgnoreCase);
        }

        private static readonly (string Key, string Value)[] BrandKeywords =
        [
            ("WESTERN DIGITAL", "Western Digital"),
            ("WDC", "Western Digital"),
            ("WD ", "Western Digital"),
            ("WD_", "Western Digital"),
            ("SEAGATE", "Seagate"),
            ("TOSHIBA", "Toshiba"),
            ("KIOXIA", "Kioxia"),
            ("SAMSUNG", "Samsung"),
            ("HITACHI", "Hitachi"),
            ("HGST", "HGST"),
            ("INTEL", "Intel"),
            ("CRUCIAL", "Crucial"),
            ("MICRON", "Micron"),
            ("KINGSTON", "Kingston"),
            ("SANDISK", "SanDisk"),
            ("HYNIX", "SK hynix"),
            ("ADATA", "ADATA"),
            ("LACIE", "LaCie"),
            ("TRANSCEND", "Transcend"),
            ("SILICON POWER", "Silicon Power"),
            ("LEXAR", "Lexar"),
            ("CORSAIR", "Corsair"),
            ("PIONEER", "Pioneer"),
            ("MAXTOR", "Maxtor"),
            ("FUJITSU", "Fujitsu"),
            ("LENOVO", "Lenovo"),
            ("HEWLETT", "HP"),
            ("HP ", "HP")
        ];
    }
}
