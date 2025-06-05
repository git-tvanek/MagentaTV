using MagentaTV.Configuration;
using MagentaTV.Services.Background.Core;
using MagentaTV.Services.Background.Events;
using MagentaTV.Services.Background.Services;

namespace MagentaTV.Extensions
{
    public static class BackgroundServiceExtensions
    {
        public static IServiceCollection AddBackgroundServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure options
            services.Configure<BackgroundServiceOptions>(
                configuration.GetSection(BackgroundServiceOptions.SectionName));

            // Register core services
            services.AddSingleton<IEventBus, EventBus>();
            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();

            // Register event handlers
            services.AddSingleton<BackgroundEventHandlers>();

            // Register the queued background service
            services.AddHostedService<QueuedBackgroundService>();

            // Subscribe event handlers
            services.AddSingleton<IHostedService>(provider =>
            {
                var eventBus = provider.GetRequiredService<IEventBus>();
                var handlers = provider.GetRequiredService<BackgroundEventHandlers>();

                eventBus.Subscribe<WorkItemStartedEvent>(handlers);
                eventBus.Subscribe<WorkItemCompletedEvent>(handlers);
                eventBus.Subscribe<ServiceHealthChangedEvent>(handlers);

                return new EmptyHostedService(); // Just for registration
            });

            return services;
        }

        public static IServiceCollection AddBackgroundService<T>(
            this IServiceCollection services)
            where T : BaseBackgroundService
        {
            services.AddSingleton<T>();
            services.AddHostedService<T>();
            return services;
        }

        // Helper methods for creating work items
        public static BackgroundWorkItem CreateWorkItem(
            string name,
            string type,
            Func<IServiceProvider, CancellationToken, Task> workItem,
            int priority = 0,
            Dictionary<string, object>? parameters = null)
        {
            return new BackgroundWorkItem
            {
                Name = name,
                Type = type,
                WorkItem = workItem,
                Priority = priority,
                Parameters = parameters ?? new Dictionary<string, object>()
            };
        }

        public static BackgroundWorkItem CreateScheduledWorkItem(
            string name,
            string type,
            Func<IServiceProvider, CancellationToken, Task> workItem,
            DateTime scheduledFor,
            int priority = 0,
            Dictionary<string, object>? parameters = null)
        {
            return new BackgroundWorkItem
            {
                Name = name,
                Type = type,
                WorkItem = workItem,
                ScheduledFor = scheduledFor,
                Priority = priority,
                Parameters = parameters ?? new Dictionary<string, object>()
            };
        }

        private class EmptyHostedService : IHostedService
        {
            public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}