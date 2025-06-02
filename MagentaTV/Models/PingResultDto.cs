namespace MagentaTV.Models;

public class PingResultDto
{
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasValidTokens { get; set; }
    public string? Username { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
}