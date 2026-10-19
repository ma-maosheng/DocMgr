using System.Runtime.ExceptionServices;

namespace DocMgr.ViewModels.Shared
{
    /// <summary>
    /// 把同一页面级数据库上下文上的操作排成一条队列。
    /// 同一次调用里的再次进入会直接执行；从界面事件再次进入则排队。
    /// 占用期间登记的后台操作，在本次释放后再按顺序执行。
    /// </summary>
    public sealed class PageDbOperationQueue
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly AsyncLocal<int> _depth = new();
        private readonly Queue<Func<Task>> _deferred = new();

        /// <summary>
        /// 当前异步调用已经占用队列时为 true。此时应直接等待内部查询，不要再排队。
        /// </summary>
        public bool IsReentrant => _depth.Value > 0;

        /// <summary>
        /// 独占执行。已在队列中时直接执行 <paramref name="action"/>。
        /// </summary>
        public Task RunAsync(Func<Task> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (_depth.Value > 0)
            {
                return action();
            }

            return RunExclusiveAsync(action);
        }

        /// <summary>
        /// 不阻塞当前调用。已在队列中时延后到本次释放之后，避免与正在执行的查询重叠。
        /// </summary>
        public Task RunInBackground(Func<Task> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (_depth.Value > 0)
            {
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_deferred)
                {
                    _deferred.Enqueue(async () =>
                    {
                        try
                        {
                            await action().ConfigureAwait(true);
                            completion.TrySetResult();
                        }
                        catch (Exception ex)
                        {
                            completion.TrySetException(ex);
                        }
                    });
                }

                _ = ObserveBackgroundAsync(completion.Task);
                return completion.Task;
            }

            Task operation = RunExclusiveAsync(action);
            _ = ObserveBackgroundAsync(operation);
            return operation;
        }

        private async Task RunExclusiveAsync(Func<Task> action)
        {
            await _gate.WaitAsync().ConfigureAwait(true);
            Exception? error = null;
            try
            {
                _depth.Value = 1;
                try
                {
                    await action().ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    _depth.Value = 0;
                }
            }
            finally
            {
                _gate.Release();
            }

            await DrainDeferredAsync().ConfigureAwait(true);
            if (error != null)
            {
                ExceptionDispatchInfo.Capture(error).Throw();
            }
        }

        private async Task DrainDeferredAsync()
        {
            while (true)
            {
                Func<Task>? next;
                lock (_deferred)
                {
                    if (_deferred.Count == 0)
                    {
                        return;
                    }

                    next = _deferred.Dequeue();
                }

                await RunExclusiveAsync(next).ConfigureAwait(true);
            }
        }

        private static async Task ObserveBackgroundAsync(Task operation)
        {
            try
            {
                await operation.ConfigureAwait(true);
            }
            catch
            {
                // 调用方未等待时，异常已由各业务方法弹窗提示。
            }
        }
    }
}
