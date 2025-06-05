// Application/EventHandlers/UserLoggedOutEventHandler.cs
using MagentaTV.Application.Events;
using MagentaTV.Services.Background;
using MagentaTV.Services.Background.Core;
using MagentaTV.Services.Session;
using MagentaTV.Services.TokenStorage;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace MagentaTV.Application.EventHandlers
{
    public class UserLoggedOutEventHandler : INotificationHandler<UserLoggedOutEvent>
    {
        private readonly IBackgroundServiceManager _backgroundManager;
        private readonly ISessionManager _sessionManager;
        private readonly ITokenStorage _tokenStorage;
        private readonly ILogger<UserLoggedOutEventHandler> _logger;

        public UserLoggedOutEventHandler(
            IBackgroundServiceManager backgroundManager,
            ISessionManager sessionManager,
            ITokenStorage tokenStorage,
            ILogger<UserLoggedOutEventHandler> logger)
        {
            _backgroundManager = backgroundManager;
            _sessionManager = sessionManager;
            _tokenStorage = tokenStorage;
            _logger = logger;
        }

        public async Task Handle(UserLoggedOutEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("User {Username} logged out. Reason: {Reason}, SessionId: {SessionId}",
                notification.Username, notification.Reason, notification.SessionId);

            try
            {
                // 1. Cleanup based on logout reason
                await HandleLogoutReasonAsync(notification);

                // 2. Queue background cleanup tasks
                await QueueCleanupTasksAsync(notification);

                // 3. Update user statistics
                await QueueUserStatisticsUpdateAsync(notification);

                // 4. Security audit logging
                await QueueSecurityAuditAsync(notification);

                _logger.LogInformation("Logout event processing completed for user {Username}", notification.Username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process logout event for user {Username}", notification.Username);

                // Queue retry work item for critical cleanup
                await QueueRetryCleanupAsync(notification, ex.Message);
            }
        }

        /// <summary>
        /// Zpracuje různé důvody odhlášení
        /// </summary>
        private async Task HandleLogoutReasonAsync(UserLoggedOutEvent notification)
        {
            switch (notification.Reason?.ToUpper())
            {
                case "VOLUNTARY":
                    _logger.LogDebug("Processing voluntary logout for {Username}", notification.Username);
                    // Normal logout - standard cleanup
                    break;

                case "EXPIRED":
                    _logger.LogInformation("Processing expired session logout for {Username}", notification.Username);
                    // Session expired - might need token refresh attempt
                    await HandleExpiredSessionAsync(notification);
                    break;

                case "REVOKED":
                    _logger.LogWarning("Processing revoked session logout for {Username}", notification.Username);
                    // Security revocation - aggressive cleanup
                    await HandleRevokedSessionAsync(notification);
                    break;

                case "CONCURRENT_LIMIT":
                    _logger.LogInformation("Processing concurrent limit logout for {Username}", notification.Username);
                    // Too many sessions - normal cleanup
                    break;

                default:
                    _logger.LogWarning("Unknown logout reason '{Reason}' for {Username}",
                        notification.Reason, notification.Username);
                    break;
            }
        }

        /// <summary>
        /// Zpracuje expirovanou session
        /// </summary>
        private async Task HandleExpiredSessionAsync(UserLoggedOutEvent notification)
        {
            // Zkontrolujeme jestli má uživatel ještě jiné aktivní sessions
            var userSessions = await _sessionManager.GetUserSessionsAsync(notification.Username);
            var activeSessions = userSessions.Where(s => s.IsActive && s.SessionId != notification.SessionId).ToList();

            if (!activeSessions.Any())
            {
                // Žádné aktivní sessions - můžeme vymazat tokeny
                _logger.LogDebug("No active sessions remaining for {Username}, scheduling token cleanup",
                    notification.Username);

                await QueueTokenCleanupAsync(notification.Username, "No active sessions");
            }
            else
            {
                _logger.LogDebug("User {Username} has {ActiveCount} remaining active sessions",
                    notification.Username, activeSessions.Count);
            }
        }

        /// <summary>
        /// Zpracuje revokovanou session (security incident)
        /// </summary>
        private async Task HandleRevokedSessionAsync(UserLoggedOutEvent notification)
        {
            // Agresivní cleanup při security incidentu
            _logger.LogWarning("Security revocation detected for {Username}, performing aggressive cleanup",
                notification.Username);

            // Okamžitě vymazat všechny tokeny
            await QueueTokenCleanupAsync(notification.Username, "Security revocation");

            // Zalogovat security incident
            await QueueSecurityIncidentAsync(notification);

            // Možná i revokovat všechny ostatní sessions uživatele
            await QueueUserSessionRevocationAsync(notification.Username, notification.SessionId);
        }

        /// <summary>
        /// Naplánuje background cleanup úlohy
        /// </summary>
        private async Task QueueCleanupTasksAsync(UserLoggedOutEvent notification)
        {
            // 1. Cache cleanup pro specifického uživatele
            var cacheCleanupWork = new BackgroundWorkItem
            {
                Name = "User Cache Cleanup",
                Type = "USER_CACHE_CLEANUP",
                Priority = 5,
                Parameters = new Dictionary<string, object>
                {
                    ["username"] = notification.Username,
                    ["sessionId"] = notification.SessionId,
                    ["reason"] = notification.Reason ?? "unknown"
                },
                WorkItem = async (provider, ct) =>
                {
                    var cache = provider.GetRequiredService<IMemoryCache>();

                    // Vymazat user-specific cache entries
                    var cacheKeys = new[]
                    {
                        $"user_channels_{notification.Username}",
                        $"user_epg_{notification.Username}",
                        $"user_streams_{notification.Username}"
                    };

                    foreach (var key in cacheKeys)
                    {
                        cache.Remove(key);
                    }
                }
            };

            await _backgroundManager.QueueWorkItemAsync(cacheCleanupWork);

            // 2. Temporary files cleanup
            var fileCleanupWork = new BackgroundWorkItem
            {
                Name = "User Temp Files Cleanup",
                Type = "USER_FILES_CLEANUP",
                Priority = 3,
                Parameters = new Dictionary<string, object>
                {
                    ["username"] = notification.Username,
                    ["sessionId"] = notification.SessionId
                },
                WorkItem = async (provider, ct) =>
                {
                    var logger = provider.GetRequiredService<ILogger<UserLoggedOutEventHandler>>();

                    try
                    {
                        // Cleanup temporary files (playlists, downloads, etc.)
                        var tempPath = Path.Combine("data", "temp", notification.Username);
                        if (Directory.Exists(tempPath))
                        {
                            Directory.Delete(tempPath, true);
                            logger.LogDebug("Cleaned temp files for user {Username}", notification.Username);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to cleanup temp files for user {Username}", notification.Username);
                    }
                }
            };

            await _backgroundManager.QueueWorkItemAsync(fileCleanupWork);
        }

        /// <summary>
        /// Naplánuje aktualizaci uživatelských statistik
        /// </summary>
        private async Task QueueUserStatisticsUpdateAsync(UserLoggedOutEvent notification)
        {
            var statsWork = new BackgroundWorkItem
            {
                Name = "Update User Logout Statistics",
                Type = "USER_LOGOUT_STATS",
                Priority = 1,
                Parameters = new Dictionary<string, object>
                {
                    ["username"] = notification.Username,
                    ["sessionId"] = notification.SessionId,
                    ["logoutTime"] = notification.Timestamp,
                    ["reason"] = notification.Reason ?? "unknown"
                },
                WorkItem = async (provider, ct) =>
                {
                    var logger = provider.GetRequiredService<ILogger<UserLoggedOutEventHandler>>();

                    try
                    {
                        // Zde by byla implementace aktualizace statistik
                        // Např. do databáze, external analytics, etc.

                        logger.LogDebug("Updated logout statistics for user {Username}", notification.Username);

                        // Simulace - v reálné implementaci by to bylo do DB
                        var statsFile = Path.Combine("data", "stats", $"logout_stats_{DateTime.UtcNow:yyyyMM}.json");
                        var statsDir = Path.GetDirectoryName(statsFile);

                        if (!Directory.Exists(statsDir))
                            Directory.CreateDirectory(statsDir!);

                        var logEntry = new
                        {
                            Username = notification.Username,
                            SessionId = notification.SessionId,
                            LogoutTime = notification.Timestamp,
                            Reason = notification.Reason,
                            ProcessedAt = DateTime.UtcNow
                        };

                        await File.AppendAllTextAsync(statsFile,
                            System.Text.Json.JsonSerializer.Serialize(logEntry) + Environment.NewLine, ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to update logout statistics for user {Username}", notification.Username);
                    }
                }
            };

            await _backgroundManager.QueueWorkItemAsync(statsWork);
        }

        /// <summary>
        /// Naplánuje security audit log
        /// </summary>
        private async Task QueueSecurityAuditAsync(UserLoggedOutEvent notification)
        {
            var auditWork = new BackgroundWorkItem
            {
                Name = "Security Audit Logging",
                Type = "SECURITY_AUDIT",
                Priority = 8, // High priority for security
                Parameters = new Dictionary<string, object>
                {
                    ["eventType"] = "USER_LOGOUT",
                    ["username"] = notification.Username,
                    ["sessionId"] = notification.SessionId,
                    ["timestamp"] = notification.Timestamp,
                    ["reason"] = notification.Reason ?? "unknown"
                },
                WorkItem = async (provider, ct) =>
                {
                    var logger = provider.GetRequiredService<ILogger<UserLoggedOutEventHandler>>();

                    try
                    {
                        // Security audit logging
                        var auditEntry = new
                        {
                            EventType = "USER_LOGOUT",
                            Username = notification.Username,
                            SessionId = notification.SessionId,
                            Timestamp = notification.Timestamp,
                            Reason = notification.Reason,
                            ProcessedAt = DateTime.UtcNow,
                            Severity = notification.Reason == "REVOKED" ? "HIGH" : "INFO"
                        };

                        var auditFile = Path.Combine("data", "audit", $"security_audit_{DateTime.UtcNow:yyyyMM}.log");
                        var auditDir = Path.GetDirectoryName(auditFile);

                        if (!Directory.Exists(auditDir))
                            Directory.CreateDirectory(auditDir!);

                        await File.AppendAllTextAsync(auditFile,
                            $"[{auditEntry.ProcessedAt:yyyy-MM-dd HH:mm:ss}] {auditEntry.Severity}: {System.Text.Json.JsonSerializer.Serialize(auditEntry)}{Environment.NewLine}", ct);

                        logger.LogInformation("Security audit logged for user logout: {Username}", notification.Username);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to write security audit log for user {Username}", notification.Username);
                    }
                }
            };

            await _backgroundManager.QueueWorkItemAsync(auditWork);
        }

        /// <summary>
        /// Naplánuje cleanup tokenů
        /// </summary>
        private async Task QueueTokenCleanupAsync(string username, string reason)
        {
            var tokenCleanupWork = new BackgroundWorkItem
            {
                Name = "Token Cleanup",
                Type = "TOKEN_CLEANUP",
                Priority = 7,
                Parameters = new Dictionary<string, object>
                {
                    ["username"] = username,
                    ["reason"] = reason
                },
                WorkItem = async (provider, ct) =>
                {
                    var tokenStorage = provider.GetRequiredService<ITokenStorage>();
                    var logger = provider.GetRequiredService<ILogger<UserLoggedOutEventHandler>>();

                    try
                    {
                        // Zkontrolujeme jestli jsou tokeny pro správného uživatele
                        var tokens = await tokenStorage.LoadTokensAsync();
                        if (tokens?.Username == username)
                        {
                            await tokenStorage.ClearTokensAsync();
                            logger.LogInformation("Cleared tokens for user {Username}, reason: {Reason}", username, reason);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to clear tokens for user {Username}", username);
                    }
                }
            };

            await _backgroundManager.QueueWorkItemAsync(tokenCleanupWork);
        }

        /// <summary>
        /// Naplánuje security incident processing
        /// </summary>
        private async Task QueueSecurityIncidentAsync(UserLoggedOutEvent notification)
        {
            var incidentWork = new BackgroundWorkItem
            {
                Name = "Security Incident Processing",
                Type = "SECURITY_INCIDENT",
                Priority = 10, // Highest priority
                Parameters = new Dictionary<string, object>
                {
                    ["username"] = notification.Username,
                    ["sessionId"] = notification.SessionId,
                    ["timestamp"] = notification.Timestamp,
                    ["reason"] = notification.Reason ?? "unknown"
                },
                WorkItem = async (provider, ct) =>
                {
                    var logger = provider.GetRequiredService<ILogger<UserLoggedOutEventHandler>>();

                    try
                    {
                        // Zde by byla implementace security incident processing
                        // - Email alerts
                        // - External security system notifications
                        // - Temporary account restrictions
                        // - Threat intelligence feeds

                        logger.LogWarning("Security incident processed for user {Username}: session revocation",
                            notification.Username);

                        // TODO: Implement actual security incident handling
                        // await securityService.ProcessIncidentAsync(incident);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to process security incident for user {Username}", notification.Username);
                    }
                }
            };

            await _backgroundManager.QueueWorkItemAsync(incidentWork);
        }

        /// <summary>
        /// Naplánuje revokaci všech sessions uživatele
        /// </summary>
        private async Task QueueUserSessionRevocationAsync(string username, string excludeSessionId)
        {
            var revocationWork = new BackgroundWorkItem
            {
                Name = "User Session Revocation",
                Type = "USER_SESSION_REVOCATION",
                Priority = 9,
                Parameters = new Dictionary<string, object>
                {
                    ["username"] = username,
                    ["excludeSessionId"] = excludeSessionId
                },
                WorkItem = async (provider, ct) =>
                {
                    var sessionManager = provider.GetRequiredService<ISessionManager>();
                    var logger = provider.GetRequiredService<ILogger<UserLoggedOutEventHandler>>();

                    try
                    {
                        var userSessions = await sessionManager.GetUserSessionsAsync(username);
                        var sessionsToRevoke = userSessions
                            .Where(s => s.IsActive && s.SessionId != excludeSessionId)
                            .ToList();

                        foreach (var session in sessionsToRevoke)
                        {
                            await sessionManager.RemoveSessionAsync(session.SessionId);
                            logger.LogInformation("Revoked session {SessionId} for user {Username} due to security incident",
                                session.SessionId, username);
                        }

                        logger.LogWarning("Revoked {Count} sessions for user {Username} due to security incident",
                            sessionsToRevoke.Count, username);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to revoke user sessions for {Username}", username);
                    }
                }
            };

            await _backgroundManager.QueueWorkItemAsync(revocationWork);
        }

        /// <summary>
        /// Naplánuje retry cleanup při selhání
        /// </summary>
        private async Task QueueRetryCleanupAsync(UserLoggedOutEvent notification, string errorMessage)
        {
            var retryWork = new BackgroundWorkItem
            {
                Name = "Retry Logout Cleanup",
                Type = "LOGOUT_CLEANUP_RETRY",
                Priority = 6,
                ScheduledFor = DateTime.UtcNow.AddMinutes(5), // Retry za 5 minut
                Parameters = new Dictionary<string, object>
                {
                    ["originalEvent"] = notification,
                    ["errorMessage"] = errorMessage,
                    ["retryAttempt"] = 1
                },
                WorkItem = async (provider, ct) =>
                {
                    var logger = provider.GetRequiredService<ILogger<UserLoggedOutEventHandler>>();
                    var handler = provider.GetRequiredService<UserLoggedOutEventHandler>();

                    try
                    {
                        logger.LogInformation("Retrying logout cleanup for user {Username}", notification.Username);
                        await handler.Handle(notification, ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Retry logout cleanup failed for user {Username}", notification.Username);
                        // V produkci by zde mohla být eskalace na operations team
                    }
                }
            };

            await _backgroundManager.QueueWorkItemAsync(retryWork);
        }
    }
}