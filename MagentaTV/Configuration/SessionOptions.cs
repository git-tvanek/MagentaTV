using System.ComponentModel.DataAnnotations;

namespace MagentaTV.Configuration
{
    public class SessionOptions
    {
        public const string SectionName = "Session";

        /// <summary>
        /// Výchozí doba platnosti session v hodinách
        /// </summary>
        [Range(1, 168)] // 1 hour to 1 week
        public int DefaultDurationHours { get; set; } = 8;

        /// <summary>
        /// Maximální doba platnosti session v hodinách
        /// </summary>
        [Range(1, 720)] // 1 hour to 30 days
        public int MaxDurationHours { get; set; } = 72;

        /// <summary>
        /// Doba platnosti pro "Remember Me" sessions v hodinách
        /// </summary>
        [Range(24, 8760)] // 1 day to 1 year
        public int RememberMeDurationHours { get; set; } = 720; // 30 days

        /// <summary>
        /// Interval pro cleanup expirovaných sessions (minuty)
        /// </summary>
        [Range(5, 1440)] // 5 minutes to 24 hours
        public int CleanupIntervalMinutes { get; set; } = 60;

        /// <summary>
        /// Doba neaktivity po které se session označí jako inactive (minuty)
        /// </summary>
        [Range(5, 480)] // 5 minutes to 8 hours
        public int InactivityTimeoutMinutes { get; set; } = 30;

        /// <summary>
        /// Povolit současné přihlášení ze stejného IP
        /// </summary>
        public bool AllowConcurrentSessions { get; set; } = true;

        /// <summary>
        /// Maximální počet současných sessions pro jednoho uživatele
        /// </summary>
        [Range(1, 10)]
        public int MaxConcurrentSessions { get; set; } = 3;

        /// <summary>
        /// Automaticky obnovovat tokeny při aktivitě
        /// </summary>
        public bool AutoRefreshTokens { get; set; } = true;

        /// <summary>
        /// Logovat session aktivity
        /// </summary>
        public bool LogSessionActivity { get; set; } = true;

        /// <summary>
        /// Encryption key pro session tokeny
        /// </summary>
        public string EncryptionKey { get; set; } = string.Empty;

        /// <summary>
        /// Secure cookie settings
        /// </summary>
        public bool SecureCookies { get; set; } = true;

        /// <summary>
        /// SameSite cookie setting
        /// </summary>
        public string SameSiteMode { get; set; } = "Strict";


        public void Validate()
        {
            if (DefaultDurationHours > MaxDurationHours)
                throw new ArgumentException("DefaultDurationHours cannot be greater than MaxDurationHours");

            if (MaxConcurrentSessions < 1)
                throw new ArgumentException("MaxConcurrentSessions must be at least 1");

            // Enhanced encryption key validation
            var encryptionKey = GetEncryptionKey();
            if (string.IsNullOrWhiteSpace(encryptionKey) || encryptionKey.Length < 32)
                throw new ArgumentException("EncryptionKey must be at least 32 characters long. Set via environment variable SESSION_ENCRYPTION_KEY in production.");
        }

        /// <summary>
        /// Gets encryption key from environment variable in production or config in development
        /// </summary>
        public string GetEncryptionKey()
        {
            // In production, prefer environment variable
            var envKey = Environment.GetEnvironmentVariable("SESSION_ENCRYPTION_KEY");
            if (!string.IsNullOrEmpty(envKey))
                return envKey;

            // Fallback to config (development only)
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                return EncryptionKey;

            throw new InvalidOperationException("SESSION_ENCRYPTION_KEY environment variable must be set in production");
        }
    }
}