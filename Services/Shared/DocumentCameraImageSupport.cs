using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;

namespace DocMgr.Services.Shared
{
    /// <summary>
    /// 高影仪帧数据与 JPEG / 预览位图转换。
    /// FlashCap 对非 JPEG 帧给出的是 DIB（无 BM 文件头），WPF BitmapImage 无法直接解码。
    /// </summary>
    public static class DocumentCameraImageSupport
    {
        /// <summary>实况预览最长边像素；全分辨率解码会卡住 UI（如 4160×3102）。</summary>
        public const int LivePreviewMaxEdge = 1600;

        /// <summary>
        /// 将摄像头原始帧（JPEG / BMP / DIB）转为可冻结的预览位图；无法解码时返回 <see langword="null"/>。
        /// 须在 STA 线程调用。
        /// </summary>
        public static BitmapSource? TryCreateBitmapSource(byte[]? imageBytes)
        {
            if (imageBytes == null || imageBytes.Length < 16)
            {
                return null;
            }

            try
            {
                if (IsJpeg(imageBytes) || IsPng(imageBytes) || IsBmpFile(imageBytes))
                {
                    return LoadBitmapImage(imageBytes);
                }

                if (IsDibHeader(imageBytes))
                {
                    BitmapSource? fromPixels = TryCreateFromDibPixels(imageBytes);
                    if (fromPixels != null)
                    {
                        return fromPixels;
                    }

                    return LoadBitmapImage(WrapDibAsBmpFile(imageBytes));
                }

                return LoadBitmapImage(imageBytes);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 后台线程将原始帧缩成轻量 JPEG，供实况预览。可在 MTA 调用；拍摄仍用全分辨率原帧。
        /// </summary>
        public static byte[]? TryCreateDownscaledJpeg(byte[]? imageBytes, int maxEdge = LivePreviewMaxEdge)
        {
            if (imageBytes == null || imageBytes.Length < 16 || maxEdge < 320)
            {
                return null;
            }

            try
            {
                byte[] payload = IsDibHeader(imageBytes) ? WrapDibAsBmpFile(imageBytes) : imageBytes;
                using Mat source = Cv2.ImDecode(payload, ImreadModes.Color);
                if (source.Empty() || source.Width < 8 || source.Height < 8)
                {
                    return null;
                }

                int maxSide = Math.Max(source.Width, source.Height);
                Mat working = source;
                Mat? resized = null;
                try
                {
                    if (maxSide > maxEdge)
                    {
                        double scale = maxEdge / (double)maxSide;
                        resized = new Mat();
                        Cv2.Resize(
                            source,
                            resized,
                            new Size(
                                Math.Max(1, (int)Math.Round(source.Width * scale)),
                                Math.Max(1, (int)Math.Round(source.Height * scale))),
                            0,
                            0,
                            InterpolationFlags.Area);
                        working = resized;
                    }

                    int[] encodeParams =
                    [
                        (int)ImwriteFlags.JpegQuality,
                        78
                    ];
                    if (!Cv2.ImEncode(".jpg", working, out byte[] buffer, encodeParams)
                        || buffer == null
                        || buffer.Length == 0)
                    {
                        return null;
                    }

                    return buffer;
                }
                finally
                {
                    resized?.Dispose();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 将摄像头原始帧转为 JPEG。已是 JPEG 时原样返回。
        /// 须在 STA 线程调用。
        /// </summary>
        public static byte[] ToJpeg(byte[] imageBytes)
        {
            ArgumentNullException.ThrowIfNull(imageBytes);
            if (IsJpeg(imageBytes))
            {
                return imageBytes;
            }

            BitmapSource bitmap = TryCreateBitmapSource(imageBytes)
                ?? throw new InvalidOperationException("无法将拍摄画面编码为 JPEG。");
            return EncodeJpeg(bitmap);
        }

        /// <summary>
        /// 将位图编码为 JPEG。
        /// </summary>
        public static byte[] EncodeJpeg(BitmapSource bitmap, int quality = 88)
        {
            ArgumentNullException.ThrowIfNull(bitmap);
            int clamped = Math.Clamp(quality, 40, 100);
            var encoder = new JpegBitmapEncoder { QualityLevel = clamped };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }

        /// <summary>
        /// 判断内容是否为 JPEG。
        /// </summary>
        public static bool IsJpeg(byte[]? content) =>
            content != null
            && content.Length >= 3
            && content[0] == 0xFF
            && content[1] == 0xD8
            && content[2] == 0xFF;

        private static bool IsPng(byte[] content) =>
            content.Length >= 8
            && content[0] == 0x89
            && content[1] == 0x50
            && content[2] == 0x4E
            && content[3] == 0x47;

        private static bool IsBmpFile(byte[] content) =>
            content.Length >= 2
            && content[0] == 0x42
            && content[1] == 0x4D;

        private static bool IsDibHeader(byte[] content)
        {
            if (content.Length < 40)
            {
                return false;
            }

            int headerSize = BitConverter.ToInt32(content, 0);
            return headerSize is 40 or 108 or 124;
        }

        private static BitmapSource LoadBitmapImage(byte[] imageBytes)
        {
            using var stream = new MemoryStream(imageBytes, writable: false);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private static BitmapSource? TryCreateFromDibPixels(byte[] dib)
        {
            int headerSize = BitConverter.ToInt32(dib, 0);
            int width = BitConverter.ToInt32(dib, 4);
            int heightRaw = BitConverter.ToInt32(dib, 8);
            ushort bitCount = BitConverter.ToUInt16(dib, 14);
            int compression = BitConverter.ToInt32(dib, 16);
            if (width <= 0 || heightRaw == 0 || compression != 0)
            {
                return null;
            }

            PixelFormat? format = bitCount switch
            {
                24 => PixelFormats.Bgr24,
                32 => PixelFormats.Bgr32,
                _ => null
            };
            if (format == null)
            {
                return null;
            }

            int height = Math.Abs(heightRaw);
            bool topDown = heightRaw < 0;
            int stride = ((width * bitCount + 31) / 32) * 4;
            int pixelCount = stride * height;
            if (headerSize + pixelCount > dib.Length)
            {
                return null;
            }

            byte[] pixels = new byte[pixelCount];
            if (topDown)
            {
                Buffer.BlockCopy(dib, headerSize, pixels, 0, pixelCount);
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    Buffer.BlockCopy(
                        dib,
                        headerSize + ((height - 1 - y) * stride),
                        pixels,
                        y * stride,
                        stride);
                }
            }

            var bitmap = BitmapSource.Create(width, height, 96, 96, format.Value, null, pixels, stride);
            bitmap.Freeze();
            return bitmap;
        }

        private static byte[] WrapDibAsBmpFile(byte[] dib)
        {
            int fileSize = 14 + dib.Length;
            byte[] bmp = new byte[fileSize];
            bmp[0] = 0x42;
            bmp[1] = 0x4D;
            BitConverter.GetBytes(fileSize).CopyTo(bmp, 2);
            int headerSize = BitConverter.ToInt32(dib, 0);
            ushort bitCount = dib.Length >= 16 ? BitConverter.ToUInt16(dib, 14) : (ushort)24;
            int colorsUsed = dib.Length >= 36 ? BitConverter.ToInt32(dib, 32) : 0;
            int paletteBytes = 0;
            if (bitCount <= 8)
            {
                int colorCount = colorsUsed > 0 ? colorsUsed : 1 << bitCount;
                paletteBytes = colorCount * 4;
            }

            int pixelOffset = 14 + headerSize + paletteBytes;
            BitConverter.GetBytes(pixelOffset).CopyTo(bmp, 10);
            Buffer.BlockCopy(dib, 0, bmp, 14, dib.Length);
            return bmp;
        }
    }
}
