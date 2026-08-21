using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Shared
{
    /// <summary>
    /// 操作进度对话框展示状态。
    /// </summary>
    public sealed class OperationProgressDialogViewModel : ViewModelBase
    {
        private string _title = "处理中";
        private string _statusText = "请稍候…";
        private string _percentText = string.Empty;
        private double _progressValue;
        private bool _isIndeterminate = true;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string PercentText
        {
            get => _percentText;
            set => SetProperty(ref _percentText, value);
        }

        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set => SetProperty(ref _isIndeterminate, value);
        }

        public void ApplyIndeterminate(string? status)
        {
            IsIndeterminate = true;
            ProgressValue = 0;
            PercentText = string.Empty;
            if (!string.IsNullOrWhiteSpace(status))
            {
                StatusText = status.Trim();
            }
        }

        public void ApplyReport(int current, int total, string? status)
        {
            if (total <= 0)
            {
                ApplyIndeterminate(status);
                return;
            }

            int safeCurrent = current < 0 ? 0 : current;
            if (safeCurrent > total)
            {
                safeCurrent = total;
            }

            IsIndeterminate = false;
            ProgressValue = total == 0 ? 0 : 100d * safeCurrent / total;
            PercentText = $"{safeCurrent} / {total}";
            if (!string.IsNullOrWhiteSpace(status))
            {
                StatusText = status.Trim();
            }
        }
    }
}
