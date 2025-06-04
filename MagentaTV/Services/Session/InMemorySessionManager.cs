// MagentaTV/Services/Session/InMemorySessionManager.cs (Updated)
using Microsoft.Extensions.Options;
using MagentaTV.Configuration;
using MagentaTV.Models.Session;
using MagentaTV.Services.TokenStorage;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace MagentaTV.Services.Session;

/// <summary>
/// Updated SessionManager s plnou TokenStorage integrací
/// </summary>
public class InMemorySessionManager : ISessionManager, IDisposable
{
    private readonly ConcurrentDictionary<string, SessionData> _sessions = new();
    private readonly ILogger<InMemorySessionManager> _logger;
    private readonly Configuration.SessionOptions _options;
    private readonly ITokenStorage _tokenStorage;
    private readonly Timer _cleanupTimer;

    public InMemorySessionManager(
        ILogger<InMemorySessionManager> logger,
        IOptions<Configuration.SessionOptions> options,
        ITokenStorage tokenStorage)
    {
        _logger = logger;
        _options = options.Value;
        _tokenStorage = tokenStorage;

        // Spustíme periodický cleanup
        _cleanupTimer = new Timer(
            async _ => await CleanupExpiredSessionsAsync(),
            null,
            TimeSpan.FromMinutes(_options.CleanupIntervalMinutes),
            TimeSpan.FromMinutes(_options.CleanupIntervalMinutes));

        _logger.LogInformation("InMemorySessionManager initialized with TokenStorage integration");
    }

    public async Task<string> CreateSessionAsync(CreateSessionRequest request, string ipAddress, string userAgent)
    {
        try
        {
            // POZOR: Nebudeme zde volat login - to už udělal controller
            // SessionManager pouze vytvoří session záznam

            // Zkontrolujeme limit současných sessions
            await EnforceSessionLimitsAsync(request.Username);

            // Vytvoříme novou session
            var sessionId = GenerateSessionId();
            var duration = GetSessionDuration(request);

            var sessionData = new SessionData
            {
                SessionId = sessionId,
                Username = request.Username,
                CreatedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(duration),
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Status = SessionStatus.Active
            };

            // Přidáme metadata
            sessionData.Metadata["RememberMe"] = request.RememberMe;
            sessionData.Metadata["RequestedDuration"] = duration;
            sessionData.Metadata["CreatedVia"] = "API";

            // Zkusíme načíst existující tokeny z TokenStorage
            try
            {
                var tokens = await _tokenStorage.LoadTokensAsync();
                if (tokens?.IsValid == true && tokens.Username == request.Username)
                {
                    sessionData.Tokens = tokens;
                    _logger.LogDebug("Loaded existing tokens for session {SessionId}", sessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load tokens for new session {SessionId}", sessionId);
            }

            _sessions[sessionId] = sessionData;

            _logger.LogInformation("Session created for user {Username}: {SessionId}, expires: {ExpiresAt}",
                request.Username, sessionId, sessionData.ExpiresAt);

            return sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create session for user {Username}", request.Username);
            throw;
        }
    }

    public async Task<SessionData?> GetSessionAsync(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
            return null;

        if (_sessions.TryGetValue(sessionId, out var session))
        {
            // Zkontrolujeme expiraci
            if (session.IsExpired)
            {
                session.Status = SessionStatus.Expired;
                return session;
            }

            // Zkontrolujeme neaktivitu
            if (session.IsInactive(TimeSpan.FromMinutes(_options.InactivityTimeoutMinutes)))
            {
                session.Status = SessionStatus.Inactive;
                _logger.LogDebug("Session {SessionId} marked as inactive due to inactivity", sessionId);
            }

            return session;
        }

        return null;
    }

    public async Task<List<SessionData>> GetUserSessionsAsync(string username)
    {
        var userSessions = _sessions.Values
            .Where(s => s.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.LastActivity)
            .ToList();

        return userSessions;
    }

    public async Task UpdateSessionActivityAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.UpdateActivity();

            // Pokud je povolený auto-refresh tokenů, zkontrolujeme je
            if (_options.AutoRefreshTokens && session.Tokens != null)
            {
                await CheckAndRefreshTokensAsync(session);
            }

            if (_options.LogSessionActivity)
            {
                _logger.LogDebug("Session activity updated: {SessionId} for user {Username}",
                    sessionId, session.Username);
            }
        }
    }

