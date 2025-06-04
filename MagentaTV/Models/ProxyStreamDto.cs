using System.ComponentModel.DataAnnotations;

namespace MagentaTV.Models;

public class ProxyStreamDto
{
    public string StreamId { get; set; } = string.Empty;
    public string ProxyUrl { get; set; } = string.Empty;
    public int ChannelId { get; set; }
    public string Quality { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public StreamStatus Status { get; set; }
    public StreamStats? Stats { get; set; }
}