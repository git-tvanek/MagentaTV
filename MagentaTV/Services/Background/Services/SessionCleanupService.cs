using MagentaTV.Services.Background.Core;
using MagentaTV.Services.Background.Events;
using MagentaTV.Services.Session;

namespace MagentaTV.Services.Background.Services
{
    public class SessionCleanupService : BaseBackgroundService
    {
        public SessionCleanupService(
            ILogger<SessionCleanupService> logger,
            IServiceProvider serviceProvider,
            IEventBus eventBus)
            : base(logger, serviceProvider, eventBus, "SessionCleanupService")
        {
        }

        protected override async Task ExecuteServiceAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ExecuteWithEventsAsync("SessionCleanup", async () =>
                {
                    using var scope = CreateScope();
                    var sessionManager = scope.ServiceProvider.GetRequiredService<ISessionManager>();

                    await sessionManager.CleanupExpiredSessionsAsync();

                    SetMetric("last_cleanup", DateTime.UtcNow);
                    SetMetric("cleanup_count", GetMetric<long>("cleanup_count") + 1);

                    return true;
                });

                // Cleanup každou hodinu
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                UpdateHeartbeat();
            }
        }
    }
}
