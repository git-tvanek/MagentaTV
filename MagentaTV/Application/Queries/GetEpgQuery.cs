using MagentaTV.Models;
using MagentaTV.Services.Epg;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GetEpgQuery : IRequest<ApiResponse<List<EpgItemDto>>>
    {
        public int ChannelId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public bool ForceRefresh { get; set; } = false;
    }

    public class GetEpgQueryHandler : IRequestHandler<GetEpgQuery, ApiResponse<List<EpgItemDto>>>
    {
        private readonly IEpgService _epgService;
        private readonly ILogger<GetEpgQueryHandler> _logger;

        public GetEpgQueryHandler(IEpgService epgService, ILogger<GetEpgQueryHandler> logger)
        {
            _epgService = epgService;
            _logger = logger;
        }

        public async Task<ApiResponse<List<EpgItemDto>>> Handle(GetEpgQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var epg = await _epgService.GetEpgAsync(request.ChannelId, request.From, request.To);
                return ApiResponse<List<EpgItemDto>>.SuccessResult(epg, $"Found {epg.Count} EPG items");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get EPG for channel {ChannelId}", request.ChannelId);
                return ApiResponse<List<EpgItemDto>>.ErrorResult("Failed to get EPG");
            }
        }
    }
}
