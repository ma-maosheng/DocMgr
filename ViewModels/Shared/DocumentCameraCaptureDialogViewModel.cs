using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DocMgr.Models.Shared;
using DocMgr.Services.Interfaces;
using DocMgr.Services.Shared;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Shared
{
    /// <summary>
    /// 高影仪直拍预览、影像处理与确认弹窗。
    /// </summary>
    public sealed class DocumentCameraCaptureDialogViewModel : ViewModelBase
    {
        private readonly IDocumentCameraCaptureService _captureService;
        private readonly IDocumentCameraCaptureSettingsStore _settingsStore;
        private readonly IUserContextService _userContextService;
        private readonly IDialogService _dialogService;
        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _persistTimer;
        private IDocumentCameraPreviewSession? _session;
        private DocumentCameraDeviceInfo? _selectedDevice;
        private BitmapSource? _previewImage;
        private string _statusText = "正在检测本机高影仪…";
        private byte[]? _originalJpeg;
        private byte[]? _capturedJpeg;
        private bool _isBusy;
        private bool _isFrozen;
        private bool _hasFrame;
        private bool _decodeFailed;
        private bool _suppressDeviceChange;
        private bool _suppressResolutionChange;
        private bool _suppressSettingPersist;
        private int _shutdown;
        private int _previewQueued;
        private int _enhanceVersion;
        private DocumentCameraCaptureSettings _settings = DocumentCameraCaptureSettings.CreateDefault();
        private DocumentCameraResolutionOption? _selectedResolution;

        public DocumentCameraCaptureDialogViewModel(
            IDocumentCameraCaptureService captureService,
            IDocumentCameraCaptureSettingsStore settingsStore,
            IUserContextService userContextService,
            IDialogService dialogService)
        {
            _captureService = captureService;
            _settingsStore = settingsStore;
            _userContextService = userContextService;
            _dialogService = dialogService;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _persistTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _persistTimer.Tick += (_, _) =>
            {
                _persistTimer.Stop();
                PersistSettings();
            };

            Devices = new ObservableCollection<DocumentCameraDeviceInfo>();
            Resolutions = new ObservableCollection<DocumentCameraResolutionOption>();
            RefreshCommand = new RelayCommand(async _ => await LoadDevicesAsync(), _ => !IsBusy);
            CaptureCommand = new RelayCommand(async _ => await CaptureStillAsync(), _ => CanCapture);
            RetakeCommand = new RelayCommand(_ => Retake(), _ => CanRetake);
            ConfirmCommand = new RelayCommand(async _ => await ConfirmAsync(), _ => CanConfirm);
            RotateLeftCommand = new RelayCommand(_ => RotationDegrees = (RotationDegrees + 270) % 360);
            RotateRightCommand = new RelayCommand(_ => RotationDegrees = (RotationDegrees + 90) % 360);
            RestoreDefaultsCommand = new RelayCommand(_ => RestoreDefaults());
            CloseCommand = new RelayCommand(async _ =>
            {
                PersistSettings();
                await ShutdownAsync();
                RequestClose?.Invoke(false);
            });
        }

        public ObservableCollection<DocumentCameraDeviceInfo> Devices { get; }

        public ObservableCollection<DocumentCameraResolutionOption> Resolutions { get; }

        public IReadOnlyList<DocumentCameraColorModeOption> ColorModeOptions =>
            DocumentCameraCaptureSettings.ColorModeOptions;

        public DocumentCameraDeviceInfo? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (!SetProperty(ref _selectedDevice, value))
                {
                    return;
                }

                CommandManager.InvalidateRequerySuggested();
                if (!_suppressDeviceChange && value != null)
                {
                    _settings.LastDeviceIdentity = value.Identity;
                    QueuePersist();
                }

                BindResolutions(value);

                if (_suppressDeviceChange || _shutdown == 1)
                {
                    return;
                }

                _ = RestartPreviewAsync();
            }
        }

        public DocumentCameraResolutionOption? SelectedResolution
        {
            get => _selectedResolution;
            set
            {
                if (!SetProperty(ref _selectedResolution, value))
                {
                    return;
                }

                if (value != null)
                {
                    _settings.CaptureWidth = value.Width;
                    _settings.CaptureHeight = value.Height;
                    if (!_suppressResolutionChange)
                    {
                        QueuePersist();
                    }
                }

                if (_suppressResolutionChange || _suppressDeviceChange || _shutdown == 1)
                {
                    return;
                }

                _ = RestartPreviewAsync();
            }
        }

        public BitmapSource? PreviewImage
        {
            get => _previewImage;
            private set
            {
                if (SetProperty(ref _previewImage, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                    OnPropertyChanged(nameof(CanCapture));
                    OnPropertyChanged(nameof(CanConfirm));
                    OnPropertyChanged(nameof(ShowPreviewPlaceholder));
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public bool AutoCrop
        {
            get => _settings.AutoCrop;
            set => SetSetting(nameof(AutoCrop), () => _settings.AutoCrop = value);
        }

        public bool PerspectiveCorrection
        {
            get => _settings.PerspectiveCorrection;
            set => SetSetting(nameof(PerspectiveCorrection), () => _settings.PerspectiveCorrection = value);
        }

        public bool RemoveBlackBorder
        {
            get => _settings.RemoveBlackBorder;
            set => SetSetting(nameof(RemoveBlackBorder), () => _settings.RemoveBlackBorder = value);
        }

        public bool WhiteDocument
        {
            get => _settings.WhiteDocument;
            set => SetSetting(nameof(WhiteDocument), () => _settings.WhiteDocument = value);
        }

        public bool RemoveShadow
        {
            get => _settings.RemoveShadow;
            set => SetSetting(nameof(RemoveShadow), () => _settings.RemoveShadow = value);
        }

        public bool AutoDeskew
        {
            get => _settings.AutoDeskew;
            set => SetSetting(nameof(AutoDeskew), () => _settings.AutoDeskew = value);
        }

        public bool Mirror
        {
            get => _settings.Mirror;
            set => SetSetting(nameof(Mirror), () => _settings.Mirror = value, affectsLivePreview: true);
        }

        public bool Sharpen
        {
            get => _settings.Sharpen;
            set => SetSetting(nameof(Sharpen), () => _settings.Sharpen = value);
        }

        public int RotationDegrees
        {
            get => _settings.RotationDegrees;
            set
            {
                int normalized = DocumentCameraCaptureSettings.NormalizeRotation(value);
                if (_settings.RotationDegrees == normalized)
                {
                    return;
                }

                _settings.RotationDegrees = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RotationDisplay));
                OnPropertyChanged(nameof(PreviewRotationAngle));
                QueuePersist();
                _ = RefreshProcessedPreviewAsync();
            }
        }

        public string RotationDisplay => $"{RotationDegrees}°";

        public double PreviewRotationAngle => IsFrozen ? 0 : RotationDegrees;

        public double PreviewMirrorScaleX => IsFrozen || !Mirror ? 1 : -1;

        public int Brightness
        {
            get => _settings.Brightness;
            set
            {
                int clamped = Math.Clamp(value, -50, 50);
                if (_settings.Brightness == clamped)
                {
                    return;
                }

                _settings.Brightness = clamped;
                OnPropertyChanged();
                QueuePersist();
                _ = RefreshProcessedPreviewAsync();
            }
        }

        public int Contrast
        {
            get => _settings.Contrast;
            set
            {
                int clamped = Math.Clamp(value, -50, 50);
                if (_settings.Contrast == clamped)
                {
                    return;
                }

                _settings.Contrast = clamped;
                OnPropertyChanged();
                QueuePersist();
                _ = RefreshProcessedPreviewAsync();
            }
        }

        public string ColorMode
        {
            get => _settings.ColorMode;
            set
            {
                string mode = string.IsNullOrWhiteSpace(value)
                    ? DocumentCameraCaptureSettings.ColorModeColor
                    : value.Trim();
                if (string.Equals(_settings.ColorMode, mode, StringComparison.Ordinal))
                {
                    return;
                }

                _settings.ColorMode = mode;
                OnPropertyChanged();
                QueuePersist();
                _ = RefreshProcessedPreviewAsync();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                    OnPropertyChanged(nameof(CanCapture));
                    OnPropertyChanged(nameof(CanRetake));
                    OnPropertyChanged(nameof(CanConfirm));
                    OnPropertyChanged(nameof(CanChangeDevice));
                }
            }
        }

        public bool IsFrozen
        {
            get => _isFrozen;
            private set
            {
                if (SetProperty(ref _isFrozen, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                    OnPropertyChanged(nameof(CanCapture));
                    OnPropertyChanged(nameof(CanRetake));
                    OnPropertyChanged(nameof(CanConfirm));
                    OnPropertyChanged(nameof(CanChangeDevice));
                    OnPropertyChanged(nameof(PreviewRotationAngle));
                    OnPropertyChanged(nameof(PreviewMirrorScaleX));
                }
            }
        }

        public bool CanCapture => !IsBusy && !IsFrozen && _session != null && HasFrame;
        public bool CanRetake => !IsBusy && IsFrozen;
        public bool CanConfirm => !IsBusy && (_capturedJpeg != null || HasFrame);
        public bool CanChangeDevice => !IsBusy && !IsFrozen;
        public bool ShowPreviewPlaceholder => PreviewImage == null;

        private bool HasFrame => _hasFrame || _session?.HasFrame == true;

        public DocumentCameraCaptureResult? CaptureResult { get; private set; }

        public ICommand RefreshCommand { get; }
        public ICommand CaptureCommand { get; }
        public ICommand RetakeCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand RotateLeftCommand { get; }
        public ICommand RotateRightCommand { get; }
        public ICommand RestoreDefaultsCommand { get; }
        public ICommand CloseCommand { get; }

        public event Action<bool?>? RequestClose;

        public Task InitializeAsync()
        {
            LoadPersistedSettings();
            return LoadDevicesAsync();
        }

        public async Task ShutdownAsync()
        {
            if (Interlocked.Exchange(ref _shutdown, 1) == 1)
            {
                return;
            }

            _persistTimer.Stop();
            PersistSettings();
            await StopSessionAsync();
        }

        private void LoadPersistedSettings()
        {
            _suppressSettingPersist = true;
            try
            {
                _settings = _settingsStore.Load(CurrentUserId);
                _settings.Normalize();
                NotifySettingsChanged();
            }
            finally
            {
                _suppressSettingPersist = false;
            }
        }

        private async Task LoadDevicesAsync()
        {
            IsBusy = true;
            StatusText = "正在检测本机高影仪…";
            _originalJpeg = null;
            _capturedJpeg = null;
            IsFrozen = false;
            _hasFrame = false;
            _decodeFailed = false;
            PreviewImage = null;
            NotifyCaptureState();

            try
            {
                await StopSessionAsync();
                IReadOnlyList<DocumentCameraDeviceInfo> devices = await _captureService.EnumerateDevicesAsync();
                _suppressDeviceChange = true;
                try
                {
                    Devices.Clear();
                    foreach (DocumentCameraDeviceInfo device in devices)
                    {
                        Devices.Add(device);
                    }

                    SelectedDevice = ResolvePreferredDevice();
                }
                finally
                {
                    _suppressDeviceChange = false;
                }

                if (SelectedDevice == null)
                {
                    StatusText = "未检测到高影仪或摄像头。请确认方正 Q1300 已安装驱动并已连接，并关闭厂商预览软件后点击「刷新」。";
                    return;
                }

                await StartPreviewAsync();
            }
            catch (InvalidOperationException ex)
            {
                StatusText = ex.Message;
                _dialogService.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                StatusText = "检测拍摄设备失败。";
                _dialogService.ShowError($"检测拍摄设备失败：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private DocumentCameraDeviceInfo? ResolvePreferredDevice()
        {
            string? lastIdentity = _settings.LastDeviceIdentity;
            if (!string.IsNullOrWhiteSpace(lastIdentity))
            {
                DocumentCameraDeviceInfo? remembered = Devices.FirstOrDefault(item =>
                    string.Equals(item.Identity, lastIdentity, StringComparison.Ordinal));
                if (remembered != null)
                {
                    return remembered;
                }
            }

            return Devices.FirstOrDefault(item => item.IsPreferred) ?? Devices.FirstOrDefault();
        }

        private void BindResolutions(DocumentCameraDeviceInfo? device)
        {
            _suppressResolutionChange = true;
            try
            {
                Resolutions.Clear();
                if (device == null)
                {
                    SelectedResolution = null;
                    return;
                }

                foreach (DocumentCameraResolutionOption option in device.Resolutions)
                {
                    Resolutions.Add(option);
                }

                DocumentCameraResolutionOption? preferred = DocumentCameraResolutionOption.ResolvePreferred(
                    Resolutions,
                    _settings.CaptureWidth,
                    _settings.CaptureHeight);
                SelectedResolution = preferred;
                if (preferred != null)
                {
                    _settings.CaptureWidth = preferred.Width;
                    _settings.CaptureHeight = preferred.Height;
                }
            }
            finally
            {
                _suppressResolutionChange = false;
            }
        }

        private async Task RestartPreviewAsync()
        {
            if (_shutdown == 1)
            {
                return;
            }

            IsBusy = true;
            _originalJpeg = null;
            _capturedJpeg = null;
            IsFrozen = false;
            _hasFrame = false;
            _decodeFailed = false;
            PreviewImage = null;
            NotifyCaptureState();
            try
            {
                await StartPreviewAsync();
            }
            catch (Exception ex)
            {
                StatusText = "无法打开所选设备。";
                _dialogService.ShowError($"无法打开拍摄设备：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task StartPreviewAsync()
        {
            await StopSessionAsync();
            if (SelectedDevice == null)
            {
                StatusText = "请选择拍摄设备。";
                return;
            }

            StatusText = SelectedResolution == null
                ? $"正在打开「{SelectedDevice.Name}」…"
                : $"正在打开「{SelectedDevice.Name}」{SelectedResolution.DisplayName}…";
            IDocumentCameraPreviewSession session = await _captureService.OpenPreviewAsync(
                SelectedDevice.Identity,
                SelectedResolution?.Width ?? 0,
                SelectedResolution?.Height ?? 0);
            session.PreviewFrameArrived += OnPreviewFrameArrived;
            _session = session;
            StatusText = "请将资料放正后点击「拍摄」。处理开关已记住，可按本机习惯调整。";
            NotifyCaptureState();
            _ = WaitForFirstFrameAsync();
        }

        private async Task StopSessionAsync()
        {
            IDocumentCameraPreviewSession? session = Interlocked.Exchange(ref _session, null);
            if (session == null)
            {
                return;
            }

            session.PreviewFrameArrived -= OnPreviewFrameArrived;
            await session.DisposeAsync();
        }

        private async Task WaitForFirstFrameAsync()
        {
            await Task.Delay(2500);
            if (_shutdown == 1 || _isFrozen || PreviewImage != null || _hasFrame)
            {
                return;
            }

            StatusText = "设备已打开，但尚未收到画面。请关闭方正自带预览软件后点「刷新」；也可改选较低像素后再试。";
        }

        private void OnPreviewFrameArrived(object? sender, byte[] image)
        {
            if (_isFrozen || _shutdown == 1 || image == null || image.Length == 0)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _previewQueued, 1, 0) != 0)
            {
                return;
            }

            _ = _dispatcher.BeginInvoke(() =>
            {
                try
                {
                    if (_isFrozen || _shutdown == 1)
                    {
                        return;
                    }

                    _hasFrame = true;
                    BitmapSource? bitmap = DocumentCameraImageSupport.TryCreateBitmapSource(image);
                    if (bitmap != null)
                    {
                        PreviewImage = bitmap;
                    }
                    else if (!_decodeFailed)
                    {
                        _decodeFailed = true;
                        StatusText = "已收到拍摄画面，但预览解码失败。请直接点击「拍摄」尝试保存；或切换设备后刷新。";
                    }

                    NotifyCaptureState();
                }
                finally
                {
                    Interlocked.Exchange(ref _previewQueued, 0);
                }
            }, DispatcherPriority.Render);
        }

        private async Task CaptureStillAsync()
        {
            if (_session == null)
            {
                _dialogService.ShowMessage("拍摄设备尚未就绪。");
                return;
            }

            byte[]? jpeg = _session.CaptureJpeg();
            if (jpeg == null || jpeg.Length == 0)
            {
                _dialogService.ShowMessage("尚未获取到预览画面，请稍候再拍。");
                return;
            }

            _originalJpeg = jpeg;
            IsFrozen = true;
            NotifyCaptureState();
            StatusText = "正在按当前处理设置生成预览…";
            await RefreshProcessedPreviewAsync();
            if (_capturedJpeg != null)
            {
                StatusText = "已拍摄。可继续调整处理开关后确认上传，或点击「重拍」。";
            }
        }

        private void Retake()
        {
            Interlocked.Increment(ref _enhanceVersion);
            _originalJpeg = null;
            _capturedJpeg = null;
            IsFrozen = false;
            NotifyCaptureState();
            StatusText = "请将资料放正后点击「拍摄」。";
        }

        private async Task ConfirmAsync()
        {
            if (_originalJpeg == null && _capturedJpeg == null)
            {
                await CaptureStillAsync();
            }
            else if (_capturedJpeg == null && _originalJpeg != null)
            {
                await RefreshProcessedPreviewAsync();
            }

            if (_capturedJpeg == null || _capturedJpeg.Length == 0)
            {
                return;
            }

            PersistSettings();
            CaptureResult = new DocumentCameraCaptureResult { JpegContent = _capturedJpeg };
            await ShutdownAsync();
            RequestClose?.Invoke(true);
        }

        private async Task RefreshProcessedPreviewAsync()
        {
            if (_originalJpeg == null || _shutdown == 1)
            {
                return;
            }

            int version = Interlocked.Increment(ref _enhanceVersion);
            byte[] original = _originalJpeg;
            DocumentCameraCaptureSettings snapshot = _settings.Clone();
            IsBusy = true;
            try
            {
                byte[] processed = await Task.Run(() => DocumentCameraImageEnhanceSupport.EnhanceJpeg(original, snapshot));
                if (version != _enhanceVersion || _shutdown == 1)
                {
                    return;
                }

                _capturedJpeg = processed;
                PreviewImage = DocumentCameraImageSupport.TryCreateBitmapSource(processed)
                    ?? DocumentCameraImageSupport.TryCreateBitmapSource(original);
            }
            catch (Exception ex)
            {
                if (version != _enhanceVersion || _shutdown == 1)
                {
                    return;
                }

                _capturedJpeg = original;
                PreviewImage = DocumentCameraImageSupport.TryCreateBitmapSource(original);
                StatusText = "影像处理失败，已显示原图。可关闭部分开关后重试。";
                _dialogService.ShowError($"影像处理失败：{ex.Message}");
            }
            finally
            {
                if (version == _enhanceVersion)
                {
                    IsBusy = false;
                    NotifyCaptureState();
                }
            }
        }

        private void RestoreDefaults()
        {
            string? lastDevice = _settings.LastDeviceIdentity;
            _suppressSettingPersist = true;
            try
            {
                _settings = DocumentCameraCaptureSettings.CreateDefault();
                _settings.LastDeviceIdentity = lastDevice;
                NotifySettingsChanged();
                BindResolutions(SelectedDevice);
            }
            finally
            {
                _suppressSettingPersist = false;
            }

            PersistSettings();
            if (IsFrozen)
            {
                _ = RefreshProcessedPreviewAsync();
            }
            else if (SelectedDevice != null)
            {
                _ = RestartPreviewAsync();
            }
        }

        private void SetSetting(string propertyName, Action assign, bool affectsLivePreview = false)
        {
            assign();
            OnPropertyChanged(propertyName);
            if (affectsLivePreview)
            {
                OnPropertyChanged(nameof(PreviewMirrorScaleX));
            }

            QueuePersist();
            _ = RefreshProcessedPreviewAsync();
        }

        private void NotifySettingsChanged()
        {
            OnPropertyChanged(nameof(AutoCrop));
            OnPropertyChanged(nameof(PerspectiveCorrection));
            OnPropertyChanged(nameof(RemoveBlackBorder));
            OnPropertyChanged(nameof(WhiteDocument));
            OnPropertyChanged(nameof(RemoveShadow));
            OnPropertyChanged(nameof(AutoDeskew));
            OnPropertyChanged(nameof(Mirror));
            OnPropertyChanged(nameof(Sharpen));
            OnPropertyChanged(nameof(RotationDegrees));
            OnPropertyChanged(nameof(RotationDisplay));
            OnPropertyChanged(nameof(PreviewRotationAngle));
            OnPropertyChanged(nameof(PreviewMirrorScaleX));
            OnPropertyChanged(nameof(Brightness));
            OnPropertyChanged(nameof(Contrast));
            OnPropertyChanged(nameof(ColorMode));
        }

        private void QueuePersist()
        {
            if (_suppressSettingPersist)
            {
                return;
            }

            _persistTimer.Stop();
            _persistTimer.Start();
        }

        private void PersistSettings()
        {
            if (_suppressSettingPersist)
            {
                return;
            }

            try
            {
                if (SelectedDevice != null)
                {
                    _settings.LastDeviceIdentity = SelectedDevice.Identity;
                }

                if (SelectedResolution != null)
                {
                    _settings.CaptureWidth = SelectedResolution.Width;
                    _settings.CaptureHeight = SelectedResolution.Height;
                }

                _settingsStore.Save(CurrentUserId, _settings);
            }
            catch (Exception)
            {
                // 本机偏好写入失败不影响拍摄。
            }
        }

        private int? CurrentUserId => _userContextService.CurrentUser?.Id;

        private void NotifyCaptureState()
        {
            CommandManager.InvalidateRequerySuggested();
            OnPropertyChanged(nameof(CanCapture));
            OnPropertyChanged(nameof(CanRetake));
            OnPropertyChanged(nameof(CanConfirm));
            OnPropertyChanged(nameof(CanChangeDevice));
            OnPropertyChanged(nameof(ShowPreviewPlaceholder));
            OnPropertyChanged(nameof(PreviewRotationAngle));
            OnPropertyChanged(nameof(PreviewMirrorScaleX));
        }
    }
}
