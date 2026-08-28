using FlashCap;
using DocMgr.Models.Shared;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.Shared
{
    /// <summary>
    /// 通过 DirectShow / Media Foundation 枚举并打开本机高影仪或 USB 摄像头。
    /// </summary>
    public sealed class DocumentCameraCaptureService : IDocumentCameraCaptureService
    {
        private static readonly string[] PreferredNameTokens =
        [
            "方正",
            "Founder",
            "Q1300",
            "高拍",
            "高影",
            "GaoPai",
            "Document Camera"
        ];

        private readonly object _cacheLock = new();
        private IReadOnlyList<CachedDevice> _cachedDevices = Array.Empty<CachedDevice>();

        /// <inheritdoc />
        public Task<IReadOnlyList<DocumentCameraDeviceInfo>> EnumerateDevicesAsync(
            CancellationToken cancellationToken = default)
        {
            // DirectShow 设备描述符须在 STA 线程枚举，供后续在同一线程打开。
            return Task.FromResult(EnumerateDevicesCore(cancellationToken));
        }

        /// <inheritdoc />
        public async Task<IDocumentCameraPreviewSession> OpenPreviewAsync(
            string deviceIdentity,
            int width = 0,
            int height = 0,
            CancellationToken cancellationToken = default)
        {
            string identity = deviceIdentity?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(identity))
            {
                throw new InvalidOperationException("未指定拍摄设备。");
            }

            CachedDevice cached = ResolveCachedDevice(identity);
            VideoCharacteristics? characteristics = SelectCharacteristics(cached.Characteristics, width, height);
            if (characteristics == null)
            {
                throw new InvalidOperationException("所选设备没有可用的采集分辨率。");
            }

            var session = new PreviewSession();
            CaptureDevice device = await cached.Descriptor.OpenAsync(
                characteristics,
                TranscodeFormats.Auto,
                session.OnPixelBufferArrived,
                cancellationToken);
            session.Attach(device);
            try
            {
                await session.StartAsync(cancellationToken);
                return session;
            }
            catch
            {
                await session.DisposeAsync();
                throw;
            }
        }

        private IReadOnlyList<DocumentCameraDeviceInfo> EnumerateDevicesCore(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var devices = new CaptureDevices();
            List<CaptureDeviceDescriptor> descriptors = devices.EnumerateDescriptors()
                .Where(item => item.Characteristics != null && item.Characteristics.Length > 0)
                .ToList();

            if (descriptors.Exists(item => item.DeviceType == DeviceTypes.DirectShow))
            {
                descriptors = descriptors
                    .Where(item => item.DeviceType == DeviceTypes.DirectShow)
                    .ToList();
            }
            else if (descriptors.Exists(item => item.DeviceType == DeviceTypes.MediaFoundation))
            {
                descriptors = descriptors
                    .Where(item => item.DeviceType == DeviceTypes.MediaFoundation)
                    .ToList();
            }

            var cached = new List<CachedDevice>();
            var infos = new List<DocumentCameraDeviceInfo>();
            for (int index = 0; index < descriptors.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CaptureDeviceDescriptor descriptor = descriptors[index];
                IReadOnlyList<DocumentCameraResolutionOption> resolutions = BuildResolutions(descriptor.Characteristics);
                if (resolutions.Count == 0)
                {
                    continue;
                }

                string identity = $"{descriptor.DeviceType}|{descriptor.Name}|{index}";
                bool isPreferred = IsPreferredDeviceName(descriptor.Name);
                DocumentCameraResolutionOption max = resolutions[0];
                var info = new DocumentCameraDeviceInfo
                {
                    Identity = identity,
                    Name = descriptor.Name ?? string.Empty,
                    DeviceType = descriptor.DeviceType.ToString(),
                    Resolutions = resolutions,
                    Width = max.Width,
                    Height = max.Height,
                    IsPreferred = isPreferred,
                    DisplayName = descriptor.Name ?? string.Empty
                };

                cached.Add(new CachedDevice(identity, descriptor, descriptor.Characteristics));
                infos.Add(info);
            }

            lock (_cacheLock)
            {
                _cachedDevices = cached;
            }

            return infos
                .OrderByDescending(item => item.IsPreferred)
                .ThenByDescending(item => item.PixelCount)
                .ThenBy(item => item.Name, StringComparer.Ordinal)
                .ToList();
        }

        private CachedDevice ResolveCachedDevice(string identity)
        {
            lock (_cacheLock)
            {
                CachedDevice? cached = _cachedDevices.FirstOrDefault(item =>
                    string.Equals(item.Identity, identity, StringComparison.Ordinal));
                if (cached != null)
                {
                    return cached;
                }
            }

            EnumerateDevicesCore(CancellationToken.None);
            lock (_cacheLock)
            {
                return _cachedDevices.FirstOrDefault(item =>
                    string.Equals(item.Identity, identity, StringComparison.Ordinal))
                    ?? throw new InvalidOperationException("未找到所选拍摄设备，请刷新后重试。");
            }
        }

        private static IReadOnlyList<DocumentCameraResolutionOption> BuildResolutions(
            IReadOnlyList<VideoCharacteristics> characteristics)
        {
            return characteristics
                .Where(item => item.PixelFormat != PixelFormats.Unknown && item.Width > 0 && item.Height > 0)
                .Select(item => new DocumentCameraResolutionOption(item.Width, item.Height))
                .Distinct()
                .OrderByDescending(item => item.PixelCount)
                .ThenByDescending(item => item.Width)
                .ToList();
        }

        private static VideoCharacteristics? SelectCharacteristics(
            IReadOnlyList<VideoCharacteristics> characteristics,
            int width,
            int height)
        {
            List<VideoCharacteristics> known = characteristics
                .Where(item => item.PixelFormat != PixelFormats.Unknown)
                .ToList();
            if (known.Count == 0)
            {
                return null;
            }

            List<VideoCharacteristics> sized = width > 0 && height > 0
                ? known.Where(item => item.Width == width && item.Height == height).ToList()
                : known;
            if (sized.Count == 0)
            {
                DocumentCameraResolutionOption? fallback = DocumentCameraResolutionOption.ResolvePreferred(
                    BuildResolutions(known),
                    width,
                    height);
                if (fallback != null)
                {
                    sized = known
                        .Where(item => item.Width == fallback.Width && item.Height == fallback.Height)
                        .ToList();
                }
            }

            if (sized.Count == 0)
            {
                sized = known;
            }

            List<VideoCharacteristics> fpsOk = sized
                .Where(item => (double)item.FramesPerSecond >= 5)
                .ToList();
            IEnumerable<VideoCharacteristics> pool = fpsOk.Count > 0 ? fpsOk : sized;
            return pool
                .OrderByDescending(item => item.PixelFormat == PixelFormats.JPEG ? 1 : 0)
                .ThenByDescending(item => (double)item.FramesPerSecond)
                .FirstOrDefault();
        }

        private static bool IsPreferredDeviceName(string? name)
        {
            string text = name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return PreferredNameTokens.Any(token =>
                text.Contains(token, StringComparison.OrdinalIgnoreCase));
        }

        private sealed record CachedDevice(
            string Identity,
            CaptureDeviceDescriptor Descriptor,
            IReadOnlyList<VideoCharacteristics> Characteristics);

        private sealed class PreviewSession : IDocumentCameraPreviewSession
        {
            private readonly object _frameLock = new();
            private CaptureDevice? _device;
            private byte[]? _latestRaw;
            private int _previewPosted;
            private int _disposed;

            public event EventHandler<byte[]>? PreviewFrameArrived;

            public bool HasFrame
            {
                get
                {
                    lock (_frameLock)
                    {
                        return _latestRaw is { Length: > 0 };
                    }
                }
            }

            public void Attach(CaptureDevice device)
            {
                _device = device;
            }

            public async Task StartAsync(CancellationToken cancellationToken)
            {
                CaptureDevice device = _device
                    ?? throw new InvalidOperationException("拍摄设备尚未打开。");
                await device.StartAsync(cancellationToken).ConfigureAwait(false);
            }

            public byte[]? CaptureJpeg()
            {
                byte[]? raw;
                lock (_frameLock)
                {
                    raw = _latestRaw;
                }

                if (raw == null || raw.Length == 0)
                {
                    return null;
                }

                return DocumentCameraImageSupport.ToJpeg(raw);
            }

            public void OnPixelBufferArrived(PixelBufferScope bufferScope)
            {
                try
                {
                    byte[] image = bufferScope.Buffer.CopyImage();
                    bufferScope.ReleaseNow();
                    if (image.Length == 0)
                    {
                        return;
                    }

                    lock (_frameLock)
                    {
                        _latestRaw = image;
                    }

                    if (Interlocked.CompareExchange(ref _previewPosted, 1, 0) != 0)
                    {
                        return;
                    }

                    EventHandler<byte[]>? handler = PreviewFrameArrived;
                    if (handler == null)
                    {
                        Interlocked.Exchange(ref _previewPosted, 0);
                        return;
                    }

                    try
                    {
                        handler(this, image);
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _previewPosted, 0);
                    }
                }
                catch (Exception)
                {
                    try
                    {
                        bufferScope.ReleaseNow();
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            public async ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
                {
                    return;
                }

                CaptureDevice? device = _device;
                _device = null;
                if (device == null)
                {
                    return;
                }

                try
                {
                    await device.StopAsync();
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }

                await device.DisposeAsync();
            }
        }
    }
}
