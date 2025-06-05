namespace MagentaTV.Services.Background.Core
{
    public interface IBackgroundTaskQueue
    {
        Task QueueBackgroundWorkItemAsync(BackgroundWorkItem workItem, CancellationToken cancellationToken = default);
        Task<BackgroundWorkItem?> DequeueAsync(CancellationToken cancellationToken);
        int Count { get; }
        int Capacity { get; }
        IEnumerable<BackgroundWorkItem> GetQueuedItems();
    }
}
