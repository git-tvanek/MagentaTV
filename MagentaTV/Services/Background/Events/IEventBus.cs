using System;

namespace MagentaTV.Services.Background.Events
{
    public interface IEventBus
    {
        Task PublishAsync<T>(T eventData, CancellationToken cancellationToken = default) where T : class;
        void Subscribe<T>(IEventHandler<T> handler) where T : class;
        void Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : class;
        void Unsubscribe<T>(IEventHandler<T> handler) where T : class;
    }
}
