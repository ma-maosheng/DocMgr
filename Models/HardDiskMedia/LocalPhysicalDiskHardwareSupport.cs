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
        public const string InterfaceNvme = "NVMe";
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
        /// USB 桥接且未能确认盘体 Identify 时，不采用主机上报的序列号/品牌/接口。
        /// </summary>
        public static bool IsTrustedInnerDiskIdentity(LocalPhysicalDiskIdentifySnapshot? identify)
        {
            if (identify == null
                || string.IsNullOrWhiteSpace(identify.SerialNumber)
                || string.IsNullOrWhiteSpace(identify.Model)
                || LooksLikeUsbBridgeIdentity(identify.Model))
            {
                return false;
            }

            string brand = ResolveBrand(identify.Model, null);
            return !string.IsNullOrWhiteSpace(brand) && !LooksLikeUsbBridgeIdentity(brand);
        }

        /// <summary>
        /// 主机侧是否为 USB 连接（含 UASP 在 Win32 上报 SCSI 的情况）。
        /// </summary>
        public static bool IsUsbAttachedDisk(string? msftBusTypeName, string? win32InterfaceType, string? pnpDeviceId)
        {
            if (IsUsbBus(msftBusTypeName) || IsUsbBus(win32InterfaceType) || ContainsOrdinal(win32InterfaceType, "USB"))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(pnpDeviceId))
            {
                return false;
            }

            string pnp = pnpDeviceId.Trim();
            return pnp.StartsWith("USB", StringComparison.OrdinalIgnoreCase)
                || pnp.Contains("USBSTOR", StringComparison.OrdinalIgnoreCase)
                || pnp.Contains(@"\USB\", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 按总线与介质类型映射硬盘类型域值。
        /// 已确认盘体 Identify 时优先用盘体类型；USB 外置在未确认盘体时不映射为移动硬盘。
        /// </summary>
        public static string ResolveDiskType(
            string? busType,
            int mediaType,
            string? model,
            string? win32MediaType,
            string? identifyDiskType = null)
        {
            if (!string.IsNullOrWhiteSpace(identifyDiskType))
            {
                return identifyDiskType.Trim();
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

            return string.Empty;
        }

        /// <summary>
        /// 按总线类型映射接口类型域值。
        /// 盘体 Identify 给出的 SATA/NVMe/SAS 优先于 USB 桥的 Win32 接口。
        /// </summary>
        public static string ResolveInterfaceType(string? busType, string? win32InterfaceType)
        {
            string normalizedBus = (busType ?? string.Empty).Trim();
            if (ContainsOrdinal(normalizedBus, "NVMe"))
            {
                return InterfaceNvme;
            }

            if (ContainsOrdinal(normalizedBus, "SAS"))
            {
                return InterfaceSas;
            }

            if (ContainsOrdinal(normalizedBus, "SATA") || ContainsOrdinal(normalizedBus, "ATA"))
            {
                return InterfaceSata;
            }

            if (ContainsOrdinal(win32InterfaceType, "SAS"))
            {
                return InterfaceSas;
            }

            if (ContainsOrdinal(win32InterfaceType, "NVMe"))
            {
                return InterfaceNvme;
            }

            if (ContainsOrdinal(win32InterfaceType, "SATA")
                || ContainsOrdinal(win32InterfaceType, "IDE")
                || ContainsOrdinal(win32InterfaceType, "SCSI")
                || ContainsOrdinal(win32InterfaceType, "HDC"))
            {
                return InterfaceSata;
            }

            return string.Empty;
        }

        /// <summary>
        /// 解析 ATA IDENTIFY DEVICE 512 字节缓冲。无效数据返回 null。
        /// </summary>
        public static LocalPhysicalDiskIdentifySnapshot? TryParseAtaIdentify(byte[]? identify)
        {
            if (identify == null || identify.Length < 512)
            {
                return null;
            }

            if (IsBufferEmpty(identify, 512))
            {
                return null;
            }

            string serial = NormalizeSerialNumber(ReadAtaString(identify, 10, 10));
            string firmware = NormalizeSerialNumber(ReadAtaString(identify, 23, 4));
            string model = ReadAtaString(identify, 27, 20).Trim();
            if (!LooksLikeDriveIdentity(serial, model))
            {
                return null;
            }

            string diskType = ResolveDiskTypeFromAtaIdentify(identify, model);
            return new LocalPhysicalDiskIdentifySnapshot
            {
                SerialNumber = serial,
                Model = model,
                Firmware = firmware,
                BusTypeName = "SATA",
                DiskType = diskType
            };
        }

        /// <summary>
        /// 解析 NVMe Identify Controller 4096 字节缓冲。无效数据返回 null。
        /// </summary>
        public static LocalPhysicalDiskIdentifySnapshot? TryParseNvmeIdentifyController(byte[]? identify)
        {
            if (identify == null || identify.Length < 72)
            {
                return null;
            }

            string serial = NormalizeSerialNumber(ReadAsciiFixed(identify, 4, 20));
            string model = ReadAsciiFixed(identify, 24, 40).Trim();
            string firmware = NormalizeSerialNumber(ReadAsciiFixed(identify, 64, 8));
            if (!LooksLikeDriveIdentity(serial, model))
            {
                return null;
            }

            return new LocalPhysicalDiskIdentifySnapshot
            {
                SerialNumber = serial,
                Model = model,
                Firmware = firmware,
                BusTypeName = "NVMe",
                DiskType = DiskTypeSsd
            };
        }

        /// <summary>
        /// 将映射值对齐到已启用域选项；对不上则保留映射原文，供可输入下拉新增。
        /// </summary>
        public static string MatchDomainOption(IReadOnlyList<string> options, string? mappedValue)
        {
            ArgumentNullException.ThrowIfNull(options);

            string trimmed = mappedValue?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed)
                || string.Equals(trimmed, DiskTypeOther, StringComparison.Ordinal)
                || string.Equals(trimmed, InterfaceOther, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            string? exact = options.FirstOrDefault(item =>
                string.Equals(item?.Trim(), trimmed, StringComparison.Ordinal));
            return string.IsNullOrWhiteSpace(exact) ? trimmed : exact;
        }

        private static bool LooksLikeUsbBridgeIdentity(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string upper = text.Trim().ToUpperInvariant();
            return upper.Contains("USB", StringComparison.Ordinal)
                || upper.Contains("JMICRON", StringComparison.Ordinal)
                || upper.Contains("JMS", StringComparison.Ordinal)
                || upper.Contains("ASMEDIA", StringComparison.Ordinal)
                || upper.Contains("ASM1", StringComparison.Ordinal)
                || upper.Contains("ASM2", StringComparison.Ordinal)
                || upper.Contains("REALTEK", StringComparison.Ordinal)
                || upper.Contains("INITIO", StringComparison.Ordinal)
                || upper.Contains("INIC", StringComparison.Ordinal)
                || upper.Contains("VIA LABS", StringComparison.Ordinal)
                || upper.Contains("VL81", StringComparison.Ordinal)
                || upper.Contains("GENERIC", StringComparison.Ordinal);
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

        private static string ResolveDiskTypeFromAtaIdentify(byte[] identify, string model)
        {
            ushort rotationRate = ReadAtaWord(identify, 217);
            if (rotationRate == 1 || ContainsSsdHint(model, null))
            {
                return DiskTypeSsd;
            }

            if (rotationRate >= 0x0401)
            {
                return DiskTypeHdd;
            }

            if (ContainsHddHint(model, null))
            {
                return DiskTypeHdd;
            }

            return string.Empty;
        }

        private static bool LooksLikeDriveIdentity(string serial, string model)
        {
            if (string.IsNullOrWhiteSpace(model) && string.IsNullOrWhiteSpace(serial))
            {
                return false;
            }

            string identity = $"{model} {serial}";
            int printable = identity.Count(ch => ch is >= (char)0x20 and <= (char)0x7E);
            return printable >= 6;
        }

        private static bool IsBufferEmpty(byte[] buffer, int length)
        {
            int limit = Math.Min(buffer.Length, length);
            for (int i = 0; i < limit; i++)
            {
                if (buffer[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static ushort ReadAtaWord(byte[] identify, int wordIndex)
        {
            int offset = wordIndex * 2;
            if (offset + 1 >= identify.Length)
            {
                return 0;
            }

            return (ushort)(identify[offset] | (identify[offset + 1] << 8));
        }

        private static string ReadAtaString(byte[] identify, int wordOffset, int wordCount)
        {
            int byteCount = wordCount * 2;
            int start = wordOffset * 2;
            if (start + byteCount > identify.Length)
            {
                return string.Empty;
            }

            var chars = new char[byteCount];
            int written = 0;
            for (int i = 0; i < byteCount; i += 2)
            {
                chars[written++] = (char)identify[start + i + 1];
                chars[written++] = (char)identify[start + i];
            }

            return new string(chars).Trim('\0', ' ', '\t');
        }

        private static string ReadAsciiFixed(byte[] buffer, int offset, int length)
        {
            if (offset < 0 || length <= 0 || offset + length > buffer.Length)
            {
                return string.Empty;
            }

            return System.Text.Encoding.ASCII.GetString(buffer, offset, length).Trim('\0', ' ', '\t');
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
