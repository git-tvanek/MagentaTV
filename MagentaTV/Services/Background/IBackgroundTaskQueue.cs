namespace MagentaTV.Services.Background
{
    /// <summary>
    /// Interface pro frontu background úloh
    /// </summary>
    public interface IBackgroundTaskQueue
    {
        /// <summary>
        /// Přidá úlohu do fronty
        /// </summary>
        Task QueueBackgroundWorkItemAsync(BackgroundWorkItem workItem);

        /// <summary>
        /// Vybere úlohu z fronty pro zpracování
        /// </summary>
        Task<BackgroundWorkItem?> DequeueAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Počet úloh ve frontě
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Maximální kapacita fronty
        /// </summary>
        int Capacity { get; }
    }
}
