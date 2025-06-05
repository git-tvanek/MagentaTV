using MagentaTV.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace MagentaTV.Services.Background.Core
{
    public class BackgroundTaskQueue : IBackgroundTaskQueue, IDisposable
    {
        private readonly ConcurrentQueue<(BackgroundWorkItem WorkItem, int Priority)> _workItems = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly ILogger<BackgroundTaskQueue> _logger;
        private readonly BackgroundServiceOptions _options;
        private volatile int _count = 0;

        public BackgroundTaskQueue(
            ILogger<BackgroundTaskQueue> logger,
            IOptions<BackgroundServiceOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        public int Count => _count;
        public int Capacity => _options.MaxQueueSize;

        public Task QueueBackgroundWorkItemAsync(BackgroundWorkItem workItem, CancellationToken cancellationToken = default)
        {
            if (workItem?.WorkItem == null)
                throw new ArgumentNullException(nameof(workItem));

            if (_count >= _options.MaxQueueSize)
            {
                _logger.LogWarning("Background task queue is full. Current count: {Count}, Max: {Max}",
                    _count, _options.MaxQueueSize);
                throw new InvalidOperationException("Background task queue is full");
            }

            _workItems.Enqueue((workItem, workItem.Priority));
            Interlocked.Increment(ref _count);

            _signal.Release();

            _logger.LogDebug("Queued background work item {Id} ({Name}) with priority {Priority}",
                workItem.Id, workItem.Name, workItem.Priority);

            return Task.CompletedTask;
        }

        public async Task<BackgroundWorkItem?> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);

            // Find highest priority item
            var items = new List<(BackgroundWorkItem WorkItem, int Priority)>();

            while (_workItems.TryDequeue(out var item))
            {
                items.Add(item);
            }

            if (!items.Any())
                return null;

            // Sort by priority (higher number = higher priority) and then by creation time
            var selectedItem = items
                .OrderByDescending(x => x.Priority)
                .ThenBy(x => x.WorkItem.CreatedAt)
                .First();

            // Re-queue the rest
            foreach (var remainingItem in items.Where(x => x != selectedItem))
            {
                _workItems.Enqueue(remainingItem);
            }

            Interlocked.Decrement(ref _count);

            // Check if item should be delayed
            if (selectedItem.WorkItem.ScheduledFor.HasValue &&
                selectedItem.WorkItem.ScheduledFor > DateTime.UtcNow)
            {
                _workItems.Enqueue(selectedItem);
                Interlocked.Increment(ref _count);
                _signal.Release();
                return null; // Will try again later
            }

            _logger.LogDebug("Dequeued background work item {Id} ({Name})",
                selectedItem.WorkItem.Id, selectedItem.WorkItem.Name);

            return selectedItem.WorkItem;
        }

        public IEnumerable<BackgroundWorkItem> GetQueuedItems()
        {
            return _workItems.Select(x => x.WorkItem).ToList();
        }

        public void Dispose()
        {
            _signal?.Dispose();
        }
    }
}
