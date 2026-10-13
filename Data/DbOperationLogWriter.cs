using DocMgr.Models.SystemSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace DocMgr.Data
{
    /// <summary>
    /// 使用独立 DbContext 写入操作日志，避免拦截器递归。
    /// </summary>
    public sealed class DbOperationLogWriter
    {
        private readonly DocMgrDatabaseSettings _databaseSettings;

        public DbOperationLogWriter(DocMgrDatabaseSettings databaseSettings)
        {
            _databaseSettings = databaseSettings;
        }

        public async Task WriteAsync(
            IReadOnlyList<DbOperationLog> logs,
            DbContext currentContext,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(currentContext);

            if (logs.Count == 0)
            {
                return;
            }

            var activeTransaction = currentContext.Database.CurrentTransaction;
            if (activeTransaction == null)
            {
                await WriteAsync(logs, cancellationToken).ConfigureAwait(false);
                return;
            }

            var connection = currentContext.Database.GetDbConnection();
            bool shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            try
            {
                foreach (var log in logs)
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = activeTransaction.GetDbTransaction();
                    command.CommandText = """
                        INSERT INTO [DbOperationLogs] (
                            [OperationTime],
                            [UserId],
                            [UserName],
                            [SessionId],
                            [SourcePage],
                            [SourceButton],
                            [EntityType],
                            [TableName],
                            [EntityKey],
                            [Operation],
                            [ChangedColumns],
                            [Summary]
                        )
                        VALUES (
                            @operationTime,
                            @userId,
                            @userName,
                            @sessionId,
                            @sourcePage,
                            @sourceButton,
                            @entityType,
                            @tableName,
                            @entityKey,
                            @operation,
                            @changedColumns,
                            @summary
                        );
                        """;

                    AddParameter(command, "@operationTime", log.OperationTime);
                    AddParameter(command, "@userId", log.UserId);
                    AddParameter(command, "@userName", log.UserName);
                    AddParameter(command, "@sessionId", log.SessionId);
                    AddParameter(command, "@sourcePage", log.SourcePage);
                    AddParameter(command, "@sourceButton", log.SourceButton);
                    AddParameter(command, "@entityType", log.EntityType);
                    AddParameter(command, "@tableName", log.TableName);
                    AddParameter(command, "@entityKey", log.EntityKey);
                    AddParameter(command, "@operation", log.Operation);
                    AddParameter(command, "@changedColumns", log.ChangedColumns);
                    AddParameter(command, "@summary", log.Summary);

                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync().ConfigureAwait(false);
                }
            }
        }

        public async Task WriteAsync(IReadOnlyList<DbOperationLog> logs, CancellationToken cancellationToken = default)
        {
            if (logs.Count == 0)
            {
                return;
            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(_databaseSettings.ConnectionString)
                .AddInterceptors(new Interceptors.SqlServerIdentityKeyNormalizeInterceptor())
                .Options;

            await using var context = new AppDbContext(options);
            context.DbOperationLogs.AddRange(logs);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void AddParameter(System.Data.Common.DbCommand command, string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}
