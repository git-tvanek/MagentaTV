using MagentaTV.Configuration;
using Microsoft.Extensions.Options;

namespace MagentaTV.Services.Background
{
    /// <summary>
    /// Thread-safe implementace fronty pro background úlohy
    /// </summary>
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly PriorityQueue<BackgroundWorkItem, int> _workItems = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly ILogger<BackgroundTaskQueue> _logger;
        private readonly BackgroundServiceOptions _options;
        private readonly object _lock = new();

        public BackgroundTaskQueue(
            ILogger<BackgroundTaskQueue> logger,
            IOptions<BackgroundServiceOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _workItems.Count;
                }
            }
        }

        public int Capacity => _options.MaxQueueSize;

        public Task QueueBackgroundWorkItemAsync(BackgroundWorkItem workItem)
        {
            if (workItem?.WorkItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            lock (_lock)
            {
                if (_workItems.Count >= _options.MaxQueueSize)
                {
                    _logger.LogWarning("Background task queue is full. Current count: {Count}, Max: {Max}",
                        _workItems.Count, _options.MaxQueueSize);
                    throw new InvalidOperationException("Background task queue is full");
                }

                // Priority je negovaná pro correct ordering (vyšší priorita = lower value v PriorityQueue)
                _workItems.Enqueue(workItem, -workItem.Priority);

                _logger.LogDebug("Queued background work item {Id} ({Name}) with priority {Priority}",
                    workItem.Id, workItem.Name, workItem.Priority);
            }

            _signal.Release();
            return Task.CompletedTask;
        }

        public async Task<BackgroundWorkItem?> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);

            lock (_lock)
            {
                if (_workItems.TryDequeue(out var workItem, out _))
                {
                    // Check if item should be delayed
                    if (workItem.ScheduledFor.HasValue && workItem.ScheduledFor > DateTime.UtcNow)
                    {
                        // Put it back and wait
                        _workItems.Enqueue(workItem, -workItem.Priority);
                        return null;
                    }

                    _logger.LogDebug("Dequeued background work item {Id} ({Name})", workItem.Id, workItem.Name);
                    return workItem;
                }

                return null;
            }
        }

        public void Dispose()
        {
            _signal?.Dispose();
        }
    }
}
