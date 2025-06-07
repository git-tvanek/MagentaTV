using MagentaTV.Models.Session;
using MagentaTV.Services.TokenStorage;

namespace MagentaTV.Services.Session
{
    /// <summary>
    /// Interface pro správu uživatelských sessions
    /// </summary>
    public interface ISessionManager
    {
        /// <summary>
        /// Vytvoří novou session pro uživatele
        /// </summary>
        Task<string> CreateSessionAsync(CreateSessionRequest request, string ipAddress, string userAgent);

        /// <summary>
        /// Získá session podle ID
        /// </summary>
        Task<SessionData?> GetSessionAsync(string sessionId);

        /// <summary>
        /// Získá všechny aktivní sessions pro uživatele
        /// </summary>
        Task<List<SessionData>> GetUserSessionsAsync(string username);

        /// <summary>
        /// Aktualizuje aktivitu session
        /// </summary>
        Task UpdateSessionActivityAsync(string sessionId);

        /// <summary>
        /// Odstraní session (logout)
        /// </summary>
        Task RemoveSessionAsync(string sessionId);

        /// <summary>
        /// Odstraní všechny sessions uživatele
        /// </summary>
        Task RemoveUserSessionsAsync(string username);

        /// <summary>
        /// Ověří platnost session
        /// </summary>
        Task<bool> ValidateSessionAsync(string sessionId);

        /// <summary>
        /// Získá session info pro API response
        /// </summary>
        Task<SessionInfoDto?> GetSessionInfoAsync(string sessionId);

        /// <summary>
        /// Regenerates the session identifier to mitigate fixation attacks.
        /// </summary>
        /// <param name="sessionId">Current session identifier.</param>
        /// <returns>New regenerated identifier.</returns>
        Task<string> RegenerateSessionIdAsync(string sessionId);

        /// <summary>
        /// Vyčistí expirované sessions
        /// </summary>
        Task CleanupExpiredSessionsAsync();

        /// <summary>
        /// Obnoví tokeny pro session
        /// </summary>
        Task RefreshSessionTokensAsync(string sessionId, TokenData newTokens);

        /// <summary>
        /// Získá statistiky sessions
        /// </summary>
        Task<SessionStatistics> GetStatisticsAsync();
    }
}
