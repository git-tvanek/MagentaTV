using MagentaTV.Models;
using MagentaTV.Services.Session;
using MediatR;
using MagentaTV.Application.Events;

namespace MagentaTV.Application.Commands
{
    public class RevokeSessionCommandHandler : IRequestHandler<RevokeSessionCommand, ApiResponse<string>>
    {
        private readonly ISessionManager _sessionManager;
        private readonly IMediator _mediator;
        private readonly ILogger<RevokeSessionCommandHandler> _logger;

        public RevokeSessionCommandHandler(
            ISessionManager sessionManager,
            IMediator mediator,
            ILogger<RevokeSessionCommandHandler> logger)
        {
            _sessionManager = sessionManager;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<ApiResponse<string>> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var currentSession = await _sessionManager.GetSessionAsync(request.CurrentSessionId);
                var targetSession = await _sessionManager.GetSessionAsync(request.TargetSessionId);

                if (currentSession?.IsActive != true)
                {
                    return ApiResponse<string>.ErrorResult("Invalid current session");
                }

                if (targetSession == null)
                {
                    return ApiResponse<string>.ErrorResult("Target session not found");
                }

                // Uživatel může rušit pouze své vlastní sessions
                if (!currentSession.Username.Equals(targetSession.Username, StringComparison.OrdinalIgnoreCase))
                {
                    return ApiResponse<string>.ErrorResult("Forbidden",
                        new List<string> { "You can only revoke your own sessions" });
                }

                await _sessionManager.RemoveSessionAsync(request.TargetSessionId);

                // Publikujeme event
                await _mediator.Publish(new UserLoggedOutEvent
                {
                    Username = targetSession.Username,
                    SessionId = request.TargetSessionId,
                    Timestamp = DateTime.UtcNow,
                    Reason = "Revoked"
                }, cancellationToken);

                _logger.LogInformation("Session {SessionId} revoked by user {Username}",
                    request.TargetSessionId, currentSession.Username);

                return ApiResponse<string>.SuccessResult("Session revoked successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to revoke session {SessionId}", request.TargetSessionId);
                return ApiResponse<string>.ErrorResult("Internal server error");
            }
        }
    }
}