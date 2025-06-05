namespace MagentaTV.Services.Background.Core
{
    public class BackgroundWorkItem
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Func<IServiceProvider, CancellationToken, Task> WorkItem { get; set; } = null!;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public DateTime? ScheduledFor { get; set; }
        public int Priority { get; set; } = 0;
        public WorkItemStatus Status { get; set; } = WorkItemStatus.Queued;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<Exception> Exceptions { get; set; } = new();
    }
}
