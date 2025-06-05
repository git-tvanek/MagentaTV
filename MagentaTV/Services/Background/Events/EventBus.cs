using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace MagentaTV.Services.Background.Events
{
    public class EventBus : IEventBus
    {
        private readonly ILogger<EventBus> _logger;
        private readonly ConcurrentDictionary<Type, List<object>> _handlers = new();

        public EventBus(ILogger<EventBus> logger)
        {
            _logger = logger;
        }

        public async Task PublishAsync<T>(T eventData, CancellationToken cancellationToken = default) where T : class
        {
            var eventType = typeof(T);

            if (!_handlers.TryGetValue(eventType, out var handlers) || !handlers.Any())
            {
                _logger.LogDebug("No handlers registered for event type {EventType}", eventType.Name);
                return;
            }

            var tasks = new List<Task>();

            foreach (var handler in handlers.ToList()) // ToList to avoid collection modification during iteration
            {
                try
                {
                    var task = handler switch
                    {
                        IEventHandler<T> typedHandler => typedHandler.HandleAsync(eventData, cancellationToken),
                        Func<T, CancellationToken, Task> funcHandler => funcHandler(eventData, cancellationToken),
                        _ => Task.CompletedTask
                    };

                    tasks.Add(task);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing handler for event {EventType}", eventType.Name);
                }
            }

            try
            {
                await Task.WhenAll(tasks);
                _logger.LogDebug("Published event {EventType} to {HandlerCount} handlers", eventType.Name, handlers.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in event handlers for {EventType}", eventType.Name);
            }
        }

        public void Subscribe<T>(IEventHandler<T> handler) where T : class
        {
            var eventType = typeof(T);
            _handlers.AddOrUpdate(eventType,
                new List<object> { handler },
                (key, existing) => { existing.Add(handler); return existing; });

            _logger.LogDebug("Subscribed handler {HandlerType} to event {EventType}",
                handler.GetType().Name, eventType.Name);
        }

        public void Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : class
        {
            var eventType = typeof(T);
            _handlers.AddOrUpdate(eventType,
                new List<object> { handler },
                (key, existing) => { existing.Add(handler); return existing; });

            _logger.LogDebug("Subscribed function handler to event {EventType}", eventType.Name);
        }

        public void Unsubscribe<T>(IEventHandler<T> handler) where T : class
        {
            var eventType = typeof(T);
            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
                _logger.LogDebug("Unsubscribed handler {HandlerType} from event {EventType}",
                    handler.GetType().Name, eventType.Name);
            }
        }
    }
}

