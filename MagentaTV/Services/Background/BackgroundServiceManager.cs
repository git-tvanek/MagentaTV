using MagentaTV.Models.Background;
using MagentaTV.Services.Background.Core;
using System.Collections.Concurrent;

namespace MagentaTV.Services.Background
{
    public class BackgroundServiceManager : IBackgroundServiceManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly ILogger<BackgroundServiceManager> _logger;
        private readonly ConcurrentDictionary<Type, BackgroundServiceInfo> _services = new();

        public BackgroundServiceManager(
            IServiceProvider serviceProvider,
            IBackgroundTaskQueue taskQueue,
            ILogger<BackgroundServiceManager> logger)
        {
            _serviceProvider = serviceProvider;
            _taskQueue = taskQueue;
            _logger = logger;
        }

        public async Task StartServiceAsync<T>() where T : BaseBackgroundService
        {
            var serviceType = typeof(T);
            var service = _serviceProvider.GetService<T>();

            if (service == null)
            {
                throw new InvalidOperationException($"Service {serviceType.Name} is not registered");
            }

            var info = new BackgroundServiceInfo
            {
                Type = serviceType,
                Name = serviceType.Name,
                Status = BackgroundServiceStatus.Starting,
                StartedAt = DateTime.UtcNow
            };

            _services.AddOrUpdate(serviceType, info, (key, existing) => info);

            try
            {
                await service.StartAsync(CancellationToken.None);
                info.Status = BackgroundServiceStatus.Running;

                _logger.LogInformation("Started background service {ServiceName}", serviceType.Name);
            }
            catch (Exception ex)
            {
                info.Status = BackgroundServiceStatus.Failed;
                info.ErrorMessage = ex.Message;

                _logger.LogError(ex, "Failed to start background service {ServiceName}", serviceType.Name);
                throw;
            }
        }

        public async Task StopServiceAsync<T>() where T : BaseBackgroundService
        {
            var serviceType = typeof(T);

            if (_services.TryGetValue(serviceType, out var info))
            {
                info.Status = BackgroundServiceStatus.Stopping;

                var service = _serviceProvider.GetService<T>();
                if (service != null)
                {
                    await service.StopAsync(CancellationToken.None);
                    info.Status = BackgroundServiceStatus.Stopped;
                    info.StoppedAt = DateTime.UtcNow;

                    _logger.LogInformation("Stopped background service {ServiceName}", serviceType.Name);
                }
            }
        }

        public Task<BackgroundServiceInfo?> GetServiceInfoAsync<T>() where T : BaseBackgroundService
        {
            var serviceType = typeof(T);
            _services.TryGetValue(serviceType, out var info);
            return Task.FromResult(info);
        }

        public Task<List<BackgroundServiceInfo>> GetAllServicesInfoAsync()
        {
            return Task.FromResult(_services.Values.ToList());
        }

        public async Task QueueWorkItemAsync(BackgroundWorkItem workItem)
        {
            await _taskQueue.QueueBackgroundWorkItemAsync(workItem);
            _logger.LogDebug("Queued work item {Id} ({Name})", workItem.Id, workItem.Name);
        }

        public Task<BackgroundServiceStats> GetStatsAsync()
        {
            var stats = new BackgroundServiceStats
            {
                TotalServices = _services.Count,
                RunningServices = _services.Values.Count(s => s.Status == BackgroundServiceStatus.Running),
                QueuedItems = _taskQueue.Count,
                QueueCapacity = _taskQueue.Capacity,
                LastUpdated = DateTime.UtcNow
            };

            return Task.FromResult(stats);
        }
    }

}
