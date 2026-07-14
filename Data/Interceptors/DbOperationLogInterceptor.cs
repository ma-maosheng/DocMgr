using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using DocMgr.Infrastructure.Startup;
using DocMgr.Models.SystemSettings;
using DocMgr.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DocMgr.Data.Interceptors
{
    /// <summary>
    /// 在 SaveChanges 成功后写入数据库操作审计日志。
    /// </summary>
    public sealed class DbOperationLogInterceptor : SaveChangesInterceptor
    {
        private static readonly HashSet<string> ExcludedEntityTypes = new(StringComparer.Ordinal)
        {
            nameof(DbOperationLog)
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly IUserContextService _userContextService;
        private readonly IDbOperationLogContextService _logContextService;
        private readonly AppInitializationState _initializationState;
        private readonly DbOperationLogWriter _logWriter;
        private readonly List<DbOperationLog> _pendingLogs = new();
        private readonly List<(DbOperationLog Log, EntityEntry Entry)> _pendingEntries = new();

        public DbOperationLogInterceptor(
            IUserContextService userContextService,
            IDbOperationLogContextService logContextService,
            AppInitializationState initializationState,
            DbOperationLogWriter logWriter)
        {
            _userContextService = userContextService;
            _logContextService = logContextService;
            _initializationState = initializationState;
            _logWriter = logWriter;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            CapturePendingLogs(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CapturePendingLogs(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            FlushPendingLogsAsync(eventData.Context, result).GetAwaiter().GetResult();
            return base.SavedChanges(eventData, result);
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            await FlushPendingLogsAsync(eventData.Context, result, cancellationToken);
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            ClearPending();
            base.SaveChangesFailed(eventData);
        }

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            ClearPending();
            return base.SaveChangesFailedAsync(eventData, cancellationToken);
        }

        private void ClearPending()
        {
            _pendingLogs.Clear();
            _pendingEntries.Clear();
        }

        private void CapturePendingLogs(DbContext? context)
        {
            ClearPending();
            if (context == null || _initializationState.IsInitializing || !_logContextService.IsRecordingEnabled)
            {
                return;
            }

            string sourcePage = _logContextService.CurrentPageName ?? string.Empty;
            string sourceButton = _logContextService.CurrentButtonName ?? string.Empty;

            var user = _userContextService.CurrentUser;
            string userName = user?.RealName?.Trim() ?? user?.LoginName?.Trim() ?? "系统";
            int? userId = user?.Id;
            string? sessionId = _userContextService.CurrentSessionId;
            DateTime operationTime = DateTime.Now;

            static void ApplySessionContext(
                EntityEntry entry,
                ref int? currentUserId,
                ref string currentUserName,
                ref string? currentSessionId)
            {
                if (entry.Entity is not UserSession session)
                {
                    return;
                }

                currentUserId ??= session.UserId;
                currentSessionId ??= session.SessionId;

                if (string.Equals(currentUserName, "系统", StringComparison.Ordinal))
                {
                    User? sessionUser = session.User;
                    string? resolvedName = sessionUser?.RealName?.Trim();
                    if (string.IsNullOrEmpty(resolvedName))
                    {
                        resolvedName = sessionUser?.LoginName?.Trim();
                    }

                    if (!string.IsNullOrEmpty(resolvedName))
                    {
                        currentUserName = resolvedName;
                    }
                    else if (session.UserId > 0)
                    {
                        currentUserName = $"用户#{session.UserId}";
                    }
                }
            }

            foreach (EntityEntry entry in context.ChangeTracker.Entries())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                {
                    continue;
                }

                if (ShouldSkipEntry(entry))
                {
                    continue;
                }

                int? logUserId = userId;
                string logUserName = userName;
                string? logSessionId = sessionId;
                ApplySessionContext(entry, ref logUserId, ref logUserName, ref logSessionId);

                string entityType = entry.Entity.GetType().Name;
                string tableName = entry.Metadata.GetTableName() ?? entityType;
                string operation = entry.State.ToString();
                string changedColumns = BuildChangedColumnsJson(entry, operation);
                string entityKey = BuildEntityKey(entry);
                string summary = BuildSummary(entityType, entityKey, operation, changedColumns);

                var log = new DbOperationLog
                {
                    OperationTime = operationTime,
                    UserId = logUserId,
                    UserName = logUserName,
                    SessionId = logSessionId,
                    SourcePage = sourcePage,
                    SourceButton = sourceButton,
                    EntityType = entityType,
                    TableName = tableName,
                    EntityKey = entityKey,
                    Operation = operation,
                    ChangedColumns = changedColumns,
                    Summary = summary
                };

                _pendingLogs.Add(log);
                _pendingEntries.Add((log, entry));
            }
        }

        private async Task FlushPendingLogsAsync(DbContext? context, int result, CancellationToken cancellationToken = default)
        {
            if (result <= 0 || _pendingLogs.Count == 0)
            {
                ClearPending();
                return;
            }

            try
            {
                FinalizePendingLogsBeforeWrite();
                IReadOnlyList<DbOperationLog> batch = _pendingLogs.ToList();
                ClearPending();
                var stopwatch = Stopwatch.StartNew();
                if (context != null)
                {
                    await _logWriter.WriteAsync(batch, context, cancellationToken);
                }
                else
                {
                    await _logWriter.WriteAsync(batch, cancellationToken);
                }

                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds >= 500)
                {
                    System.Diagnostics.Debug.WriteLine($"[DbOperationLog] 写入耗时 {stopwatch.ElapsedMilliseconds}ms，条数={batch.Count}");
                }

                _logContextService.ClearCurrentButtonName();
            }
            catch (Exception ex)
            {
                ClearPending();
                System.Diagnostics.Debug.WriteLine($"[DbOperationLog] 写入失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 新增记录在 SaveChanges 前主键可能尚未生成，保存成功后补全主键与字段快照。
        /// </summary>
        private void FinalizePendingLogsBeforeWrite()
        {
            foreach ((DbOperationLog log, EntityEntry entry) in _pendingEntries)
            {
                if (!string.Equals(log.Operation, EntityState.Added.ToString(), StringComparison.Ordinal))
                {
                    continue;
                }

                log.EntityKey = BuildEntityKey(entry);
                log.ChangedColumns = BuildChangedColumnsJson(entry, log.Operation);
                log.Summary = BuildSummary(log.EntityType, log.EntityKey, log.Operation, log.ChangedColumns);
            }
        }

        private static bool ShouldSkipEntry(EntityEntry entry)
        {
            string entityType = entry.Entity.GetType().Name;
            if (ExcludedEntityTypes.Contains(entityType))
            {
                return true;
            }

            if (entry.Entity is UserSession && entry.State == EntityState.Modified)
            {
                var modifiedNames = entry.Properties
                    .Where(property => property.IsModified)
                    .Select(property => property.Metadata.Name)
                    .ToList();

                if (modifiedNames.Count == 1
                    && string.Equals(modifiedNames[0], nameof(UserSession.LastHeartbeatTime), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildEntityKey(EntityEntry entry)
        {
            var keyParts = entry.Properties
                .Where(property => property.Metadata.IsPrimaryKey())
                .Select(property => $"{property.Metadata.Name}={FormatScalar(property.CurrentValue ?? property.OriginalValue)}")
                .ToList();

            if (keyParts.Count > 0)
            {
                return string.Join(", ", keyParts);
            }

            return entry.State == EntityState.Added ? "(待生成主键)" : "(无主键)";
        }

        private static string BuildChangedColumnsJson(EntityEntry entry, string operation)
        {
            IReadOnlyList<DbOperationFieldChange> fields = operation switch
            {
                nameof(EntityState.Added) => BuildAddedFieldChanges(entry),
                nameof(EntityState.Deleted) => BuildDeletedFieldChanges(entry),
                _ => BuildModifiedFieldChanges(entry)
            };

            return JsonSerializer.Serialize(fields, JsonOptions);
        }

        private static List<DbOperationFieldChange> BuildAddedFieldChanges(EntityEntry entry)
        {
            return entry.Properties
                .OrderBy(property => property.Metadata.IsPrimaryKey() ? 0 : 1)
                .ThenBy(property => property.Metadata.Name, StringComparer.Ordinal)
                .Select(property => new DbOperationFieldChange
                {
                    Name = property.Metadata.Name,
                    ColumnName = property.Metadata.GetColumnName() ?? property.Metadata.Name,
                    ClrType = GetClrTypeName(property),
                    IsPrimaryKey = property.Metadata.IsPrimaryKey(),
                    IsChanged = true,
                    OldValue = null,
                    NewValue = FormatValueText(property.CurrentValue)
                })
                .ToList();
        }

        private static List<DbOperationFieldChange> BuildModifiedFieldChanges(EntityEntry entry)
        {
            return entry.Properties
                .OrderBy(property => property.Metadata.IsPrimaryKey() ? 0 : 1)
                .ThenBy(property => property.Metadata.Name, StringComparer.Ordinal)
                .Select(property => new DbOperationFieldChange
                {
                    Name = property.Metadata.Name,
                    ColumnName = property.Metadata.GetColumnName() ?? property.Metadata.Name,
                    ClrType = GetClrTypeName(property),
                    IsPrimaryKey = property.Metadata.IsPrimaryKey(),
                    IsChanged = property.IsModified,
                    OldValue = FormatValueText(property.OriginalValue),
                    NewValue = FormatValueText(property.CurrentValue)
                })
                .ToList();
        }

        private static List<DbOperationFieldChange> BuildDeletedFieldChanges(EntityEntry entry)
        {
            return entry.Properties
                .OrderBy(property => property.Metadata.IsPrimaryKey() ? 0 : 1)
                .ThenBy(property => property.Metadata.Name, StringComparer.Ordinal)
                .Select(property => new DbOperationFieldChange
                {
                    Name = property.Metadata.Name,
                    ColumnName = property.Metadata.GetColumnName() ?? property.Metadata.Name,
                    ClrType = GetClrTypeName(property),
                    IsPrimaryKey = property.Metadata.IsPrimaryKey(),
                    IsChanged = true,
                    OldValue = FormatValueText(property.OriginalValue),
                    NewValue = null
                })
                .ToList();
        }

        private static string GetClrTypeName(PropertyEntry property)
        {
            Type clrType = property.Metadata.ClrType;
            Type underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;
            return underlyingType.Name;
        }

        private static object? NormalizeValue(object? value)
        {
            if (value == null)
            {
                return null;
            }

            return value switch
            {
                DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"),
                byte[] bytes => Convert.ToBase64String(bytes),
                Enum enumValue => enumValue.ToString(),
                bool or byte or sbyte or short or ushort or int or uint or long or ulong
                    or float or double or decimal or char or string or Guid => value,
                _ => value.ToString()
            };
        }

        private static string? FormatValueText(object? value)
        {
            if (value == null)
            {
                return null;
            }

            object? normalized = NormalizeValue(value);
            return normalized?.ToString();
        }

        private static string BuildSummary(string entityType, string entityKey, string operation, string changedColumnsJson)
        {
            try
            {
                var fields = JsonSerializer.Deserialize<List<DbOperationFieldChange>>(changedColumnsJson, JsonOptions) ?? new List<DbOperationFieldChange>();
                if (fields.Count == 0)
                {
                    return $"{operation} {entityType} [{entityKey}]";
                }

                if (string.Equals(operation, EntityState.Added.ToString(), StringComparison.Ordinal))
                {
                    var valueParts = fields
                        .Select(field => $"{field.Name}={FormatSummaryValue(field.NewValue)}")
                        .Take(8)
                        .ToList();
                    string suffix = fields.Count > valueParts.Count ? "…" : string.Empty;
                    return $"{operation} {entityType} [{entityKey}]：{string.Join("；", valueParts)}{suffix}";
                }

                if (string.Equals(operation, EntityState.Deleted.ToString(), StringComparison.Ordinal))
                {
                    var valueParts = fields
                        .Select(field => $"{field.Name}={FormatSummaryValue(field.OldValue)}")
                        .Take(8)
                        .ToList();
                    string suffix = fields.Count > valueParts.Count ? "…" : string.Empty;
                    return $"{operation} {entityType} [{entityKey}]：{string.Join("；", valueParts)}{suffix}";
                }

                var changeParts = fields
                    .Where(field => field.IsChanged)
                    .Select(field => $"{field.Name}: {FormatSummaryValue(field.OldValue)} → {FormatSummaryValue(field.NewValue)}")
                    .ToList();

                if (changeParts.Count == 0)
                {
                    changeParts = fields
                        .Select(field => $"{field.Name}: {FormatSummaryValue(field.OldValue)} → {FormatSummaryValue(field.NewValue)}")
                        .Take(8)
                        .ToList();
                }

                return $"{operation} {entityType} [{entityKey}]：{string.Join("；", changeParts)}";
            }
            catch
            {
                return $"{operation} {entityType} [{entityKey}]";
            }
        }

        private static string FormatSummaryValue(string? value)
        {
            if (value == null)
            {
                return "null";
            }

            string text = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 80 ? text : text[..77] + "...";
        }

        private static string FormatScalar(object? value)
        {
            object? normalized = NormalizeValue(value);
            return normalized switch
            {
                null => "null",
                _ => normalized.ToString() ?? string.Empty
            };
        }

        private sealed class DbOperationFieldChange
        {
            public string Name { get; set; } = string.Empty;

            public string ColumnName { get; set; } = string.Empty;

            public string ClrType { get; set; } = string.Empty;

            public bool IsPrimaryKey { get; set; }

            public bool IsChanged { get; set; }

            public string? OldValue { get; set; }

            public string? NewValue { get; set; }
        }
    }
}
