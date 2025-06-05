using MagentaTV.Application.Events;
using MagentaTV.Models;
using MagentaTV.Services;
using MagentaTV.Services.Background;
using MagentaTV.Services.Background.Core;
using MagentaTV.Services.Background.Services;
using MagentaTV.Services.Session;
using MediatR;

namespace MagentaTV.Application.EventHandlers
{
    public class UserLoggedInEventHandler : INotificationHandler<UserLoggedInEvent>
    {
        private readonly IBackgroundServiceManager _backgroundManager;
        private readonly ILogger<UserLoggedInEventHandler> _logger;

        public UserLoggedInEventHandler(
            IBackgroundServiceManager backgroundManager,
            ILogger<UserLoggedInEventHandler> logger)
        {
            _backgroundManager = backgroundManager;
            _logger = logger;
        }

        public async Task Handle(UserLoggedInEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("User {Username} logged in, triggering post-login tasks", notification.Username);

            try
            {
                // 1. Okamžitý cache warming s vysokou prioritou
                await QueueImmediateCacheWarmingAsync(notification);

                // 2. Trigger manuální cache warming ve stávajícím service
                await TriggerExistingCacheWarmingServiceAsync();

                // 3. Pre-load user specific data
                await QueueUserDataPreloadAsync(notification);

                // 4. Update user statistics
                await QueueUserStatisticsUpdateAsync(notification);

                _logger.LogInformation("All post-login tasks queued successfully for user {Username}", notification.Username);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue post-login tasks for user {Username}", notification.Username);
                // Nebudeme házet exception - login už proběhl úspěšně
            }
        }

        /// <summary>
        /// Naplánuje okamžité cache warming s nejvyšší prioritou
        /// </summary>
        private async Task QueueImmediateCacheWarmingAsync(UserLoggedInEvent notification)
        {
            var immediateCacheWork = new BackgroundWorkItem
            {
                Name = "Immediate Post-Login Cache Warming",
                Type = "IMMEDIATE_CACHE_WARM",
                Priority = 10, // Nejvyšší priorita
                Parameters = new()
                {
                    ["username"] = notification.Username,
                    ["trigger"] = "user_login",
                    ["session_id"] = notification.SessionId
                },
                WorkItem = async (provider, ct) =>
                {
                    var logger = provider.GetRequiredService<ILogger<UserLoggedInEventHandler>>();
                    var magenta = provider.GetRequiredService<IMagenta>();

                    try
                    {
                        logger.LogInformation("Starting immediate cache warming for user {Username}", notification.Username);

                        // Načteme kanály
                        var channels = await magenta.GetChannelsAsync();
                        logger.LogInformation("Immediate cache warming completed with {ChannelCount} channels for user {Username}",
                            channels.Count, notification.Username);

                        // Pokusíme se načíst i základní EPG pro populární kanály
                        await WarmPopularChannelsEpgAsync(magenta, channels.Take(5).ToList(), logger);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Immediate cache warming failed for user {Username}", notification.Username);
                        // Není fatální chyba
                    }
                }
            };

            await _backgroundManager.QueueWorkItemAsync(immediateCacheWork);
        }

        /// <summary>
        /// Triggerne cache warming přes BackgroundServiceManager
        /// </summary>
        private async Task TriggerExistingCacheWarmingServiceAsync()
        {
            try
            {
                // Používáme BackgroundServiceManager pro trigger
                await _backgroundManager.TriggerCacheWarmingIfPossibleAsync();
                _logger.LogDebug("Triggered cache warming via BackgroundServiceManager");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to trigger cache warming via BackgroundServiceManager");
            }
        }

