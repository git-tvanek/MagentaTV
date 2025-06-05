namespace MagentaTV.Services.Background.Events
{
    public class WorkItemStartedEvent
    {
        public string WorkItemId { get; set; } = string.Empty;
        public string WorkItemName { get; set; } = string.Empty;
        public string WorkItemType { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
    }
}
