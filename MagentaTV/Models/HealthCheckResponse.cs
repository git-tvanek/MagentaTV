namespace MagentaTV.Models
{
    public class HealthCheckResponse
    {
        public string Status { get; set; } = string.Empty;
        public Dictionary<string, object> Checks { get; set; } = new();
        public TimeSpan Duration { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}