namespace MagentaTV.Models
{
    public class ActiveStreamDto
    {
        public string StreamId { get; set; } = string.Empty;
        public int ChannelId { get; set; }
        public string ChannelName { get; set; } = string.Empty;
        public string Quality { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public TimeSpan Uptime => DateTime.UtcNow - StartedAt;
        public StreamStatus Status { get; set; }
        public int ViewerCount { get; set; }
        public StreamStats Stats { get; set; } = new();
    }
}
