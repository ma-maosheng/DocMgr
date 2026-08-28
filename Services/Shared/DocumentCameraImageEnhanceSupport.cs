using OpenCvSharp;
using DocMgr.Models.Shared;

namespace DocMgr.Services.Shared
{
    /// <summary>
    /// 高影仪静帧影像处理：切边、透视、去黑边、白底、扶正等。失败时返回原 JPEG。
    /// </summary>
    public static class DocumentCameraImageEnhanceSupport
    {
        /// <summary>
        /// 按偏好处理 JPEG。处理失败或结果无效时返回原始字节。
        /// </summary>
        public static byte[] EnhanceJpeg(byte[] jpegBytes, DocumentCameraCaptureSettings settings)
        {
            ArgumentNullException.ThrowIfNull(jpegBytes);
            ArgumentNullException.ThrowIfNull(settings);
            if (jpegBytes.Length < 16)
            {
                return jpegBytes;
            }

            DocumentCameraCaptureSettings snapshot = settings.Clone();
            snapshot.Normalize();
            try
            {
                using Mat source = Cv2.ImDecode(jpegBytes, ImreadModes.Color);
                if (source.Empty())
                {
                    return jpegBytes;
                }

                using Mat enhanced = Enhance(source, snapshot);
                if (enhanced.Empty() || enhanced.Width < 32 || enhanced.Height < 32)
                {
                    return jpegBytes;
                }

                int[] encodeParams =
                [
                    (int)ImwriteFlags.JpegQuality,
                    snapshot.JpegQuality
                ];
                if (!Cv2.ImEncode(".jpg", enhanced, out byte[] buffer, encodeParams)
                    || buffer == null
                    || buffer.Length == 0)
                {
                    return jpegBytes;
                }

                return buffer;
            }
            catch (Exception)
            {
                return jpegBytes;
            }
        }

        private static Mat Enhance(Mat source, DocumentCameraCaptureSettings settings)
        {
            Mat current = source.Clone();
            try
            {
                Replace(ref current, Rotate(current, settings.RotationDegrees));
                if (settings.Mirror)
                {
                    Replace(ref current, FlipHorizontal(current));
                }

                bool warped = false;
                if (settings.PerspectiveCorrection || settings.AutoCrop)
                {
                    warped = TryCropDocument(ref current, settings.PerspectiveCorrection, settings.AutoCrop);
                }

                if (settings.AutoDeskew && !warped)
                {
                    Replace(ref current, Deskew(current));
                }

                if (settings.RemoveBlackBorder)
                {
                    Replace(ref current, TrimBlackBorder(current));
                }

                if (settings.RemoveShadow || settings.WhiteDocument)
                {
                    Replace(ref current, CorrectIllumination(current, settings.RemoveShadow, settings.WhiteDocument));
                }

                if (settings.Sharpen)
                {
                    Replace(ref current, Sharpen(current));
                }

                Replace(ref current, ApplyColorMode(current, settings.ColorMode));
                Replace(ref current, AdjustBrightnessContrast(current, settings.Brightness, settings.Contrast));
                return current;
            }
            catch
            {
                current.Dispose();
                throw;
            }
        }

        private static void Replace(ref Mat current, Mat next)
        {
            if (ReferenceEquals(current, next))
            {
                return;
            }

            current.Dispose();
            current = next;
        }

        private static Mat Rotate(Mat source, int degrees)
        {
            if (degrees == 0)
            {
                return source.Clone();
            }

            RotateFlags? flag = degrees switch
            {
                90 => RotateFlags.Rotate90Clockwise,
                180 => RotateFlags.Rotate180,
                270 => RotateFlags.Rotate90Counterclockwise,
                _ => null
            };
            if (flag == null)
            {
                return source.Clone();
            }

            Mat dest = new();
            Cv2.Rotate(source, dest, flag.Value);
            return dest;
        }

        private static Mat FlipHorizontal(Mat source)
        {
            Mat dest = new();
            Cv2.Flip(source, dest, FlipMode.Y);
            return dest;
        }

