using MagentaTV.Configuration;
using MagentaTV.Models.Background;
using Microsoft.Extensions.Options;

namespace MagentaTV.Services.Background
{
    /// <summary>
    /// Background service pro zpracování úloh z fronty
    /// </summary>
    public class QueuedBackgroundService : BaseBackgroundService
    {
        private readonly IBackgroundTaskQueue _taskQueue;

        public QueuedBackgroundService(
            IBackgroundTaskQueue taskQueue,
            ILogger<QueuedBackgroundService> logger,
            IServiceProvider serviceProvider,
            IOptions<BackgroundServiceOptions> options)
            : base(logger, serviceProvider, options, "QueuedBackgroundService")
        {
            _taskQueue = taskQueue;
        }

        protected override async Task ExecuteServiceAsync(CancellationToken stoppingToken)
        {
            Logger.LogInformation("Queued background service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                    if (workItem == null)
                    {
                        await WaitForIntervalAsync(TimeSpan.FromMilliseconds(100), stoppingToken);
                        continue;
                    }

                    await ProcessWorkItemAsync(workItem, stoppingToken);
                    UpdateHeartbeat();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error processing background work items");

                    if (!Options.ContinueOnError)
                    {
                        throw;
                    }
                }
            }
        }

        private async Task ProcessWorkItemAsync(BackgroundWorkItem workItem, CancellationToken stoppingToken)
        {
            workItem.Status = BackgroundWorkItemStatus.Running;
            workItem.StartedAt = DateTime.UtcNow;

            Logger.LogInformation("Processing background work item {Id} ({Name})", workItem.Id, workItem.Name);

            try
            {
                using var scope = CreateScope();
                await workItem.WorkItem(scope.ServiceProvider, stoppingToken);

                workItem.Status = BackgroundWorkItemStatus.Completed;
                workItem.CompletedAt = DateTime.UtcNow;

                Logger.LogInformation("Completed background work item {Id} ({Name}) in {Duration}ms",
                    workItem.Id, workItem.Name,
                    (workItem.CompletedAt - workItem.StartedAt)?.TotalMilliseconds);

                // OPRAVA: Správné přetypování object na long
                var processedItems = GetMetricValue<long>("processed_items", 0L);
                RecordMetric("processed_items", processedItems + 1L);
            }
            catch (Exception ex)
            {
                workItem.Exceptions.Add(ex);
                workItem.ErrorMessage = ex.Message;
                workItem.RetryCount++;

                Logger.LogWarning(ex, "Background work item {Id} ({Name}) failed on attempt {Attempt}",
                    workItem.Id, workItem.Name, workItem.RetryCount);

                if (workItem.RetryCount < workItem.MaxRetries)
                {
                    workItem.Status = BackgroundWorkItemStatus.Retrying;
                    workItem.ScheduledFor = DateTime.UtcNow.Add(workItem.RetryDelay);

                    Logger.LogInformation("Retrying background work item {Id} in {Delay}",
                        workItem.Id, workItem.RetryDelay);

                    // Přidáme zpět do fronty pro retry
                    await _taskQueue.QueueBackgroundWorkItemAsync(workItem);
                }
                else
                {
                    workItem.Status = BackgroundWorkItemStatus.Failed;
                    workItem.CompletedAt = DateTime.UtcNow;

                    Logger.LogError(ex, "Background work item {Id} ({Name}) failed after {MaxRetries} attempts",
                        workItem.Id, workItem.Name, workItem.MaxRetries);

                    // OPRAVA: Správné přetypování object na long
                    var failedItems = GetMetricValue<long>("failed_items", 0L);
                    RecordMetric("failed_items", failedItems + 1L);
                }
            }
        }

        /// <summary>
        /// Helper metoda pro bezpečné získání metriky s typovou kontrolou
        /// </summary>
        private T GetMetricValue<T>(string metricName, T defaultValue)
        {
            var metrics = GetMetrics();
            if (metrics.TryGetValue(metricName, out var value))
            {
                try
                {
                    if (value is T typedValue)
                    {
                        return typedValue;
                    }

                    // Pokus o konverzi
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to convert metric {MetricName} value {Value} to type {Type}, using default {Default}",
                        metricName, value, typeof(T).Name, defaultValue);
                }
            }

            return defaultValue;
        }
    }
}