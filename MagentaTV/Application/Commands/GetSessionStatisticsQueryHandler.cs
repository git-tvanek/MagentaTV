using MagentaTV.Models;
using MagentaTV.Services.Session;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GetSessionStatisticsQueryHandler : IRequestHandler<GetSessionStatisticsQuery, ApiResponse<SessionStatistics>>
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<GetSessionStatisticsQueryHandler> _logger;

        public GetSessionStatisticsQueryHandler(ISessionManager sessionManager, ILogger<GetSessionStatisticsQueryHandler> logger)
        {
            _sessionManager = sessionManager;
            _logger = logger;
        }

        public async Task<ApiResponse<SessionStatistics>> Handle(GetSessionStatisticsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var stats = await _sessionManager.GetStatisticsAsync();
                return ApiResponse<SessionStatistics>.SuccessResult(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get session statistics");
                return ApiResponse<SessionStatistics>.ErrorResult("Internal server error");
            }
        }
    }
}