    public async Task RemoveSessionAsync(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            // Pokud byla tato session jediná pro uživatele, vymažeme i tokeny
            var remainingSessions = await GetUserSessionsAsync(session.Username);
            var activeSessions = remainingSessions.Where(s => s.IsActive && s.SessionId != sessionId).ToList();

            if (!activeSessions.Any())
            {
                try
                {
                    await _tokenStorage.ClearTokensAsync();
                    _logger.LogDebug("Cleared tokens for user {Username} - no more active sessions", session.Username);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clear tokens for user {Username}", session.Username);
                }
            }

            _logger.LogInformation("Session removed: {SessionId} for user {Username}",
                sessionId, session.Username);
        }
    }

    public async Task RemoveUserSessionsAsync(string username)
    {
        var userSessions = await GetUserSessionsAsync(username);
        var removedCount = 0;

        foreach (var session in userSessions)
        {
            if (_sessions.TryRemove(session.SessionId, out _))
            {
                removedCount++;
            }
        }

        // Vymažeme i tokeny
        try
        {
            await _tokenStorage.ClearTokensAsync();
            _logger.LogDebug("Cleared tokens for user {Username}", username);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear tokens for user {Username}", username);
        }

        _logger.LogInformation("Removed {Count} sessions for user {Username}", removedCount, username);
    }

    public async Task<bool> ValidateSessionAsync(string sessionId)
    {
        var session = await GetSessionAsync(sessionId);
        if (session?.IsActive != true)
            return false;

        // Ověříme i platnost tokenů
        if (session.Tokens != null && !session.Tokens.IsValid)
        {
            _logger.LogDebug("Session {SessionId} has invalid tokens", sessionId);
            return false;
        }

        return true;
    }

    public async Task<SessionInfoDto?> GetSessionInfoAsync(string sessionId)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null)
            return null;

        return new SessionInfoDto
        {
            SessionId = session.SessionId,
            Username = session.Username,
            CreatedAt = session.CreatedAt,
            LastActivity = session.LastActivity,
            ExpiresAt = session.ExpiresAt,
            IsExpired = session.IsExpired,
            TimeToExpiry = session.TimeToExpiry,
            Status = session.Status,
            HasValidTokens = session.HasValidTokens,
            TokensExpiresAt = session.Tokens?.ExpiresAt
        };
    }

    public async Task CleanupExpiredSessionsAsync()
    {
        var expiredCount = 0;
        var inactiveCount = 0;
        var toRemove = new List<string>();

        foreach (var kvp in _sessions)
        {
            var session = kvp.Value;

            if (session.IsExpired)
            {
                toRemove.Add(kvp.Key);
                expiredCount++;
            }
            else if (session.IsInactive(TimeSpan.FromMinutes(_options.InactivityTimeoutMinutes)))
            {
                if (session.Status == SessionStatus.Active)
                {
                    session.Status = SessionStatus.Inactive;
                    inactiveCount++;
                }
            }
        }

        // Odstraníme expirované sessions
        foreach (var sessionId in toRemove)
        {
            await RemoveSessionAsync(sessionId);
        }

        if (expiredCount > 0 || inactiveCount > 0)
        {
            _logger.LogInformation("Session cleanup completed. Removed: {ExpiredCount}, marked inactive: {InactiveCount}",
                expiredCount, inactiveCount);
        }
    }

    public async Task RefreshSessionTokensAsync(string sessionId, TokenData newTokens)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.Tokens = newTokens;
            session.UpdateActivity();

            _logger.LogDebug("Refreshed tokens for session {SessionId}, expires: {ExpiresAt}",
                sessionId, newTokens.ExpiresAt);
        }
    }

    public async Task<SessionStatistics> GetStatisticsAsync()
    {
        var allSessions = _sessions.Values.ToList();

        var stats = new SessionStatistics
        {
            TotalActiveSessions = allSessions.Count(s => s.Status == SessionStatus.Active),
            TotalExpiredSessions = allSessions.Count(s => s.Status == SessionStatus.Expired),
            TotalInactiveSessions = allSessions.Count(s => s.Status == SessionStatus.Inactive),
            TotalRevokedSessions = allSessions.Count(s => s.Status == SessionStatus.Revoked),
            UniqueUsers = allSessions.Select(s => s.Username).Distinct().Count(),
            LastCleanup = DateTime.UtcNow
        };

        // Průměrná doba trvání sessions
        var completedSessions = allSessions.Where(s => s.Status != SessionStatus.Active).ToList();
        if (completedSessions.Any())
        {
            var durations = completedSessions.Select(s => s.LastActivity - s.CreatedAt);
            stats.AverageSessionDuration = TimeSpan.FromTicks((long)durations.Average(d => d.Ticks));
        }

        // Sessions podle statusu
        stats.SessionsByStatus = allSessions
            .GroupBy(s => s.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        // Sessions podle uživatele
        stats.SessionsByUser = allSessions
            .GroupBy(s => s.Username)
            .ToDictionary(g => g.Key, g => g.Count());

        return stats;
    }

    #region Private Methods

    private async Task EnforceSessionLimitsAsync(string username)
    {
        if (!_options.AllowConcurrentSessions)
        {
            // Odstraníme všechny existující sessions
            await RemoveUserSessionsAsync(username);
        }
        else
        {
            var userSessions = await GetUserSessionsAsync(username);
            var activeSessions = userSessions.Where(s => s.IsActive).ToList();

            if (activeSessions.Count >= _options.MaxConcurrentSessions)
            {
                // Odstraníme nejstarší sessions
                var sessionsToRemove = activeSessions
                    .OrderBy(s => s.LastActivity)
                    .Take(activeSessions.Count - _options.MaxConcurrentSessions + 1);

                foreach (var session in sessionsToRemove)
                {
                    await RemoveSessionAsync(session.SessionId);
                }

                _logger.LogInformation("Enforced session limit for user {Username}, removed {Count} old sessions",
                    username, sessionsToRemove.Count());
            }
        }
    }

    private int GetSessionDuration(CreateSessionRequest request)
    {
        if (request.RememberMe)
        {
            return _options.RememberMeDurationHours;
        }

        if (request.SessionDurationHours.HasValue)
        {
            return Math.Min(request.SessionDurationHours.Value, _options.MaxDurationHours);
        }

        return _options.DefaultDurationHours;
    }

    private string GenerateSessionId()
    {
        using var rng = RandomNumberGenerator.Create();
        var tokenBytes = new byte[32];
        rng.GetBytes(tokenBytes);
        return Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    /// <summary>
    /// Zkontroluje a případně obnoví tokeny pro session
    /// </summary>
    private async Task CheckAndRefreshTokensAsync(SessionData session)
    {
        if (session.Tokens == null)
            return;

        // Pokud jsou tokeny blízko expiraci, pokusíme se je obnovit
        if (session.Tokens.IsNearExpiry)
        {
            try
            {
                // Načteme nejnovější tokeny z storage (možná byly mezitím obnoveny)
                var latestTokens = await _tokenStorage.LoadTokensAsync();
                if (latestTokens?.IsValid == true && latestTokens.ExpiresAt > session.Tokens.ExpiresAt)
                {
                    session.Tokens = latestTokens;
                    _logger.LogDebug("Updated session {SessionId} with newer tokens from storage", session.SessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check for updated tokens for session {SessionId}", session.SessionId);
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }

    #endregion
}