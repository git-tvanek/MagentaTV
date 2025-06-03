namespace MagentaTV.Models
{
    public class StreamingHealthDto
    {
        public bool IsHealthy { get; set; }
        public string FFmpegVersion { get; set; } = string.Empty;
        public bool HardwareAccelerationAvailable { get; set; }
        public int ActiveStreamsCount { get; set; }
        public int ActiveRecordingsCount { get; set; }
        public ResourceUsage ResourceUsage { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
