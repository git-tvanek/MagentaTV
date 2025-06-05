using MagentaTV.Application.Events;
using MagentaTV.Services.Background.Core;
using MagentaTV.Services.Background;
using MediatR;

namespace MagentaTV.Application.EventHandlers
{
    public class UserLoggedInEventHandler : INotificationHandler<UserLoggedInEvent>
    {
        private readonly IBackgroundServiceManager _backgroundManager;
        private readonly ILogger<UserLoggedInEventHandler> _logger;

        public UserLoggedInEventHandler(
            IBackgroundServiceManager backgroundManager,
            ILogger<UserLoggedInEventHandler> logger)
        {
            _backgroundManager = backgroundManager;
            _logger = logger;
        }

        public async Task Handle(UserLoggedInEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("User {Username} logged in, queuing background tasks", notification.Username);

            // Pre-load user specific data
            var preloadWork = new BackgroundWorkItem
            {
                Name = "User Data Preload",
                Type = "USER_PRELOAD",
                Priority = 10,
                Parameters = new() { ["username"] = notification.Username }
            };

            await _backgroundManager.QueueWorkItemAsync(preloadWork);

            // Update user statistics
            var statsWork = new BackgroundWorkItem
            {
                Name = "Update User Stats",
                Type = "USER_STATS",
                Priority = 1,
                Parameters = new()
                {
                    ["username"] = notification.Username,
                    ["loginTime"] = notification.Timestamp,
                    ["ipAddress"] = notification.IpAddress
                }
            };

            await _backgroundManager.QueueWorkItemAsync(statsWork);
        }
    }
}
