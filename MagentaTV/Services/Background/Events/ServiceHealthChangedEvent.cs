namespace MagentaTV.Services.Background.Events
{
    public class ServiceHealthChangedEvent
    {
        public string ServiceName { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
