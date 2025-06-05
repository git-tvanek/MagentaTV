namespace MagentaTV.Services.Background.Core
{
    public class ServiceHealth
    {
        public string ServiceName { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime LastHeartbeat { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }
}
