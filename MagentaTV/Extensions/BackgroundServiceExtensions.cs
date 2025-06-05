using MagentaTV.Configuration;
using MagentaTV.Services.Background;

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

            // Register core background services
            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
            services.AddSingleton<IBackgroundServiceManager, BackgroundServiceManager>();

            // Register the queued background service
            services.AddHostedService<QueuedBackgroundService>();

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

        /// <summary>
        /// Helper pro vytvoření background work item
        /// </summary>
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

        /// <summary>
        /// Helper pro scheduled work item
        /// </summary>
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
    }
}
