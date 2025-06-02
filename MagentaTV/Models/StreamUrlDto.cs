namespace MagentaTV.Models;

public class StreamUrlDto
{
    public int? ChannelId { get; set; }
    public long? ScheduleId { get; set; }
    public string StreamUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string Type { get; set; } = string.Empty; // LIVE, CATCHUP
}