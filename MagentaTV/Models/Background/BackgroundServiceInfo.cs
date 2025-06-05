namespace MagentaTV.Models.Background
{
    public class BackgroundServiceInfo
    {
        public Type Type { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public BackgroundServiceStatus Status { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? StoppedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new();
    }
}
