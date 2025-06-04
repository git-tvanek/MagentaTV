using MagentaTV.Models.Session;
using MagentaTV.Services.TokenStorage;

namespace MagentaTV.Services.Session
{
    /// <summary>
    /// Kompletní session data včetně tokenů
    /// </summary>
    public class SessionData
    {
        public string SessionId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public SessionStatus Status { get; set; } = SessionStatus.Active;

        // Token data
        public TokenData? Tokens { get; set; }

        // Session metadata
        public Dictionary<string, object> Metadata { get; set; } = new();

        // Computed properties
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt || Status == SessionStatus.Expired;
        public bool IsActive => Status == SessionStatus.Active && !IsExpired;
        public TimeSpan TimeToExpiry => ExpiresAt - DateTime.UtcNow;
        public bool HasValidTokens => Tokens?.IsValid == true;

        /// <summary>
        /// Aktualizuje last activity timestamp
        /// </summary>
        public void UpdateActivity()
        {
            LastActivity = DateTime.UtcNow;

            // Pokud byla session neaktivní, obnovíme ji
            if (Status == SessionStatus.Inactive)
            {
                Status = SessionStatus.Active;
            }
        }

        /// <summary>
        /// Označí session jako expirovanou
        /// </summary>
        public void Expire()
        {
            Status = SessionStatus.Expired;
            ExpiresAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Revokuje session
        /// </summary>
        public void Revoke()
        {
            Status = SessionStatus.Revoked;
        }

        /// <summary>
        /// Zkontroluje jestli je session neaktivní
        /// </summary>
        public bool IsInactive(TimeSpan inactivityTimeout)
        {
            return DateTime.UtcNow - LastActivity > inactivityTimeout;
        }
    }
}
