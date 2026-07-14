using System.ComponentModel;

using System.Runtime.CompilerServices;

using System.Windows;

using System.Windows.Threading;



namespace DocMgr.Infrastructure.Startup

{

    /// <summary>

    /// 应用启动阶段状态，供登录界面与数据库审计日志门控使用。

    /// </summary>

    public sealed class AppInitializationState : INotifyPropertyChanged

    {

        private bool _isLoginReady;

        private bool _isFullyInitialized;

        private string _statusMessage = "正在初始化系统，请稍候…";

        private string? _errorMessage;



        /// <summary>

        /// 数据库结构已就绪，可执行登录（后台维护可能仍在进行）。

        /// </summary>

        public bool IsLoginReady => _isLoginReady;



        /// <summary>

        /// 启动阶段尚未完成到可登录（仅用于审计日志门控）。

        /// </summary>

        public bool IsInitializing => !_isLoginReady && !HasFailed;



        public bool IsFullyInitialized => _isFullyInitialized;



        public bool IsBackgroundMaintenanceRunning => _isLoginReady && !_isFullyInitialized && !HasFailed;



        public bool HasFailed => !string.IsNullOrWhiteSpace(_errorMessage);



        public string StatusMessage => _statusMessage;



        public string? ErrorMessage => _errorMessage;



        public bool IsLoginEnabled => _isLoginReady && !HasFailed;



        public void ReportProgress(string message)

        {

            if (string.IsNullOrWhiteSpace(message))

            {

                return;

            }



            _statusMessage = message.Trim();

            NotifyStateChanged();

        }



        /// <summary>

        /// 数据库已可支撑登录，允许用户进入系统；耗时维护任务继续在后台执行。

        /// </summary>

        public void MarkLoginReady(string? message = null)

        {

            _isLoginReady = true;

            _errorMessage = null;

            _statusMessage = string.IsNullOrWhiteSpace(message)

                ? "可以登录，系统正在后台同步数据…"

                : message.Trim();

            NotifyStateChanged();

        }



        public void MarkFullyReady()

        {

            _isLoginReady = true;

            _isFullyInitialized = true;

            _errorMessage = null;

            _statusMessage = "系统已就绪。";

            NotifyStateChanged();

        }



        public void MarkFailed(Exception exception)

        {

            ArgumentNullException.ThrowIfNull(exception);



            _isLoginReady = false;

            _isFullyInitialized = false;

            _errorMessage = exception.Message;

            _statusMessage = "系统初始化失败，请联系管理员。";

            NotifyStateChanged();

        }



        public event PropertyChangedEventHandler? PropertyChanged;



        private void NotifyStateChanged()

        {

            Dispatcher? dispatcher = Application.Current?.Dispatcher;

            if (dispatcher != null && !dispatcher.CheckAccess())

            {

                dispatcher.BeginInvoke(NotifyStateChangedCore, DispatcherPriority.Normal);

                return;

            }



            NotifyStateChangedCore();

        }



        private void NotifyStateChangedCore()

        {

            OnPropertyChanged(nameof(IsLoginReady));

            OnPropertyChanged(nameof(IsInitializing));

            OnPropertyChanged(nameof(IsFullyInitialized));

            OnPropertyChanged(nameof(IsBackgroundMaintenanceRunning));

            OnPropertyChanged(nameof(HasFailed));

            OnPropertyChanged(nameof(StatusMessage));

            OnPropertyChanged(nameof(ErrorMessage));

            OnPropertyChanged(nameof(IsLoginEnabled));

        }



        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)

        {

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        }

    }

}


