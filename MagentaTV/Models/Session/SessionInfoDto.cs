namespace MagentaTV.Models.Session
{
    public class SessionInfoDto
    {
        public string SessionId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsExpired { get; set; }
        public TimeSpan TimeToExpiry { get; set; }
        public SessionStatus Status { get; set; }
        public bool HasValidTokens { get; set; }
        public DateTime? TokensExpiresAt { get; set; }
    }
}
