using MagentaTV.Services.Background.Core;
using MagentaTV.Services.Background.Events;
using Microsoft.Extensions.Caching.Memory;

namespace MagentaTV.Services.Background.Services
{
    public class CacheWarmingService : BaseBackgroundService
    {
        public CacheWarmingService(
            ILogger<CacheWarmingService> logger,
            IServiceProvider serviceProvider,
            IEventBus eventBus)
            : base(logger, serviceProvider, eventBus, "CacheWarmingService")
        {
        }

        protected override async Task ExecuteServiceAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ExecuteWithEventsAsync("CacheWarming", async () =>
                {
                    using var scope = CreateScope();
                    var magentaService = scope.ServiceProvider.GetRequiredService<IMagenta>();
                    var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

                    // Pre-load kanály do cache
                    try
                    {
                        var channels = await magentaService.GetChannelsAsync();
                        Logger.LogInformation("Warmed cache with {ChannelCount} channels", channels.Count);

                        SetMetric("warmed_channels", channels.Count);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to warm channel cache");
                    }

                    return true;
                });

                // Warm cache každé 4 hodiny
                await Task.Delay(TimeSpan.FromHours(4), stoppingToken);
                UpdateHeartbeat();
            }
        }
    }
}
