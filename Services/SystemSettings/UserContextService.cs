using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DocMgr.Services.SystemSettings
{
    public class UserContextService : IUserContextService
    {
        private User? _currentUser;
        private string? _currentSessionId;

        public User? CurrentUser => _currentUser;

        public string? CurrentSessionId => _currentSessionId;

        public void SetCurrentSession(User user, string sessionId)
        {
            ArgumentNullException.ThrowIfNull(user);

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("SessionId cannot be empty.", nameof(sessionId));
            }

            _currentUser = user;
            _currentSessionId = sessionId;
            OnPropertyChanged(nameof(CurrentUser));
            OnPropertyChanged(nameof(CurrentSessionId));
        }

        public void Clear()
        {
            _currentUser = null;
            _currentSessionId = null;
            OnPropertyChanged(nameof(CurrentUser));
            OnPropertyChanged(nameof(CurrentSessionId));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}