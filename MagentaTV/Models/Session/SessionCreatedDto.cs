namespace MagentaTV.Models.Session;

public class SessionCreatedDto
{
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}