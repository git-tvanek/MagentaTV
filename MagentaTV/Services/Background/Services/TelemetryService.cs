using MagentaTV.Configuration;
using MagentaTV.Services.Background.Core;
using MagentaTV.Services.Background.Events;
using MagentaTV.Services.Background;
using MagentaTV.Services.Cache;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Linq;

namespace MagentaTV.Services.Background.Services;

public class TelemetryService : BaseBackgroundService, ITelemetryService
{
    private readonly TelemetryOptions _options;

    public TelemetryService(
        ILogger<TelemetryService> logger,
        IServiceProvider serviceProvider,
        IEventBus eventBus,
        IOptions<TelemetryOptions> options)
        : base(logger, serviceProvider, eventBus, "TelemetryService")
    {
        _options = options.Value;
    }

    public async Task CollectAsync()
    {
        await CollectTelemetryAsync(CancellationToken.None);
    }

    protected override async Task ExecuteServiceAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CollectTelemetryAsync(stoppingToken);

            await Task.Delay(TimeSpan.FromMinutes(_options.IntervalMinutes), stoppingToken);
            UpdateHeartbeat();
        }
    }

    private async Task<bool> CollectTelemetryAsync(CancellationToken stoppingToken)
    {
        return await ExecuteWithEventsAsync("CollectTelemetry", async () =>
        {
            using var scope = CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<IBackgroundServiceManager>();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            var stats = await manager.GetStatsAsync();
            var services = await manager.GetAllServicesInfoAsync();
            var cacheStats = await cache.GetStatisticsAsync();

            var serviceInfo = services.Select(s => new
            {
                Type = s.Type.FullName,
                s.Name,
                s.Status,
                s.StartedAt,
                s.StoppedAt,
                s.ErrorMessage,
                Metrics = s.Metrics
            }).ToList();

            var telemetry = new
            {
                Timestamp = DateTime.UtcNow,
                BackgroundStats = stats,
                ServiceInfo = serviceInfo,
                CacheStats = cacheStats
            };

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_options.LogFilePath)!);
                var json = JsonSerializer.Serialize(telemetry);
                await File.AppendAllTextAsync(_options.LogFilePath, json + Environment.NewLine, stoppingToken);
                SetMetric("last_write", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to write telemetry data");
                SetMetric("write_errors", GetMetric<long>("write_errors") + 1);
            }

            return true;
        });
    }
}
