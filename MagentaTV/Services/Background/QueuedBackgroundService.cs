using MagentaTV.Models.Background;
using MagentaTV.Services.Background.Core;
using MagentaTV.Services.Background.Events;
using System.Diagnostics;

namespace MagentaTV.Services.Background
{
    public class QueuedBackgroundService : BaseBackgroundService
    {
        private readonly IBackgroundTaskQueue _taskQueue;

        public QueuedBackgroundService(
            IBackgroundTaskQueue taskQueue,
            ILogger<QueuedBackgroundService> logger,
            IServiceProvider serviceProvider,
            IEventBus eventBus)
            : base(logger, serviceProvider, eventBus, "QueuedBackgroundService")
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
                        await Task.Delay(100, stoppingToken);
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
                    SetMetric("processing_errors", GetMetric<long>("processing_errors") + 1);
                }
            }
        }

        private async Task ProcessWorkItemAsync(BackgroundWorkItem workItem, CancellationToken stoppingToken)
        {
            workItem.Status = BackgroundWorkItemStatus.Running;
            workItem.StartedAt = DateTime.UtcNow;

            // Publish start event
            await EventBus.PublishAsync(new WorkItemStartedEvent
            {
                WorkItemId = workItem.Id,
                WorkItemName = workItem.Name,
                WorkItemType = workItem.Type,
                StartedAt = workItem.StartedAt.Value
            });

            Logger.LogInformation("Processing work item {Id} ({Name})", workItem.Id, workItem.Name);

            var stopwatch = Stopwatch.StartNew();
            bool success = false;
            string? errorMessage = null;

            try
            {
                using var scope = CreateScope();
                await workItem.WorkItem(scope.ServiceProvider, stoppingToken);

                workItem.Status = BackgroundWorkItemStatus.Completed;
                success = true;

                Logger.LogInformation("Completed work item {Id} ({Name}) in {Duration}ms",
                    workItem.Id, workItem.Name, stopwatch.ElapsedMilliseconds);

                SetMetric("processed_items", GetMetric<long>("processed_items") + 1);
                SetMetric($"processed_{workItem.Type}", GetMetric<long>($"processed_{workItem.Type}") + 1);
            }
            catch (Exception ex)
            {
                workItem.Status = BackgroundWorkItemStatus.Failed;
                workItem.Exceptions.Add(ex);
                workItem.ErrorMessage = ex.Message;
                errorMessage = ex.Message;

                Logger.LogError(ex, "Work item {Id} ({Name}) failed", workItem.Id, workItem.Name);

                SetMetric("failed_items", GetMetric<long>("failed_items") + 1);
                SetMetric($"failed_{workItem.Type}", GetMetric<long>($"failed_{workItem.Type}") + 1);
            }
            finally
            {
                stopwatch.Stop();
                workItem.CompletedAt = DateTime.UtcNow;

                // Publish completion event
                await EventBus.PublishAsync(new WorkItemCompletedEvent
                {
                    WorkItemId = workItem.Id,
                    WorkItemName = workItem.Name,
                    Success = success,
                    Duration = stopwatch.Elapsed,
                    ErrorMessage = errorMessage
                });
            }
        }
    }
}