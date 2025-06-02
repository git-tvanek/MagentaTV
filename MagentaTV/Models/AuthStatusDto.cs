namespace MagentaTV.Models;

public class AuthStatusDto
{
    public bool IsAuthenticated { get; set; }
    public string? Username { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
    public TimeSpan? TimeToExpiry { get; set; }
}