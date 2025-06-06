using MagentaTV.Services.Background.Core;
using MagentaTV.Services.Background.Events;
using MagentaTV.Services.TokenStorage;
using MagentaTV.Services.Channels;
using Microsoft.Extensions.Caching.Memory;

namespace MagentaTV.Services.Background.Services
{
    /// <summary>
    /// Periodically preloads channel data into the in-memory cache so that
    /// the first user requests are served faster. The service runs in the
    /// background and intelligently decides when to perform the warming based
    /// on token availability and previous success.
    /// </summary>
    public class CacheWarmingService : BaseBackgroundService, ICacheWarmingService
    {
        private bool _hasWarmedAfterLogin = false;
        private DateTime _lastSuccessfulWarm = DateTime.MinValue;

        public CacheWarmingService(
            ILogger<CacheWarmingService> logger,
            IServiceProvider serviceProvider,
            IEventBus eventBus)
            : base(logger, serviceProvider, eventBus, "CacheWarmingService")
        {
        }

        protected override async Task ExecuteServiceAsync(CancellationToken stoppingToken)
        {
            Logger.LogInformation("CacheWarmingService started with intelligent token checking");

            while (!stoppingToken.IsCancellationRequested)
            {
                await ExecuteWithEventsAsync("CacheWarming", async () =>
                {
                    using var scope = CreateScope();
                    var channelService = scope.ServiceProvider.GetRequiredService<IChannelService>();
                    var tokenStorage = scope.ServiceProvider.GetRequiredService<ITokenStorage>();
                    var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

                    // Zkontroluj dostupnost tokenů
                    var hasValidTokens = await tokenStorage.HasValidTokensAsync();
                    if (!hasValidTokens)
                    {
                        Logger.LogDebug("Skipping cache warming - no valid tokens available");
                        SetMetric("skipped_no_tokens", GetMetric<long>("skipped_no_tokens") + 1);
                        SetMetric("last_check_result", "no_tokens");
                        return true; // Není chyba, jen počkáme na přihlášení
                    }

                    // Zkontroluj jestli už není cache naplněná
                    if (IsCacheAlreadyWarmed(cache))
                    {
                        Logger.LogDebug("Cache already warmed, skipping automatic warming");
                        SetMetric("skipped_already_warmed", GetMetric<long>("skipped_already_warmed") + 1);
                        SetMetric("last_check_result", "already_warmed");
                        return true;
                    }

                    try
                    {
                        var channels = await channelService.GetChannelsAsync();
                        Logger.LogInformation("Cache warmed with {ChannelCount} channels", channels.Count);

                        // Update metrics
                        SetMetric("warmed_channels", channels.Count);
                        SetMetric("last_warm_time", DateTime.UtcNow);
                        SetMetric("successful_warms", GetMetric<long>("successful_warms") + 1);
                        SetMetric("last_check_result", "success");

                        _lastSuccessfulWarm = DateTime.UtcNow;

                        // Pokud tohle je první úspěšné warming po spuštění, označíme to
                        if (!_hasWarmedAfterLogin)
                        {
                            _hasWarmedAfterLogin = true;
                            Logger.LogInformation("Initial cache warming completed successfully");
                        }
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Logger.LogDebug("Cache warming skipped - authentication expired during operation");
                        SetMetric("failed_auth", GetMetric<long>("failed_auth") + 1);
                        SetMetric("last_check_result", "auth_failed");
                        // Nejde o fatální chybu, tokeny mohly expirovat během operace
                    }
                    catch (HttpRequestException ex)
                    {
                        Logger.LogWarning(ex, "Cache warming failed due to network issues");
                        SetMetric("failed_network", GetMetric<long>("failed_network") + 1);
                        SetMetric("last_check_result", "network_failed");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to warm channel cache");
                        SetMetric("failed_other", GetMetric<long>("failed_other") + 1);
                        SetMetric("last_check_result", "other_failed");
                    }

                    return true;
                });

                // Dynamický interval na základě úspěchu
                var delayHours = GetDynamicInterval();
                Logger.LogDebug("Next cache warming in {Hours} hours", delayHours);

                await Task.Delay(TimeSpan.FromHours(delayHours), stoppingToken);
                UpdateHeartbeat();
            }
        }

        public bool HasWarmedSuccessfully => _hasWarmedAfterLogin;
        public DateTime? LastSuccessfulWarm => _lastSuccessfulWarm == DateTime.MinValue ? null : _lastSuccessfulWarm;


        /// <summary>
        /// Allows other components to explicitly request a cache warm-up. This
        /// is typically invoked from event handlers when new tokens become
        /// available.
        /// </summary>
        public async Task TriggerWarmingAsync()
        {
            try
            {
                Logger.LogInformation("Manual cache warming triggered");

                using var scope = CreateScope();
                var channelService = scope.ServiceProvider.GetRequiredService<IChannelService>();

                var channels = await channelService.GetChannelsAsync();
                Logger.LogInformation("Manual cache warming completed with {ChannelCount} channels", channels.Count);

                SetMetric("manual_warms", GetMetric<long>("manual_warms") + 1);
                _hasWarmedAfterLogin = true;
                _lastSuccessfulWarm = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Manual cache warming failed");
                SetMetric("manual_warm_failures", GetMetric<long>("manual_warm_failures") + 1);
            }
        }

        /// <summary>
        /// Checks whether the channel data are already present in the cache.
        /// </summary>
        private bool IsCacheAlreadyWarmed(IMemoryCache cache)
        {
            // Look for the cached channel list entry
            return cache.TryGetValue("channels", out _);
        }

        /// <summary>
        /// Calculates the next execution interval based on the success of the
        /// previous cache warming attempt.
        /// </summary>
        private double GetDynamicInterval()
        {
            // If warming has never succeeded run more frequently
            if (_lastSuccessfulWarm == DateTime.MinValue)
            {
                return 0.5; // 30 minut
            }

            // When the last run succeeded recently we can wait longer
            var timeSinceLastWarm = DateTime.UtcNow - _lastSuccessfulWarm;
            if (timeSinceLastWarm < TimeSpan.FromHours(2))
            {
                return 4; // 4 hodiny
            }

            // Default interval
            return 2; // 2 hodiny
        }

        /// <summary>
        /// Adds cache-warming specific metrics to the service health report.
        /// </summary>
        public override Task<ServiceHealth> GetHealthAsync()
        {
            var baseHealth = base.GetHealthAsync().Result;

            // Přidáme cache warming specific metriky
            baseHealth.Metrics["has_warmed_after_login"] = _hasWarmedAfterLogin;
            baseHealth.Metrics["last_successful_warm"] = _lastSuccessfulWarm;
            baseHealth.Metrics["time_since_last_warm"] = DateTime.UtcNow - _lastSuccessfulWarm;

            return Task.FromResult(baseHealth);
        }
    }
}