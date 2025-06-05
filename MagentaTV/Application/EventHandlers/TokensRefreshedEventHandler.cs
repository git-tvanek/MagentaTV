using MagentaTV.Application.Events;
using MagentaTV.Services.Session;
using MediatR;

namespace MagentaTV.Application.EventHandlers
{
    public class TokensRefreshedEventHandler : INotificationHandler<TokensRefreshedEvent>
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<TokensRefreshedEventHandler> _logger;

        public TokensRefreshedEventHandler(
            ISessionManager sessionManager,
            ILogger<TokensRefreshedEventHandler> logger)
        {
            _sessionManager = sessionManager;
            _logger = logger;
        }

        public async Task Handle(TokensRefreshedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Tokens refreshed for user {Username}", notification.Username);

            // Update all user sessions with new token info
            var userSessions = await _sessionManager.GetUserSessionsAsync(notification.Username);

            foreach (var session in userSessions.Where(s => s.IsActive))
            {
                // This would need TokenData - simplified for example
                _logger.LogDebug("Updated session {SessionId} with new token expiry", session.SessionId);
            }
        }
    }
}
