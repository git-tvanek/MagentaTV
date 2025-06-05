using MagentaTV.Services.Background.Core.MagentaTV.Services.Background.Core;
using MagentaTV.Services.Background.Events;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace MagentaTV.Services.Background.Core
{
    public abstract class BaseBackgroundService : BackgroundService, IBackgroundService
    {
        protected readonly ILogger Logger;
        protected readonly IServiceProvider ServiceProvider;
        protected readonly IEventBus EventBus;

        public string ServiceName { get; }

        private DateTime _lastHeartbeat = DateTime.UtcNow;
        private readonly ConcurrentDictionary<string, object> _metrics = new();
        private volatile bool _isHealthy = true;
        private string _status = "Starting";

        protected BaseBackgroundService(
            ILogger logger,
            IServiceProvider serviceProvider,
            IEventBus eventBus,
            string? serviceName = null)
        {
            Logger = logger;
            ServiceProvider = serviceProvider;
            EventBus = eventBus;
            ServiceName = serviceName ?? GetType().Name;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.LogInformation("Background service {ServiceName} starting", ServiceName);
            _status = "Running";
            _isHealthy = true;

            await PublishHealthChangeAsync();

            try
            {
                await ExecuteServiceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                Logger.LogInformation("Background service {ServiceName} was cancelled", ServiceName);
                _status = "Cancelled";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Background service {ServiceName} failed", ServiceName);
                _status = "Failed";
                _isHealthy = false;
                await PublishHealthChangeAsync();
                throw;
            }
            finally
            {
                _status = "Stopped";
                await PublishHealthChangeAsync();
                Logger.LogInformation("Background service {ServiceName} stopped", ServiceName);
            }
        }

        protected abstract Task ExecuteServiceAsync(CancellationToken stoppingToken);

        public virtual Task<ServiceHealth> GetHealthAsync()
        {
            var health = new ServiceHealth
            {
                ServiceName = ServiceName,
                IsHealthy = _isHealthy && DateTime.UtcNow - _lastHeartbeat < TimeSpan.FromMinutes(5),
                Status = _status,
                LastHeartbeat = _lastHeartbeat,
                Metrics = new Dictionary<string, object>(_metrics)
            };

            return Task.FromResult(health);
        }

        protected void UpdateHeartbeat()
        {
            _lastHeartbeat = DateTime.UtcNow;
        }

        protected void SetMetric(string name, object value)
        {
            _metrics.AddOrUpdate(name, value, (key, oldValue) => value);
        }

        protected T? GetMetric<T>(string name)
        {
            if (_metrics.TryGetValue(name, out var value) && value is T typedValue)
                return typedValue;
            return default;
        }

        protected IServiceScope CreateScope()
        {
            return ServiceProvider.CreateScope();
        }

        protected async Task<T> ExecuteWithEventsAsync<T>(
            string operationName,
            Func<Task<T>> operation)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await operation();
                stopwatch.Stop();

                SetMetric($"{operationName}_last_duration_ms", stopwatch.ElapsedMilliseconds);
                SetMetric($"{operationName}_success_count", GetMetric<long>($"{operationName}_success_count") + 1);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                SetMetric($"{operationName}_error_count", GetMetric<long>($"{operationName}_error_count") + 1);
                SetMetric($"{operationName}_last_error", ex.Message);

                Logger.LogError(ex, "Operation {OperationName} failed in service {ServiceName}",
                    operationName, ServiceName);
                throw;
            }
        }

        private async Task PublishHealthChangeAsync()
        {
            try
            {
                await EventBus.PublishAsync(new ServiceHealthChangedEvent
                {
                    ServiceName = ServiceName,
                    IsHealthy = _isHealthy,
                    Status = _status,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to publish health change event for {ServiceName}", ServiceName);
            }
        }
    }
}
