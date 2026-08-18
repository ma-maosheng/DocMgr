using System.Globalization;
using System.IO;
using System.Management;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 通过 WMI 读取本机物理磁盘硬件信息。
    /// </summary>
    public sealed class LocalPhysicalDiskHardwareService : ILocalPhysicalDiskHardwareService
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<LocalPhysicalDiskInfo>> GetPhysicalDisksAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() => (IReadOnlyList<LocalPhysicalDiskInfo>)EnumeratePhysicalDisks(cancellationToken), cancellationToken);
        }

        private static List<LocalPhysicalDiskInfo> EnumeratePhysicalDisks(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyDictionary<int, MsftPhysicalDiskSnapshot> msftDisks = TryLoadMsftPhysicalDisks();
                IReadOnlySet<int> systemDiskIndexes = LoadSystemDiskIndexes();
                IReadOnlyDictionary<int, string> driveLettersByIndex = LoadDriveLettersByDiskIndex();
                IReadOnlyDictionary<int, DateTime> manufactureDatesByIndex = TryLoadPhysicalMediaManufactureDates();

                var disks = new List<LocalPhysicalDiskInfo>();
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Index, DeviceID, Model, SerialNumber, InterfaceType, Size, MediaType, Manufacturer, PNPDeviceID, FirmwareRevision FROM Win32_DiskDrive");
                using ManagementObjectCollection results = searcher.Get();
            foreach (ManagementBaseObject item in results)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var disk = (ManagementObject)item;
                LocalPhysicalDiskInfo? info = MapWin32Disk(
                    disk,
                    msftDisks,
                    systemDiskIndexes,
                    driveLettersByIndex,
                    manufactureDatesByIndex);
                if (info != null)
                {
                    disks.Add(info);
                }
            }

                return disks
                    .OrderBy(item => item.IsSystemDisk)
                    .ThenBy(item => item.DiskIndex)
                    .ToList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ManagementException ex)
            {
                throw new InvalidOperationException($"无法读取本机硬盘信息：{ex.Message}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException("当前账户无权读取本机硬盘信息，请改用手工录入。", ex);
            }
        }

        private static LocalPhysicalDiskInfo? MapWin32Disk(
            ManagementObject disk,
            IReadOnlyDictionary<int, MsftPhysicalDiskSnapshot> msftDisks,
            IReadOnlySet<int> systemDiskIndexes,
            IReadOnlyDictionary<int, string> driveLettersByIndex,
            IReadOnlyDictionary<int, DateTime> manufactureDatesByIndex)
        {
            int index = GetInt32(disk, "Index");
            if (index < 0)
            {
                return null;
            }

            string model = GetString(disk, "Model");
            string manufacturer = GetString(disk, "Manufacturer");
            string win32Serial = LocalPhysicalDiskHardwareSupport.NormalizeSerialNumber(GetString(disk, "SerialNumber"));
            string win32Interface = GetString(disk, "InterfaceType");
            string win32MediaType = GetString(disk, "MediaType");
            string firmwareRevision = GetString(disk, "FirmwareRevision");
            ulong size = GetUInt64(disk, "Size");

            msftDisks.TryGetValue(index, out MsftPhysicalDiskSnapshot? msft);
            string serialNumber = !string.IsNullOrWhiteSpace(msft?.SerialNumber)
                ? msft!.SerialNumber
                : win32Serial;
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                serialNumber = LocalPhysicalDiskHardwareSupport.NormalizeSerialNumber(msft?.SerialNumber);
            }

            string busType = msft?.BusTypeName
                ?? (LocalPhysicalDiskHardwareSupport.IsUsbBus(win32Interface) ? "USB" : win32Interface);
            int mediaType = msft?.MediaType ?? 0;
            if (size == 0 && msft?.Size > 0)
            {
                size = msft.Size;
            }

            if (!string.IsNullOrWhiteSpace(msft?.Model) && string.IsNullOrWhiteSpace(model))
            {
                model = msft!.Model;
            }

            if (string.IsNullOrWhiteSpace(firmwareRevision) && !string.IsNullOrWhiteSpace(msft?.FirmwareVersion))
            {
                firmwareRevision = msft!.FirmwareVersion;
            }

            string brand = LocalPhysicalDiskHardwareSupport.ResolveBrand(model, string.IsNullOrWhiteSpace(msft?.Manufacturer) ? manufacturer : msft!.Manufacturer);
            LocalPhysicalDiskHardwareSupport.FormatCapacity(size, out string capacityValue, out string capacityUnit, out string capacityText);

            bool isSystemDisk = systemDiskIndexes.Contains(index);
            bool isVirtualDisk = LocalPhysicalDiskHardwareSupport.IsVirtualBus(busType);
            DateTime? hardwareManufactureDate = manufactureDatesByIndex.TryGetValue(index, out DateTime manufactureDate)
                ? manufactureDate
                : null;
            DateTime? factoryDate = LocalPhysicalDiskHardwareSupport.ResolveFactoryDate(
                hardwareManufactureDate,
                serialNumber,
                firmwareRevision);
            string statusHint = BuildStatusHint(isSystemDisk, isVirtualDisk, serialNumber);

            driveLettersByIndex.TryGetValue(index, out string? driveLetters);

            return new LocalPhysicalDiskInfo
            {
                DiskIndex = index,
                DeviceId = GetString(disk, "DeviceID"),
                Model = model,
                SerialNumber = serialNumber,
                Brand = brand,
                CapacityValue = capacityValue,
                CapacityUnit = capacityUnit,
                CapacityText = capacityText,
                DiskType = LocalPhysicalDiskHardwareSupport.ResolveDiskType(busType, mediaType, model, win32MediaType),
                InterfaceType = LocalPhysicalDiskHardwareSupport.ResolveInterfaceType(busType, win32Interface),
                DriveLetters = driveLetters ?? string.Empty,
                FactoryDate = factoryDate,
                IsSystemDisk = isSystemDisk,
                IsVirtualDisk = isVirtualDisk,
                StatusHint = statusHint
            };
        }

        private static string BuildStatusHint(bool isSystemDisk, bool isVirtualDisk, string serialNumber)
        {
            if (isSystemDisk)
            {
                return "系统盘，不可用于登记";
            }

            if (isVirtualDisk)
            {
                return "虚拟磁盘，不可用于登记";
            }

            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                return "未读到序列号，回填后需手工补录";
            }

            return string.Empty;
        }

        private static IReadOnlyDictionary<int, MsftPhysicalDiskSnapshot> TryLoadMsftPhysicalDisks()
        {
            var map = new Dictionary<int, MsftPhysicalDiskSnapshot>();
            try
            {
                var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                scope.Connect();
                using var searcher = new ManagementObjectSearcher(
                    scope,
                    new ObjectQuery("SELECT DeviceId, FriendlyName, SerialNumber, MediaType, BusType, Size, Manufacturer, Model, FirmwareVersion FROM MSFT_PhysicalDisk"));
                using ManagementObjectCollection results = searcher.Get();
                foreach (ManagementBaseObject item in results)
                {
                    using var disk = (ManagementObject)item;
                    int deviceId = GetInt32(disk, "DeviceId");
                    if (deviceId < 0)
                    {
                        continue;
                    }

                    int busType = GetInt32(disk, "BusType");
                    map[deviceId] = new MsftPhysicalDiskSnapshot
                    {
                        SerialNumber = LocalPhysicalDiskHardwareSupport.NormalizeSerialNumber(GetString(disk, "SerialNumber")),
                        Model = FirstNonEmpty(GetString(disk, "FriendlyName"), GetString(disk, "Model")),
                        Manufacturer = GetString(disk, "Manufacturer"),
                        MediaType = GetInt32(disk, "MediaType"),
                        BusTypeName = LocalPhysicalDiskHardwareSupport.MapMsftBusType(busType),
                        Size = GetUInt64(disk, "Size"),
                        FirmwareVersion = GetString(disk, "FirmwareVersion")
                    };
                }
            }
            catch (ManagementException)
            {
                return map;
            }
            catch (UnauthorizedAccessException)
            {
                return map;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                return map;
            }

            return map;
        }

        private static Dictionary<int, DateTime> TryLoadPhysicalMediaManufactureDates()
        {
            var dates = new Dictionary<int, DateTime>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Tag, SerialNumber, ManufactureDate FROM Win32_PhysicalMedia");
                using ManagementObjectCollection results = searcher.Get();
                foreach (ManagementBaseObject item in results)
                {
                    using var media = (ManagementObject)item;
                    DateTime? manufactureDate = ParseCimDate(GetString(media, "ManufactureDate"));
                    if (manufactureDate == null)
                    {
                        continue;
                    }

                    int index = ParseDiskIndexFromPath(GetString(media, "Tag"));
                    if (index < 0)
                    {
                        continue;
                    }

                    dates[index] = manufactureDate.Value.Date;
                }
            }
            catch (ManagementException)
            {
                return dates;
            }
            catch (UnauthorizedAccessException)
            {
                return dates;
            }

            return dates;
        }

        private static DateTime? ParseCimDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("0000", StringComparison.Ordinal))
            {
                return null;
            }

            try
            {
                return ManagementDateTimeConverter.ToDateTime(value.Trim());
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
            catch (ManagementException)
            {
                return null;
            }
        }

        private static HashSet<int> LoadSystemDiskIndexes()
        {
            var indexes = new HashSet<int>();
            string systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? string.Empty;
            string driveLetter = systemRoot.TrimEnd('\\', '/');
            if (string.IsNullOrWhiteSpace(driveLetter))
            {
                return indexes;
            }

            try
            {
                using var partitionSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{EscapeWql(driveLetter)}'}} WHERE AssocClass = Win32_LogicalDiskToPartition");
                using ManagementObjectCollection partitions = partitionSearcher.Get();
                foreach (ManagementBaseObject partitionItem in partitions)
                {
                    using var partition = (ManagementObject)partitionItem;
                    string partitionId = GetString(partition, "DeviceID");
                    if (string.IsNullOrWhiteSpace(partitionId))
                    {
                        continue;
                    }

                    using var diskSearcher = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{EscapeWql(partitionId)}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition");
                    using ManagementObjectCollection disks = diskSearcher.Get();
                    foreach (ManagementBaseObject diskItem in disks)
                    {
                        using var disk = (ManagementObject)diskItem;
                        int index = GetInt32(disk, "Index");
                        if (index >= 0)
                        {
                            indexes.Add(index);
                        }
                    }
                }
            }
            catch (ManagementException)
            {
                return indexes;
            }

            return indexes;
        }

        private static Dictionary<int, string> LoadDriveLettersByDiskIndex()
        {
            var lettersByIndex = new Dictionary<int, List<string>>();
            try
            {
                using var linkSearcher = new ManagementObjectSearcher("SELECT Antecedent, Dependent FROM Win32_DiskDriveToDiskPartition");
                using ManagementObjectCollection driveLinks = linkSearcher.Get();
                var partitionToDisk = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (ManagementBaseObject item in driveLinks)
                {
                    using var link = (ManagementObject)item;
                    string diskPath = GetString(link, "Antecedent");
                    string partitionPath = GetString(link, "Dependent");
                    int index = ParseDiskIndexFromPath(diskPath);
                    string partitionId = ParseQuotedValue(partitionPath);
                    if (index >= 0 && !string.IsNullOrWhiteSpace(partitionId))
                    {
                        partitionToDisk[partitionId] = index;
                    }
                }

                using var letterSearcher = new ManagementObjectSearcher("SELECT Antecedent, Dependent FROM Win32_LogicalDiskToPartition");
                using ManagementObjectCollection letterLinks = letterSearcher.Get();
                foreach (ManagementBaseObject item in letterLinks)
                {
                    using var link = (ManagementObject)item;
                    string partitionPath = GetString(link, "Antecedent");
                    string logicalPath = GetString(link, "Dependent");
                    string partitionId = ParseQuotedValue(partitionPath);
                    string letter = ParseQuotedValue(logicalPath);
                    if (string.IsNullOrWhiteSpace(partitionId)
                        || string.IsNullOrWhiteSpace(letter)
                        || !partitionToDisk.TryGetValue(partitionId, out int index))
                    {
                        continue;
                    }

                    if (!lettersByIndex.TryGetValue(index, out List<string>? letters))
                    {
                        letters = new List<string>();
                        lettersByIndex[index] = letters;
                    }

                    if (!letters.Contains(letter, StringComparer.OrdinalIgnoreCase))
                    {
                        letters.Add(letter);
                    }
                }
            }
            catch (ManagementException)
            {
                return new Dictionary<int, string>();
            }

            return lettersByIndex.ToDictionary(
                pair => pair.Key,
                pair => string.Join("、", pair.Value.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)));
        }

        private static int ParseDiskIndexFromPath(string path)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                path ?? string.Empty,
                @"PHYSICALDRIVE(?<index>\d+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups["index"].Value, out int index))
            {
                return index;
            }

            return -1;
        }

        private static string ParseQuotedValue(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            int start = path.IndexOf('"', StringComparison.Ordinal);
            int end = path.LastIndexOf('"');
            if (start >= 0 && end > start)
            {
                return path.Substring(start + 1, end - start - 1).Trim();
            }

            return string.Empty;
        }

        private static string FirstNonEmpty(string first, string second)
            => !string.IsNullOrWhiteSpace(first) ? first : second;

        private static string EscapeWql(string value)
            => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);

        private static object? GetProperty(ManagementBaseObject obj, string name)
        {
            try
            {
                return obj[name];
            }
            catch (ManagementException)
            {
                return null;
            }
        }

        private static string GetString(ManagementBaseObject obj, string name)
        {
            object? value = GetProperty(obj, name);
            return value?.ToString()?.Trim() ?? string.Empty;
        }

        private static int GetInt32(ManagementBaseObject obj, string name)
        {
            object? value = GetProperty(obj, name);
            if (value == null)
            {
                return -1;
            }

            try
            {
                return Convert.ToInt32(value, CultureInfoInvariant);
            }
            catch (FormatException)
            {
                return -1;
            }
            catch (InvalidCastException)
            {
                return -1;
            }
            catch (OverflowException)
            {
                return -1;
            }
        }

        private static ulong GetUInt64(ManagementBaseObject obj, string name)
        {
            object? value = GetProperty(obj, name);
            if (value == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToUInt64(value, CultureInfoInvariant);
            }
            catch (FormatException)
            {
                return 0;
            }
            catch (InvalidCastException)
            {
                return 0;
            }
            catch (OverflowException)
            {
                return 0;
            }
        }

        private static readonly IFormatProvider CultureInfoInvariant = CultureInfo.InvariantCulture;

        private sealed class MsftPhysicalDiskSnapshot
        {
            public string SerialNumber { get; init; } = string.Empty;
            public string Model { get; init; } = string.Empty;
            public string Manufacturer { get; init; } = string.Empty;
            public int MediaType { get; init; }
            public string BusTypeName { get; init; } = string.Empty;
            public ulong Size { get; init; }
            public string FirmwareVersion { get; init; } = string.Empty;
        }
    }
}
