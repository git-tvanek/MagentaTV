namespace MagentaTV.Services.Background.Events
{
    public class BackgroundEventHandlers :
        IEventHandler<WorkItemStartedEvent>,
        IEventHandler<WorkItemCompletedEvent>,
        IEventHandler<ServiceHealthChangedEvent>
    {
        private readonly ILogger<BackgroundEventHandlers> _logger;

        public BackgroundEventHandlers(ILogger<BackgroundEventHandlers> logger)
        {
            _logger = logger;
        }

        public Task HandleAsync(WorkItemStartedEvent eventData, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Work item started: {WorkItemName} ({WorkItemId}) at {StartedAt}",
                eventData.WorkItemName, eventData.WorkItemId, eventData.StartedAt);

            // Here you could update external monitoring systems, databases, etc.
            return Task.CompletedTask;
        }

        public Task HandleAsync(WorkItemCompletedEvent eventData, CancellationToken cancellationToken = default)
        {
            if (eventData.Success)
            {
                _logger.LogInformation("Work item completed successfully: {WorkItemName} in {Duration}",
                    eventData.WorkItemName, eventData.Duration);
            }
            else
            {
                _logger.LogWarning("Work item failed: {WorkItemName} - {ErrorMessage}",
                    eventData.WorkItemName, eventData.ErrorMessage);
            }

            // Here you could send notifications, update metrics, etc.
            return Task.CompletedTask;
        }

        public Task HandleAsync(ServiceHealthChangedEvent eventData, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Service health changed: {ServiceName} is now {Status} (Healthy: {IsHealthy})",
                eventData.ServiceName, eventData.Status, eventData.IsHealthy);

            // Here you could send alerts, update health dashboards, etc.
            return Task.CompletedTask;
        }
    }
}