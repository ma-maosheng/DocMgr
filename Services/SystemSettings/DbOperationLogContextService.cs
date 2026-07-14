using System.ComponentModel;
using System.Runtime.CompilerServices;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.SystemSettings
{
    public sealed class DbOperationLogContextService : IDbOperationLogContextService
    {
        private bool _isRecordingEnabled = false;
        private string? _currentPageName;
        private string? _currentButtonName;

        public bool IsRecordingEnabled => _isRecordingEnabled;

        public string? CurrentPageName => _currentPageName;

        public string? CurrentButtonName => _currentButtonName;

        public void SetRecordingEnabled(bool enabled)
        {
            if (_isRecordingEnabled == enabled)
            {
                return;
            }

            _isRecordingEnabled = enabled;
            OnPropertyChanged(nameof(IsRecordingEnabled));
        }

        public void SetCurrentPage(string? pageName)
        {
            string? normalized = Normalize(pageName);
            if (string.Equals(_currentPageName, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _currentPageName = normalized;
            ClearCurrentButtonName();
            OnPropertyChanged(nameof(CurrentPageName));
        }

        public void CaptureButtonAction(string? buttonName)
        {
            string? normalized = Normalize(buttonName);
            if (string.Equals(_currentButtonName, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _currentButtonName = normalized;
            OnPropertyChanged(nameof(CurrentButtonName));
        }

        public void ClearCurrentButtonName()
        {
            if (_currentButtonName == null)
            {
                return;
            }

            _currentButtonName = null;
            OnPropertyChanged(nameof(CurrentButtonName));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
