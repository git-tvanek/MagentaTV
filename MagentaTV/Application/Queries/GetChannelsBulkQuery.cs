using System.Collections.Generic;
using System.Linq;
using MagentaTV.Models;
using MagentaTV.Services.Channels;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MagentaTV.Application.Queries;

public class GetChannelsBulkQuery : IRequest<ApiResponse<List<ChannelDto>>>
{
    public IEnumerable<int> ChannelIds { get; set; } = Enumerable.Empty<int>();
}

public class GetChannelsBulkQueryHandler : IRequestHandler<GetChannelsBulkQuery, ApiResponse<List<ChannelDto>>>
{
    private readonly IChannelService _channelService;
    private readonly ILogger<GetChannelsBulkQueryHandler> _logger;

    public GetChannelsBulkQueryHandler(IChannelService channelService, ILogger<GetChannelsBulkQueryHandler> logger)
    {
        _channelService = channelService;
        _logger = logger;
    }

    public async Task<ApiResponse<List<ChannelDto>>> Handle(GetChannelsBulkQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var channels = await _channelService.GetChannelsAsync();
            var filtered = channels.Where(c => request.ChannelIds.Contains(c.ChannelId)).ToList();
            return ApiResponse<List<ChannelDto>>.SuccessResult(filtered, $"Found {filtered.Count} channels");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get channels");
            return ApiResponse<List<ChannelDto>>.ErrorResult("Failed to get channels");
        }
    }
}