        private static bool TryCropDocument(ref Mat current, bool perspective, bool autoCrop)
        {
            Point2f[]? quad = TryFindDocumentQuad(current);
            if (quad == null)
            {
                if (!autoCrop)
                {
                    return false;
                }

                Rect? bounds = TryFindContentBounds(current);
                if (bounds == null)
                {
                    return false;
                }

                Replace(ref current, CropWithPadding(current, bounds.Value, 8));
                return false;
            }

            Point2f[] ordered = OrderQuad(quad);
            if (ordered.Distinct().Count() < 4)
            {
                return false;
            }

            if (perspective)
            {
                Mat? warped = TryWarpPerspective(current, ordered);
                if (warped != null)
                {
                    Replace(ref current, warped);
                    return true;
                }
            }

            if (autoCrop)
            {
                var points = ordered.Select(item => (Point)item).ToArray();
                Rect bounds = Cv2.BoundingRect(points);
                Replace(ref current, CropWithPadding(current, bounds, 6));
            }

            return false;
        }

        private static Point2f[]? TryFindDocumentQuad(Mat source)
        {
            using Mat gray = ToGray(source);
            int maxSide = Math.Max(gray.Width, gray.Height);
            double scale = maxSide > 960 ? 960.0 / maxSide : 1.0;
            using Mat small = new();
            if (scale < 1)
            {
                Cv2.Resize(gray, small, new Size((int)(gray.Width * scale), (int)(gray.Height * scale)));
            }
            else
            {
                gray.CopyTo(small);
            }

            using Mat blurred = new();
            Cv2.GaussianBlur(small, blurred, new Size(5, 5), 0);

            Point2f[]? fromEdges = FindLargestQuad(blurred, scale, useCanny: true);
            if (fromEdges != null)
            {
                return fromEdges;
            }

            return FindLargestQuad(blurred, scale, useCanny: false);
        }

