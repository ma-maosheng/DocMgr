using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace DocMgr.Services.Shared
{
    public class ToDoCenterService : IToDoCenterService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IUserContextService _userContextService;

        private readonly List<ToDoItem> _items = new();
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        private DispatcherTimer? _timer;
        private EventHandler? _timerTickHandler;
        private int _pendingCount;

        private int _topN = 20;
        private int _refreshSeconds = 15;
        private bool _disposed;

        public bool EnableToDoPopup { get; private set; } = true;
        public bool EnableToDoBadge { get; private set; } = true;
        public bool MarkAllAsReadOnAcknowledge { get; private set; } = true;

        public ToDoCenterService(IServiceScopeFactory scopeFactory, IUserContextService userContextService)
        {
            _scopeFactory = scopeFactory;
            _userContextService = userContextService;
            _userContextService.PropertyChanged += UserContextService_PropertyChanged;
        }

        public int PendingCount
        {
            get => _pendingCount;
            private set
            {
                if (_pendingCount == value) return;
                _pendingCount = value;
                OnPropertyChanged();
            }
        }

        public IReadOnlyList<ToDoItem> Items => _items;

        public async Task ApplyPreferenceAsync(UserPreference preference)
        {
            if (preference == null) return;

            EnableToDoPopup = preference.EnableToDoPopup;
            EnableToDoBadge = preference.EnableToDoBadge;
            MarkAllAsReadOnAcknowledge = preference.MarkAllAsReadOnAcknowledge;

            _topN = preference.ToDoTopN <= 0 ? 20 : preference.ToDoTopN;
            _refreshSeconds = preference.ToDoRefreshSeconds <= 0 ? 15 : preference.ToDoRefreshSeconds;

            StopAutoRefresh();
            StartAutoRefresh(TimeSpan.FromSeconds(_refreshSeconds));

            await RefreshAsync(_topN);
        }

        public async Task InitializeAsync(int topN = 20)
        {
            _topN = topN <= 0 ? 20 : topN;
            await RefreshAsync(_topN);
        }

        public async Task RefreshAsync(int topN = 20)
        {
            if (_disposed) return;

            await _refreshLock.WaitAsync();
            try
            {
                var user = _userContextService.CurrentUser;
                if (user == null)
                {
                    UpdateItems(Array.Empty<ToDoItem>());
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var toDoService = scope.ServiceProvider.GetRequiredService<IToDoService>();

                var items = await toDoService.GetMyToDosAsync(user, topN);
                UpdateItems(items ?? new List<ToDoItem>());
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        public async Task MarkAsReadAsync(ToDoItem item)
        {
            if (item == null) return;

            var user = _userContextService.CurrentUser;
            if (user == null) return;

            using var scope = _scopeFactory.CreateScope();
            var toDoService = scope.ServiceProvider.GetRequiredService<IToDoService>();

            await toDoService.MarkAsReadAsync(user, item.Id);
            await RefreshAsync(_topN);
        }

        public async Task MarkAsReadAsync(IEnumerable<ToDoItem> items)
        {
            var user = _userContextService.CurrentUser;
            if (user == null || items == null) return;

            using var scope = _scopeFactory.CreateScope();
            var toDoService = scope.ServiceProvider.GetRequiredService<IToDoService>();

            var ids = System.Linq.Enumerable.Select(items, x => x.Id);
            await toDoService.MarkAsReadBatchAsync(user, ids);
            await RefreshAsync(_topN);
        }

        public void StartAutoRefresh(TimeSpan? interval = null)
        {
            if (_timer != null) return;

            _timer = new DispatcherTimer
            {
                Interval = interval ?? TimeSpan.FromSeconds(_refreshSeconds)
            };

            _timerTickHandler = async (_, _) => await RefreshAsync(_topN);
            _timer.Tick += _timerTickHandler;
            _timer.Start();
        }

        public void StopAutoRefresh()
        {
            if (_timer == null) return;

            _timer.Stop();
            if (_timerTickHandler != null)
            {
                _timer.Tick -= _timerTickHandler;
            }

            _timerTickHandler = null;
            _timer = null;
        }

        private async void UserContextService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IUserContextService.CurrentUser))
            {
                await RefreshAsync(_topN);
            }
        }

        private void UpdateItems(IEnumerable<ToDoItem> items)
        {
            void Apply()
            {
                _items.Clear();
                _items.AddRange(items);
                PendingCount = _items.Count;
                OnPropertyChanged(nameof(Items));
            }

            if (Application.Current?.Dispatcher?.CheckAccess() == true)
            {
                Apply();
            }
            else
            {
                Application.Current?.Dispatcher?.Invoke(Apply);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            StopAutoRefresh();
            _userContextService.PropertyChanged -= UserContextService_PropertyChanged;
            _refreshLock.Dispose();
        }
    }
}