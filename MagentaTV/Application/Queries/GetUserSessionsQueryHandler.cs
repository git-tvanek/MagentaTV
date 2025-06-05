using MagentaTV.Models;
using MagentaTV.Models.Session;
using MagentaTV.Services.Session;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GetUserSessionsQueryHandler : IRequestHandler<GetUserSessionsQuery, ApiResponse<List<SessionDto>>>
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<GetUserSessionsQueryHandler> _logger;

        public GetUserSessionsQueryHandler(ISessionManager sessionManager, ILogger<GetUserSessionsQueryHandler> logger)
        {
            _sessionManager = sessionManager;
            _logger = logger;
        }

        public async Task<ApiResponse<List<SessionDto>>> Handle(GetUserSessionsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var currentSession = await _sessionManager.GetSessionAsync(request.CurrentSessionId);
                if (currentSession?.IsActive != true)
                {
                    return ApiResponse<List<SessionDto>>.ErrorResult("Invalid session");
                }

                var userSessions = await _sessionManager.GetUserSessionsAsync(currentSession.Username);
                var sessionDtos = userSessions.Select(s => new SessionDto
                {
                    SessionId = s.SessionId,
                    Username = s.Username,
                    CreatedAt = s.CreatedAt,
                    LastActivity = s.LastActivity,
                    ExpiresAt = s.ExpiresAt,
                    IpAddress = s.IpAddress,
                    UserAgent = s.UserAgent,
                    Status = s.Status
                }).ToList();

                return ApiResponse<List<SessionDto>>.SuccessResult(sessionDtos,
                    $"Nalezeno {sessionDtos.Count} sessions");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get user sessions for session {SessionId}", request.CurrentSessionId);
                return ApiResponse<List<SessionDto>>.ErrorResult("Internal server error");
            }
        }
    }
}