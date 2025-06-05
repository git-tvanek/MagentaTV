using MagentaTV.Models;
using MagentaTV.Services.Session;
using MediatR;
using MagentaTV.Application.Events;

namespace MagentaTV.Application.Commands
{
    public class LogoutAllOtherSessionsCommandHandler : IRequestHandler<LogoutAllOtherSessionsCommand, ApiResponse<string>>
    {
        private readonly ISessionManager _sessionManager;
        private readonly IMediator _mediator;
        private readonly ILogger<LogoutAllOtherSessionsCommandHandler> _logger;

        public LogoutAllOtherSessionsCommandHandler(
            ISessionManager sessionManager,
            IMediator mediator,
            ILogger<LogoutAllOtherSessionsCommandHandler> logger)
        {
            _sessionManager = sessionManager;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<ApiResponse<string>> Handle(LogoutAllOtherSessionsCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var currentSession = await _sessionManager.GetSessionAsync(request.CurrentSessionId);
                if (currentSession?.IsActive != true)
                {
                    return ApiResponse<string>.ErrorResult("Invalid session");
                }

                var userSessions = await _sessionManager.GetUserSessionsAsync(currentSession.Username);
                var otherSessions = userSessions.Where(s => s.SessionId != request.CurrentSessionId).ToList();

                foreach (var session in otherSessions)
                {
                    await _sessionManager.RemoveSessionAsync(session.SessionId);

                    // Publikujeme event pro každou session
                    await _mediator.Publish(new UserLoggedOutEvent
                    {
                        Username = session.Username,
                        SessionId = session.SessionId,
                        Timestamp = DateTime.UtcNow,
                        Reason = "Concurrent_Limit"
                    }, cancellationToken);
                }

                _logger.LogInformation("All other sessions logged out for user {Username}, kept current: {SessionId}",
                    currentSession.Username, request.CurrentSessionId);

                return ApiResponse<string>.SuccessResult("All other sessions logged out",
                    $"Ukončeno {otherSessions.Count} ostatních sessions");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to logout all other sessions for {SessionId}", request.CurrentSessionId);
                return ApiResponse<string>.ErrorResult("Internal server error");
            }
        }
    }
}