        /// <summary>
        /// Naplánuje pre-loading user specific dat
        /// </summary>
        private async Task QueueUserDataPreloadAsync(UserLoggedInEvent notification)
        {
            var preloadWork = new BackgroundWorkItem
            {
                Name = "User Data Preload",
                Type = "USER_PRELOAD",
                Priority = 8,
                Parameters = new()
                {
                    ["username"] = notification.Username,
                    ["session_id"] = notification.SessionId,
                    ["ip_address"] = notification.IpAddress
                },
                WorkItem = async (provider, ct) =>
                {
                    var logger = provider.GetRequiredService<ILogger<UserLoggedInEventHandler>>();
                    var magenta = provider.GetRequiredService<IMagenta>();

                    try
                    {
                        // Pre-load some popular EPG data
                        var channels = await magenta.GetChannelsAsync();
                        var popularChannels = channels.Take(10).ToList();

                        foreach (var channel in popularChannels)
                        {
                            try
                            {
                                await magenta.GetEpgAsync(channel.ChannelId, DateTime.Now, DateTime.Now.AddHours(6));
                                logger.LogDebug("Preloaded EPG for channel {ChannelId} ({ChannelName})",
                                    channel.ChannelId, channel.Name);
                            }
                            catch (Exception ex)
                            {
                                logger.LogDebug(ex, "Failed to preload EPG for channel {ChannelId}", channel.ChannelId);
                            }

                            // Prevent overwhelming the API
                            await Task.Delay(100, ct);
                        }

                        logger.LogInformation("User data preload completed for {Username}", notification.Username);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to preload user data for {Username}", notification.Username);
                    }
                }
            };

            await _backgroundManager.QueueWorkItemAsync(preloadWork);
        }

        /// <summary>
        /// Naplánuje aktualizaci user statistics
        /// </summary>
        private async Task QueueUserStatisticsUpdateAsync(UserLoggedInEvent notification)
        {
            var statsWork = new BackgroundWorkItem
            {
                Name = "Update User Login Statistics",
                Type = "USER_LOGIN_STATS",
                Priority = 3,
                Parameters = new()
                {
                    ["username"] = notification.Username,
                    ["login_time"] = notification.Timestamp,
                    ["ip_address"] = notification.IpAddress,
                    ["session_id"] = notification.SessionId
                },
                WorkItem = async (provider, ct) =>
                {
                    var logger = provider.GetRequiredService<ILogger<UserLoggedInEventHandler>>();
                    var sessionManager = provider.GetRequiredService<ISessionManager>();

                    try
                    {
                        var stats = await sessionManager.GetStatisticsAsync();
                        logger.LogInformation("Login stats updated for {Username}. Active sessions: {Count}, Total unique users: {UniqueUsers}",
                            notification.Username, stats.TotalActiveSessions, stats.UniqueUsers);

                        // Zde můžete přidat další statistiky - zápis do DB, external analytics, etc.
                        await LogLoginStatisticsAsync(notification, stats, logger);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to update login stats for user {Username}", notification.Username);
                    }
                }
            };

            await _backgroundManager.QueueWorkItemAsync(statsWork);
        }

        /// <summary>
        /// Pre-warm EPG pro populární kanály
        /// </summary>
        private async Task WarmPopularChannelsEpgAsync(IMagenta magenta, List<ChannelDto> channels, ILogger logger)
        {
            foreach (var channel in channels)
            {
                try
                {
                    await magenta.GetEpgAsync(channel.ChannelId, DateTime.Now, DateTime.Now.AddHours(3));
                    logger.LogDebug("Warmed EPG cache for popular channel {ChannelId}", channel.ChannelId);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to warm EPG for channel {ChannelId}", channel.ChannelId);
                }

                await Task.Delay(50); // Malé zpoždění mezi požadavky
            }
        }

        /// <summary>
        /// Uloží login statistiky
        /// </summary>
        private async Task LogLoginStatisticsAsync(UserLoggedInEvent notification, SessionStatistics stats, ILogger logger)
        {
            try
            {
                var loginEntry = new
                {
                    Username = notification.Username,
                    SessionId = notification.SessionId,
                    LoginTime = notification.Timestamp,
                    IpAddress = notification.IpAddress,
                    ActiveSessions = stats.TotalActiveSessions,
                    UniqueUsers = stats.UniqueUsers,
                    ProcessedAt = DateTime.UtcNow
                };

                var statsFile = Path.Combine("data", "stats", $"login_stats_{DateTime.UtcNow:yyyyMM}.json");
                var statsDir = Path.GetDirectoryName(statsFile);

                if (!Directory.Exists(statsDir))
                    Directory.CreateDirectory(statsDir!);

                await File.AppendAllTextAsync(statsFile,
                    System.Text.Json.JsonSerializer.Serialize(loginEntry) + Environment.NewLine);

                logger.LogDebug("Login statistics logged for user {Username}", notification.Username);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to log login statistics for user {Username}", notification.Username);
            }
        }
    }
}