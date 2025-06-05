using MagentaTV.Models;
using MagentaTV.Models.Session;
using MagentaTV.Services.Session;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GetCurrentSessionQueryHandler : IRequestHandler<GetCurrentSessionQuery, ApiResponse<SessionInfoDto>>
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<GetCurrentSessionQueryHandler> _logger;

        public GetCurrentSessionQueryHandler(ISessionManager sessionManager, ILogger<GetCurrentSessionQueryHandler> logger)
        {
            _sessionManager = sessionManager;
            _logger = logger;
        }

        public async Task<ApiResponse<SessionInfoDto>> Handle(GetCurrentSessionQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var sessionInfo = await _sessionManager.GetSessionInfoAsync(request.SessionId);
                if (sessionInfo == null)
                {
                    return ApiResponse<SessionInfoDto>.ErrorResult("Session not found");
                }

                if (!sessionInfo.IsExpired)
                {
                    await _sessionManager.UpdateSessionActivityAsync(request.SessionId);
                }

                return ApiResponse<SessionInfoDto>.SuccessResult(sessionInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get session info for {SessionId}", request.SessionId);
                return ApiResponse<SessionInfoDto>.ErrorResult("Internal server error");
            }
        }
    }
}