        private static Point2f[]? FindLargestQuad(Mat graySmall, double scale, bool useCanny)
        {
            using Mat binary = new();
            if (useCanny)
            {
                Cv2.Canny(graySmall, binary, 40, 120);
                using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
                Cv2.Dilate(binary, binary, kernel);
            }
            else
            {
                Cv2.AdaptiveThreshold(
                    graySmall,
                    binary,
                    255,
                    AdaptiveThresholdTypes.GaussianC,
                    ThresholdTypes.BinaryInv,
                    21,
                    8);
                using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
                Cv2.MorphologyEx(binary, binary, MorphTypes.Close, kernel);
            }

            Cv2.FindContours(binary, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            double imageArea = graySmall.Width * (double)graySmall.Height;
            Point[]? best = null;
            double bestArea = 0;
            foreach (Point[] contour in contours)
            {
                double peri = Cv2.ArcLength(contour, true);
                Point[] approx = Cv2.ApproxPolyDP(contour, 0.02 * peri, true);
                if (approx.Length != 4 || !Cv2.IsContourConvex(approx))
                {
                    continue;
                }

                double area = Math.Abs(Cv2.ContourArea(approx));
                if (area < imageArea * 0.18 || area <= bestArea)
                {
                    continue;
                }

                bestArea = area;
                best = approx;
            }

            if (best == null)
            {
                return null;
            }

            return best
                .Select(point => new Point2f((float)(point.X / scale), (float)(point.Y / scale)))
                .ToArray();
        }

        private static Mat? TryWarpPerspective(Mat source, Point2f[] ordered)
        {
            float widthTop = Distance(ordered[0], ordered[1]);
            float widthBottom = Distance(ordered[3], ordered[2]);
            float heightLeft = Distance(ordered[0], ordered[3]);
            float heightRight = Distance(ordered[1], ordered[2]);
            int width = (int)Math.Round(Math.Max(widthTop, widthBottom));
            int height = (int)Math.Round(Math.Max(heightLeft, heightRight));
            if (width < 48 || height < 48)
            {
                return null;
            }

            double aspect = width / (double)height;
            if (aspect is < 0.2 or > 5)
            {
                return null;
            }

            double warpedArea = width * (double)height;
            double sourceArea = source.Width * (double)source.Height;
            if (warpedArea < sourceArea * 0.12)
            {
                return null;
            }

            Point2f[] destination =
            [
                new Point2f(0, 0),
                new Point2f(width - 1, 0),
                new Point2f(width - 1, height - 1),
                new Point2f(0, height - 1)
            ];
            using Mat matrix = Cv2.GetPerspectiveTransform(ordered, destination);
            Mat dest = new();
            Cv2.WarpPerspective(source, dest, matrix, new Size(width, height));
            return dest;
        }

        private static Point2f[] OrderQuad(Point2f[] points)
        {
            Point2f[] ordered = new Point2f[4];
            float[] sums = points.Select(item => item.X + item.Y).ToArray();
            float[] diffs = points.Select(item => item.Y - item.X).ToArray();
            int topLeft = IndexOfMin(sums);
            int bottomRight = IndexOfMax(sums);
            int topRight = IndexOfMin(diffs);
            int bottomLeft = IndexOfMax(diffs);
            ordered[0] = points[topLeft];
            ordered[1] = points[topRight];
            ordered[2] = points[bottomRight];
            ordered[3] = points[bottomLeft];
            return ordered;
        }

        private static Rect? TryFindContentBounds(Mat source)
        {
            using Mat gray = ToGray(source);
            using Mat binary = new();
            Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            using Mat nonZero = new();
            Cv2.FindNonZero(binary, nonZero);
            if (nonZero.Empty())
            {
                return null;
            }

            Rect bounds = Cv2.BoundingRect(nonZero);
            if (bounds.Width < source.Width * 0.35 || bounds.Height < source.Height * 0.35)
            {
                return null;
            }

            return bounds;
        }

        private static Mat CropWithPadding(Mat source, Rect bounds, int padding)
        {
            int x = Math.Max(0, bounds.X - padding);
            int y = Math.Max(0, bounds.Y - padding);
            int right = Math.Min(source.Width, bounds.X + bounds.Width + padding);
            int bottom = Math.Min(source.Height, bounds.Y + bounds.Height + padding);
            int width = right - x;
            int height = bottom - y;
            if (width < 32 || height < 32)
            {
                return source.Clone();
            }

            return new Mat(source, new Rect(x, y, width, height)).Clone();
        }

        private static Mat Deskew(Mat source)
        {
            using Mat gray = ToGray(source);
            using Mat binary = new();
            Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
            Cv2.FindContours(binary, out Point[][] contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
            if (contours.Length == 0)
            {
                return source.Clone();
            }

            Point[] largest = contours.OrderByDescending(item => Cv2.ContourArea(item)).First();
            if (Cv2.ContourArea(largest) < source.Width * source.Height * 0.08)
            {
                return source.Clone();
            }

            RotatedRect rect = Cv2.MinAreaRect(largest);
            double angle = rect.Angle;
            if (rect.Size.Width < rect.Size.Height)
            {
                angle += 90;
            }

            if (angle > 45)
            {
                angle -= 90;
            }
            else if (angle < -45)
            {
                angle += 90;
            }

            if (Math.Abs(angle) < 0.4 || Math.Abs(angle) > 15)
            {
                return source.Clone();
            }

            Point2f center = new(source.Width / 2f, source.Height / 2f);
            using Mat matrix = Cv2.GetRotationMatrix2D(center, angle, 1.0);
            Mat dest = new();
            Cv2.WarpAffine(source, dest, matrix, source.Size(), InterpolationFlags.Linear, BorderTypes.Replicate);
            return dest;
        }

        private static Mat TrimBlackBorder(Mat source)
        {
            using Mat gray = ToGray(source);
            using Mat binary = new();
            Cv2.Threshold(gray, binary, 18, 255, ThresholdTypes.Binary);
            using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            Cv2.MorphologyEx(binary, binary, MorphTypes.Open, kernel);
            using Mat nonZero = new();
            Cv2.FindNonZero(binary, nonZero);
            if (nonZero.Empty())
            {
                return source.Clone();
            }

            Rect bounds = Cv2.BoundingRect(nonZero);
            double remainRatio = (bounds.Width * (double)bounds.Height) / (source.Width * (double)source.Height);
            if (remainRatio < 0.75)
            {
                return source.Clone();
            }

            return CropWithPadding(source, bounds, 2);
        }

        private static Mat CorrectIllumination(Mat source, bool removeShadow, bool whiteDocument)
        {
            using Mat lab = new();
            Cv2.CvtColor(source, lab, ColorConversionCodes.BGR2Lab);
            Mat[] channels = Cv2.Split(lab);
            try
            {
                Mat lightness = channels[0];
                int kernelSize = Math.Max(31, (Math.Min(lightness.Width, lightness.Height) / (removeShadow ? 8 : 16)) | 1);
                if (kernelSize % 2 == 0)
                {
                    kernelSize++;
                }

                using Mat background = new();
                if (removeShadow)
                {
                    using Mat morphKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(kernelSize, kernelSize));
                    Cv2.MorphologyEx(lightness, background, MorphTypes.Close, morphKernel);
                }
                else
                {
                    Cv2.GaussianBlur(lightness, background, new Size(kernelSize, kernelSize), 0);
                }

                using Mat lightness32 = new();
                using Mat background32 = new();
                lightness.ConvertTo(lightness32, MatType.CV_32F);
                background.ConvertTo(background32, MatType.CV_32F);
                Cv2.Max(background32, 1f, background32);
                using Mat ratio = new();
                Cv2.Divide(lightness32, background32, ratio);
                float target = whiteDocument ? 230f : 210f;
                using Mat scaled = new();
                Cv2.Multiply(ratio, target, scaled);
                Cv2.Min(scaled, 255, scaled);
                using Mat lightness8 = new();
                scaled.ConvertTo(lightness8, MatType.CV_8U);
                lightness8.CopyTo(channels[0]);
                using Mat merged = new();
                Cv2.Merge(channels, merged);
                Mat dest = new();
                Cv2.CvtColor(merged, dest, ColorConversionCodes.Lab2BGR);
                return dest;
            }
            finally
            {
                foreach (Mat channel in channels)
                {
                    channel.Dispose();
                }
            }
        }

        private static Mat Sharpen(Mat source)
        {
            using Mat blurred = new();
            Cv2.GaussianBlur(source, blurred, new Size(0, 0), 1.2);
            Mat dest = new();
            Cv2.AddWeighted(source, 1.45, blurred, -0.45, 0, dest);
            return dest;
        }

        private static Mat ApplyColorMode(Mat source, string colorMode)
        {
            if (string.Equals(colorMode, DocumentCameraCaptureSettings.ColorModeColor, StringComparison.Ordinal))
            {
                return source.Clone();
            }

            using Mat gray = ToGray(source);
            if (string.Equals(colorMode, DocumentCameraCaptureSettings.ColorModeBinary, StringComparison.Ordinal))
            {
                using Mat binary = new();
                Cv2.AdaptiveThreshold(
                    gray,
                    binary,
                    255,
                    AdaptiveThresholdTypes.GaussianC,
                    ThresholdTypes.Binary,
                    25,
                    10);
                Mat dest = new();
                Cv2.CvtColor(binary, dest, ColorConversionCodes.GRAY2BGR);
                return dest;
            }

            Mat grayBgr = new();
            Cv2.CvtColor(gray, grayBgr, ColorConversionCodes.GRAY2BGR);
            return grayBgr;
        }

        private static Mat AdjustBrightnessContrast(Mat source, int brightness, int contrast)
        {
            if (brightness == 0 && contrast == 0)
            {
                return source.Clone();
            }

            double alpha = 1 + (contrast / 100.0);
            double beta = brightness * 1.8;
            Mat dest = new();
            source.ConvertTo(dest, -1, alpha, beta);
            return dest;
        }

        private static Mat ToGray(Mat source)
        {
            if (source.Channels() == 1)
            {
                return source.Clone();
            }

            Mat gray = new();
            Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
            return gray;
        }

        private static float Distance(Point2f a, Point2f b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return MathF.Sqrt((dx * dx) + (dy * dy));
        }

        private static int IndexOfMin(float[] values)
        {
            int index = 0;
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] < values[index])
                {
                    index = i;
                }
            }

            return index;
        }

        private static int IndexOfMax(float[] values)
        {
            int index = 0;
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > values[index])
                {
                    index = i;
                }
            }

            return index;
        }
    }
}
