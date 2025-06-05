using MagentaTV.Models.Background;

namespace MagentaTV.Services.Background
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
        public int Priority { get; set; } = 0; // Vyšší číslo = vyšší priorita
        public int MaxRetries { get; set; } = 3;
        public int RetryCount { get; set; } = 0;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
        public List<Exception> Exceptions { get; set; } = new();
        public BackgroundWorkItemStatus Status { get; set; } = BackgroundWorkItemStatus.Queued;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
