using MagentaTV.Models;
using MagentaTV.Services;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GetChannelsQuery : IRequest<ApiResponse<List<ChannelDto>>>
    {
        public bool ForceRefresh { get; set; } = false;
    }

    public class GetChannelsQueryHandler : IRequestHandler<GetChannelsQuery, ApiResponse<List<ChannelDto>>>
    {
        private readonly IMagenta _magentaService;
        private readonly ILogger<GetChannelsQueryHandler> _logger;

        public GetChannelsQueryHandler(IMagenta magentaService, ILogger<GetChannelsQueryHandler> logger)
        {
            _magentaService = magentaService;
            _logger = logger;
        }

        public async Task<ApiResponse<List<ChannelDto>>> Handle(GetChannelsQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var channels = await _magentaService.GetChannelsAsync();
                return ApiResponse<List<ChannelDto>>.SuccessResult(channels, $"Found {channels.Count} channels");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get channels");
                return ApiResponse<List<ChannelDto>>.ErrorResult("Failed to get channels");
            }
        }
    }
}