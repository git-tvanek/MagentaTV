// MagentaTV/Services/Background/Core/BackgroundWorkItem.cs
// TOTO JE SPRÁVNÁ VERZE - kombinace správného namespace + kompletní funkcionality

using MagentaTV.Models.Background;

namespace MagentaTV.Services.Background.Core
{
    /// <summary>
    /// Představuje jednu background úlohu
    /// </summary>
    public class BackgroundWorkItem
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Func<IServiceProvider, CancellationToken, Task> WorkItem { get; set; } = null!;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public DateTime? ScheduledFor { get; set; }

        // Priority - vyšší číslo = vyšší priorita
        public int Priority { get; set; } = 0;

        // Retry logika
        public int MaxRetries { get; set; } = 3;
        public int RetryCount { get; set; } = 0;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
        public List<Exception> Exceptions { get; set; } = new();

        // Status tracking
        public BackgroundWorkItemStatus Status { get; set; } = BackgroundWorkItemStatus.Queued;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }

        // Computed properties
        public bool CanRetry => RetryCount < MaxRetries;
        public TimeSpan Duration => CompletedAt.HasValue && StartedAt.HasValue
            ? CompletedAt.Value - StartedAt.Value
            : TimeSpan.Zero;
        public bool IsCompleted => Status is BackgroundWorkItemStatus.Completed
            or BackgroundWorkItemStatus.Failed
            or BackgroundWorkItemStatus.Cancelled;
    }
}