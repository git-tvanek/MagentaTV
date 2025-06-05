using MagentaTV.Models;
using MagentaTV.Services.TokenStorage;
using MagentaTV.Extensions;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class PingQueryHandler : IRequestHandler<PingQuery, ApiResponse<PingResultDto>>
    {
        private readonly ITokenStorage _tokenStorage;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<PingQueryHandler> _logger;

        public PingQueryHandler(
            ITokenStorage tokenStorage,
            IHttpContextAccessor httpContextAccessor,
            ILogger<PingQueryHandler> logger)
        {
            _tokenStorage = tokenStorage;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<ApiResponse<PingResultDto>> Handle(PingQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var currentSession = _httpContextAccessor.HttpContext?.GetCurrentSession();
                var hasValidTokens = await _tokenStorage.HasValidTokensAsync();
                var tokens = await _tokenStorage.LoadTokensAsync();

                var result = new PingResultDto
                {
                    Timestamp = DateTime.UtcNow,
                    Status = "OK",
                    HasValidTokens = hasValidTokens && currentSession?.IsActive == true,
                    Username = currentSession?.Username ?? tokens?.Username,
                    TokenExpiresAt = tokens?.ExpiresAt
                };

                return ApiResponse<PingResultDto>.SuccessResult(result, "API je dostupná");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ping error");
                return ApiResponse<PingResultDto>.ErrorResult("Internal server error");
            }
        }
    }
}