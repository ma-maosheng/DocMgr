using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.SystemSettings
{
    public class DbOperationLogPageViewModel : ViewModelBase
    {
        private readonly IDbOperationLogService _logService;
        private readonly IDbOperationLogContextService _logContextService;
        private readonly IDialogService _dialogService;

        public DbOperationLogPageViewModel(
            IDbOperationLogService logService,
            IDbOperationLogContextService logContextService,
            IDialogService dialogService)
        {
            _logService = logService;
            _logContextService = logContextService;
            _dialogService = dialogService;
            _logContextService.PropertyChanged += LogContextService_PropertyChanged;

            OperationOptions = new ObservableCollection<string>(new[] { "全部", "Added", "Modified", "Deleted" });
            TableNameOptions = new ObservableCollection<string> { "全部" };
            Logs = new ObservableCollection<DbOperationLog>();

            SelectedOperation = "全部";
            SelectedTableName = "全部";
            ResetDefaultDateRange();

            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            ViewDetailCommand = new RelayCommand(async _ => await ViewDetailAsync(), _ => SelectedLog != null);
            ClearAllCommand = new RelayCommand(async _ => await ClearAllAsync());
            StopRecordingCommand = new RelayCommand(
                _ => SetRecordingEnabled(false),
                _ => _logContextService.IsRecordingEnabled);
            StartRecordingCommand = new RelayCommand(
                _ => SetRecordingEnabled(true),
                _ => !_logContextService.IsRecordingEnabled);

            UpdateRecordingStatusText();
        }

        private void LogContextService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(IDbOperationLogContextService.IsRecordingEnabled))
            {
                UpdateRecordingStatusText();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void SetRecordingEnabled(bool enabled)
        {
            _logContextService.SetRecordingEnabled(enabled);
            UpdateRecordingStatusText();
            CommandManager.InvalidateRequerySuggested();
        }

        private void UpdateRecordingStatusText()
        {
            RecordingStatusText = _logContextService.IsRecordingEnabled ? "记录状态：正在记录" : "记录状态：已停止";
        }

        /// <summary>
        /// 页面显示时调用：刷新结束日期并重新加载，避免构造阶段与 DatePicker 绑定竞态导致查询范围错误。
        /// </summary>
        public Task LoadOnPageDisplayedAsync() => RefreshAsync();

        public void ResetDefaultDateRange()
        {
            EndTime = DateTime.Today;
            StartTime = DateTime.Today.AddDays(-7);
        }

        public async Task RefreshAsync()
        {
            EndTime = DateTime.Today;
            await LoadAsync();
        }

        public ObservableCollection<DbOperationLog> Logs { get; }

        public ObservableCollection<string> OperationOptions { get; }

        public ObservableCollection<string> TableNameOptions { get; }

        private DbOperationLog? _selectedLog;
        public DbOperationLog? SelectedLog
        {
            get => _selectedLog;
            set => SetProperty(ref _selectedLog, value);
        }

        private DateTime? _startTime;
        public DateTime? StartTime
        {
            get => _startTime;
            set => SetProperty(ref _startTime, value);
        }

        private DateTime? _endTime;
        public DateTime? EndTime
        {
            get => _endTime;
            set => SetProperty(ref _endTime, value);
        }

        private string _selectedOperation = "全部";
        public string SelectedOperation
        {
            get => _selectedOperation;
            set => SetProperty(ref _selectedOperation, value);
        }

        private string _selectedTableName = "全部";
        public string SelectedTableName
        {
            get => _selectedTableName;
            set => SetProperty(ref _selectedTableName, value);
        }

        private string _keyword = string.Empty;
        public string Keyword
        {
            get => _keyword;
            set => SetProperty(ref _keyword, value);
        }

        private string _statusText = string.Empty;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private string _recordingStatusText = string.Empty;
        public string RecordingStatusText
        {
            get => _recordingStatusText;
            private set => SetProperty(ref _recordingStatusText, value);
        }

        public RelayCommand RefreshCommand { get; }

        public RelayCommand ViewDetailCommand { get; }

        public RelayCommand ClearAllCommand { get; }

        public RelayCommand StopRecordingCommand { get; }

        public RelayCommand StartRecordingCommand { get; }

        private async Task ClearAllAsync()
        {
            if (!_dialogService.ShowConfirm("确定要清除所有数据库操作日志吗？此操作不可恢复。", "清除日志"))
            {
                return;
            }

            try
            {
                int deleted = await _logService.ClearAllAsync();
                await RefreshAsync();
                await RunOnUiAsync(() => StatusText = $"已清除 {deleted} 条日志。{RecordingStatusText}");
            }
            catch (Exception ex)
            {
                await RunOnUiAsync(() => StatusText = "清除失败：" + ex.Message);
            }
        }

        private async Task LoadAsync()
        {
            try
            {
                DateTime start = (StartTime ?? DateTime.Today.AddDays(-7)).Date;
                DateTime endInclusive = (EndTime ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);

                var query = new DbOperationLogQuery
                {
                    StartTime = start,
                    EndTime = endInclusive,
                    TableName = string.Equals(SelectedTableName, "全部", StringComparison.Ordinal) ? null : SelectedTableName,
                    Operation = string.Equals(SelectedOperation, "全部", StringComparison.Ordinal) ? null : SelectedOperation,
                    Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim()
                };

                var logs = await _logService.SearchAsync(query);
                var tableNames = await _logService.GetDistinctTableNamesAsync();

                await RunOnUiAsync(() =>
                {
                    Logs.Clear();
                    foreach (var log in logs)
                    {
                        Logs.Add(log);
                    }

                    TableNameOptions.Clear();
                    TableNameOptions.Add("全部");
                    foreach (var tableName in tableNames)
                    {
                        TableNameOptions.Add(tableName);
                    }

                    if (!TableNameOptions.Contains(SelectedTableName))
                    {
                        SelectedTableName = "全部";
                    }

                    StatusText = $"共 {Logs.Count} 条记录（默认最多显示最近 500 条）。{RecordingStatusText}";
                });
            }
            catch (Exception ex)
            {
                await RunOnUiAsync(() => StatusText = "加载失败：" + ex.Message);
            }
        }

        private static Task RunOnUiAsync(Action action)
        {
            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action, DispatcherPriority.Normal).Task;
        }

        private async Task ViewDetailAsync()
        {
            if (SelectedLog == null)
            {
                return;
            }

            DbOperationLog? log = await _logService.GetByIdAsync(SelectedLog.Id) ?? SelectedLog;
            string content = BuildDetailContent(log);
            _dialogService.ShowTextDetailDialog(content, "数据库操作日志详情");
        }

        private static string BuildDetailContent(DbOperationLog log)
        {
            var builder = new StringBuilder();

            builder.AppendLine("======== 基本信息 ========");
            builder.AppendLine($"日志编号：{log.Id}");
            builder.AppendLine($"操作时间：{log.OperationTime:yyyy-MM-dd HH:mm:ss.fff}");
            builder.AppendLine($"操作人：{log.UserName}");
            builder.AppendLine($"用户编号：{log.UserId?.ToString() ?? "—"}");
            builder.AppendLine($"会话编号：{log.SessionId ?? "—"}");
            builder.AppendLine($"来源页面：{FormatOptionalText(log.SourcePage)}");
            builder.AppendLine($"来源按钮：{FormatOptionalText(log.SourceButton)}");
            builder.AppendLine($"操作类型：{log.Operation}");
            builder.AppendLine($"数据库表：{log.TableName}");
            builder.AppendLine($"实体类型：{log.EntityType}");
            builder.AppendLine($"主键标识：{log.EntityKey}");
            builder.AppendLine();
            builder.AppendLine("======== 变更摘要 ========");
            builder.AppendLine(log.Summary);
            builder.AppendLine();
            builder.AppendLine("======== 字段级变更明细 ========");
            builder.AppendLine(FormatFieldChanges(log.ChangedColumns, log.Operation));
            builder.AppendLine();
            builder.AppendLine("======== 变更 JSON 原始数据 ========");
            builder.AppendLine(FormatRawChangedColumnsJson(log.ChangedColumns));

            return builder.ToString().TrimEnd();
        }

        private static string FormatRawChangedColumnsJson(string changedColumnsJson)
        {
            if (string.IsNullOrWhiteSpace(changedColumnsJson))
            {
                return "(无)";
            }

            try
            {
                using var document = JsonDocument.Parse(changedColumnsJson);
                return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return changedColumnsJson;
            }
        }

        private static string FormatFieldChanges(string changedColumnsJson, string operation)
        {
            if (string.IsNullOrWhiteSpace(changedColumnsJson))
            {
                return "(无字段变更记录)";
            }

            try
            {
                using var document = JsonDocument.Parse(changedColumnsJson);
                if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
                {
                    return "(无字段变更记录)";
                }

                var lines = new List<string>();
                int index = 1;
                foreach (JsonElement field in document.RootElement.EnumerateArray())
                {
                    string name = ReadStringProperty(field, "Name") ?? "?";
                    string columnName = ReadStringProperty(field, "ColumnName") ?? name;
                    string clrType = ReadStringProperty(field, "ClrType") ?? "?";
                    bool isPrimaryKey = field.TryGetProperty("IsPrimaryKey", out JsonElement pkElement) && pkElement.ValueKind == JsonValueKind.True;
                    bool isChanged = !field.TryGetProperty("IsChanged", out JsonElement changedElement)
                        || changedElement.ValueKind == JsonValueKind.True;

                    string? oldValue = ReadFieldValue(field, "OldValue", "Old");
                    string? newValue = ReadFieldValue(field, "NewValue", "New");

                    lines.Add($"{index}. [{name}] 列={columnName}, 类型={clrType}{(isPrimaryKey ? ", 主键" : string.Empty)}{(isChanged ? ", 已变更" : string.Empty)}");

                    if (string.Equals(operation, "Added", StringComparison.Ordinal))
                    {
                        lines.Add($"   字段值：{FormatDisplayValue(newValue)}");
                    }
                    else if (string.Equals(operation, "Deleted", StringComparison.Ordinal))
                    {
                        lines.Add($"   字段值：{FormatDisplayValue(oldValue)}");
                    }
                    else
                    {
                        lines.Add($"   变更前：{FormatDisplayValue(oldValue)}");
                        lines.Add($"   变更后：{FormatDisplayValue(newValue)}");
                    }

                    lines.Add(string.Empty);
                    index++;
                }

                return string.Join(Environment.NewLine, lines).TrimEnd();
            }
            catch
            {
                return changedColumnsJson;
            }
        }

        private static string? ReadStringProperty(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out JsonElement valueElement) && valueElement.ValueKind == JsonValueKind.String
                ? valueElement.GetString()
                : null;
        }

        private static string? ReadFieldValue(JsonElement field, string primaryPropertyName, string legacyPropertyName)
        {
            if (field.TryGetProperty(primaryPropertyName, out JsonElement primaryElement))
            {
                return FormatJsonValue(primaryElement);
            }

            if (field.TryGetProperty(legacyPropertyName, out JsonElement legacyElement))
            {
                return FormatJsonValue(legacyElement);
            }

            return null;
        }

        private static string FormatOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        }

        private static string FormatDisplayValue(string? value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value.Length == 0)
            {
                return "(空字符串)";
            }

            return value;
        }

        private static string FormatJsonValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => "null",
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => element.ToString(),
                JsonValueKind.Array or JsonValueKind.Object => element.GetRawText(),
                _ => element.ToString()
            };
        }
    }
}
