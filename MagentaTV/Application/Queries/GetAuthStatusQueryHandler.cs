using MagentaTV.Models;
using MagentaTV.Services.TokenStorage;
using MagentaTV.Extensions;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GetAuthStatusQueryHandler : IRequestHandler<GetAuthStatusQuery, ApiResponse<AuthStatusDto>>
    {
        private readonly ITokenStorage _tokenStorage;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<GetAuthStatusQueryHandler> _logger;

        public GetAuthStatusQueryHandler(
            ITokenStorage tokenStorage,
            IHttpContextAccessor httpContextAccessor,
            ILogger<GetAuthStatusQueryHandler> logger)
        {
            _tokenStorage = tokenStorage;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<ApiResponse<AuthStatusDto>> Handle(GetAuthStatusQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var currentSession = _httpContextAccessor.HttpContext?.GetCurrentSession();
                TokenData? tokens = null;
                if (currentSession != null)
                {
                    tokens = await _tokenStorage.LoadTokensAsync(currentSession.SessionId);
                }

                var status = new AuthStatusDto
                {
                    IsAuthenticated = currentSession?.IsActive == true,
                    Username = currentSession?.Username ?? tokens?.Username,
                    ExpiresAt = currentSession?.ExpiresAt,
                    IsExpired = currentSession?.IsExpired ?? true,
                    TimeToExpiry = currentSession?.IsActive == true ? currentSession.TimeToExpiry : null
                };

                if (tokens != null)
                {
                    status.TimeToExpiry = tokens.IsValid ? tokens.TimeToExpiry : status.TimeToExpiry;
                }

                return ApiResponse<AuthStatusDto>.SuccessResult(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting auth status");
                return ApiResponse<AuthStatusDto>.ErrorResult("Internal server error");
            }
        }
    }
}