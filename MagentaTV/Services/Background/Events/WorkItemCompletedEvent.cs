namespace MagentaTV.Services.Background.Events
{
    public class WorkItemCompletedEvent
    {
        public string WorkItemId { get; set; } = string.Empty;
        public string WorkItemName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
