using System.Runtime.InteropServices;
using DocMgr.Models.HardDiskMedia;
using Microsoft.Win32.SafeHandles;

namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 通过 ATA SAT / NVMe Identify 读取盘体身份。
    /// 任一步失败均返回 null，不向调用方抛出或携带错误信息。
    /// </summary>
    internal static class LocalPhysicalDiskIdentifyReader
    {
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const uint FileAttributeNormal = 0x00000080;

        private const uint IoctlAtaPassThrough = 0x0004D02C;
        private const uint IoctlScsiPassThrough = 0x0004D004;
        private const uint IoctlStorageQueryProperty = 0x002D1400;

        private const ushort AtaFlagsDrdyRequired = 0x01;
        private const ushort AtaFlagsDataIn = 0x02;
        private const ushort AtaFlagsUseDma = 0x10;
        private const byte AtaCommandIdentifyDevice = 0xEC;
        private const byte ScsiIoctlDataIn = 1;
        private const byte SatAtaPassThrough16 = 0x85;
        private const byte SatAtaPassThrough12 = 0xA1;
        private const int IdentifyLength = 512;
        private const int NvmeIdentifyLength = 4096;
        private const int StorageAdapterProtocolSpecificProperty = 49;
        private const int StorageDeviceProtocolSpecificProperty = 50;
        private const int ProtocolTypeNvme = 3;
        private const int NvmeDataTypeIdentify = 1;
        private const int NvmeCnsController = 1;

        /// <summary>
        /// 读取指定物理盘的 Identify。失败返回 null。
        /// </summary>
        public static LocalPhysicalDiskIdentifySnapshot? TryRead(int diskIndex)
        {
            if (diskIndex < 0)
            {
                return null;
            }

            try
            {
                using SafeFileHandle? handle = TryOpenPhysicalDrive(diskIndex);
                if (handle == null || handle.IsInvalid)
                {
                    return null;
                }

                IntPtr device = handle.DangerousGetHandle();
                return TryAtaPassThrough(device, useDma: false)
                    ?? TryAtaPassThrough(device, useDma: true)
                    ?? TryScsiSatIdentify(device, use16ByteCdb: true)
                    ?? TryScsiSatIdentify(device, use16ByteCdb: false)
                    ?? TryNvmeIdentify(device, StorageDeviceProtocolSpecificProperty)
                    ?? TryNvmeIdentify(device, StorageAdapterProtocolSpecificProperty);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static SafeFileHandle? TryOpenPhysicalDrive(int diskIndex)
        {
            string path = $@"\\.\PHYSICALDRIVE{diskIndex}";
            SafeFileHandle handle = NativeMethods.CreateFile(
                path,
                GenericRead | GenericWrite,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileAttributeNormal,
                IntPtr.Zero);
            if (!handle.IsInvalid)
            {
                return handle;
            }

            handle.Dispose();
            handle = NativeMethods.CreateFile(
                path,
                GenericRead,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileAttributeNormal,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                return null;
            }

            return handle;
        }

        private static LocalPhysicalDiskIdentifySnapshot? TryAtaPassThrough(IntPtr device, bool useDma)
        {
            int headerSize = Marshal.SizeOf<AtaPassThroughEx>();
            int bufferSize = headerSize + IdentifyLength;
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                ZeroMemory(buffer, bufferSize);
                ushort flags = (ushort)(AtaFlagsDataIn | AtaFlagsDrdyRequired);
                if (useDma)
                {
                    flags |= AtaFlagsUseDma;
                }

                var packet = new AtaPassThroughEx
                {
                    Length = (ushort)headerSize,
                    AtaFlags = flags,
                    DataTransferLength = IdentifyLength,
                    TimeOutValue = 8,
                    DataBufferOffset = (ulong)headerSize,
                    PreviousTaskFile = new byte[8],
                    CurrentTaskFile = new byte[8]
                };
                packet.CurrentTaskFile[1] = 1;
                packet.CurrentTaskFile[6] = AtaCommandIdentifyDevice;
                Marshal.StructureToPtr(packet, buffer, false);

                if (!NativeMethods.DeviceIoControl(
                    device,
                    IoctlAtaPassThrough,
                    buffer,
                    (uint)bufferSize,
                    buffer,
                    (uint)bufferSize,
                    out _,
                    IntPtr.Zero))
                {
                    return null;
                }

                var identify = new byte[IdentifyLength];
                Marshal.Copy(buffer + headerSize, identify, 0, IdentifyLength);
                return LocalPhysicalDiskHardwareSupport.TryParseAtaIdentify(identify);
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static LocalPhysicalDiskIdentifySnapshot? TryScsiSatIdentify(IntPtr device, bool use16ByteCdb)
        {
            int headerSize = Marshal.SizeOf<ScsiPassThrough>();
            const int senseLength = 32;
            int dataOffset = headerSize + senseLength;
            int bufferSize = dataOffset + IdentifyLength;
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                ZeroMemory(buffer, bufferSize);
                var packet = new ScsiPassThrough
                {
                    Length = (ushort)headerSize,
                    CdbLength = use16ByteCdb ? (byte)16 : (byte)12,
                    SenseInfoLength = senseLength,
                    DataIn = ScsiIoctlDataIn,
                    DataTransferLength = IdentifyLength,
                    TimeOutValue = 8,
                    DataBufferOffset = (ulong)dataOffset,
                    SenseInfoOffset = (uint)headerSize,
                    Cdb = new byte[16]
                };
                FillSatIdentifyCdb(packet.Cdb, use16ByteCdb);
                Marshal.StructureToPtr(packet, buffer, false);

                if (!NativeMethods.DeviceIoControl(
                    device,
                    IoctlScsiPassThrough,
                    buffer,
                    (uint)bufferSize,
                    buffer,
                    (uint)bufferSize,
                    out _,
                    IntPtr.Zero))
                {
                    return null;
                }

                var header = Marshal.PtrToStructure<ScsiPassThrough>(buffer);
                if (header.ScsiStatus != 0)
                {
                    return null;
                }

                var identify = new byte[IdentifyLength];
                Marshal.Copy(buffer + dataOffset, identify, 0, IdentifyLength);
                return LocalPhysicalDiskHardwareSupport.TryParseAtaIdentify(identify);
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static void FillSatIdentifyCdb(byte[] cdb, bool use16ByteCdb)
        {
            const byte pioDataInProtocol = 4 << 1;
            const byte tDirFromDeviceByteBlockSectorCount = 0x0E;
            if (use16ByteCdb)
            {
                cdb[0] = SatAtaPassThrough16;
                cdb[1] = pioDataInProtocol;
                cdb[2] = tDirFromDeviceByteBlockSectorCount;
                cdb[6] = 1;
                cdb[14] = AtaCommandIdentifyDevice;
                return;
            }

            cdb[0] = SatAtaPassThrough12;
            cdb[1] = pioDataInProtocol;
            cdb[2] = tDirFromDeviceByteBlockSectorCount;
            cdb[4] = 1;
            cdb[9] = AtaCommandIdentifyDevice;
        }

        private static LocalPhysicalDiskIdentifySnapshot? TryNvmeIdentify(IntPtr device, int propertyId)
        {
            int protocolSize = Marshal.SizeOf<StorageProtocolSpecificData>();
            int queryHeaderSize = 8;
            int descriptorHeaderSize = 8;
            int bufferSize = queryHeaderSize + protocolSize + NvmeIdentifyLength;
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                ZeroMemory(buffer, bufferSize);
                Marshal.WriteInt32(buffer, 0, propertyId);
                Marshal.WriteInt32(buffer, 4, 0);

                var protocol = new StorageProtocolSpecificData
                {
                    ProtocolType = ProtocolTypeNvme,
                    DataType = NvmeDataTypeIdentify,
                    ProtocolDataRequestValue = NvmeCnsController,
                    ProtocolDataOffset = (uint)protocolSize,
                    ProtocolDataLength = NvmeIdentifyLength
                };
                IntPtr protocolPtr = buffer + queryHeaderSize;
                Marshal.StructureToPtr(protocol, protocolPtr, false);

                if (!NativeMethods.DeviceIoControl(
                    device,
                    IoctlStorageQueryProperty,
                    buffer,
                    (uint)bufferSize,
                    buffer,
                    (uint)bufferSize,
                    out uint returned,
                    IntPtr.Zero))
                {
                    return null;
                }

                if (returned < descriptorHeaderSize + protocolSize)
                {
                    return null;
                }

                var outProtocol = Marshal.PtrToStructure<StorageProtocolSpecificData>(buffer + descriptorHeaderSize);
                int dataOffset = descriptorHeaderSize + (int)outProtocol.ProtocolDataOffset;
                if (dataOffset < descriptorHeaderSize || dataOffset + 72 > bufferSize)
                {
                    return null;
                }

                var identify = new byte[NvmeIdentifyLength];
                int copyLength = Math.Min(NvmeIdentifyLength, bufferSize - dataOffset);
                Marshal.Copy(buffer + dataOffset, identify, 0, copyLength);
                return LocalPhysicalDiskHardwareSupport.TryParseNvmeIdentifyController(identify);
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static void ZeroMemory(IntPtr dest, int length)
        {
            if (length <= 0)
            {
                return;
            }

            Marshal.Copy(new byte[length], 0, dest, length);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AtaPassThroughEx
        {
            public ushort Length;
            public ushort AtaFlags;
            public byte PathId;
            public byte TargetId;
            public byte Lun;
            public byte ReservedAsUchar;
            public uint DataTransferLength;
            public uint TimeOutValue;
            public uint ReservedAsUlong;
            public ulong DataBufferOffset;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] PreviousTaskFile;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] CurrentTaskFile;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ScsiPassThrough
        {
            public ushort Length;
            public byte ScsiStatus;
            public byte PathId;
            public byte TargetId;
            public byte Lun;
            public byte CdbLength;
            public byte SenseInfoLength;
            public byte DataIn;
            public uint DataTransferLength;
            public uint TimeOutValue;
            public ulong DataBufferOffset;
            public uint SenseInfoOffset;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] Cdb;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StorageProtocolSpecificData
        {
            public int ProtocolType;
            public uint DataType;
            public uint ProtocolDataRequestValue;
            public uint ProtocolDataRequestSubValue;
            public uint ProtocolDataOffset;
            public uint ProtocolDataLength;
            public uint FixedProtocolReturnData;
            public uint ProtocolDataRequestSubValue2;
            public uint ProtocolDataRequestSubValue3;
            public uint ProtocolDataRequestSubValue4;
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern SafeFileHandle CreateFile(
                string lpFileName,
                uint dwDesiredAccess,
                uint dwShareMode,
                IntPtr lpSecurityAttributes,
                uint dwCreationDisposition,
                uint dwFlagsAndAttributes,
                IntPtr hTemplateFile);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeviceIoControl(
                IntPtr hDevice,
                uint dwIoControlCode,
                IntPtr lpInBuffer,
                uint nInBufferSize,
                IntPtr lpOutBuffer,
                uint nOutBufferSize,
                out uint lpBytesReturned,
                IntPtr lpOverlapped);
        }
    }
}
