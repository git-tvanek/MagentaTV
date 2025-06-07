using System;
using System.Collections.Generic;
using System.Linq;
using MagentaTV.Models;
using MagentaTV.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MagentaTV.Application.Queries;

public class GetBulkEpgQueryHandler : IRequestHandler<GetBulkEpgQuery, ApiResponse<Dictionary<int, List<EpgItemDto>>>>
{
    private readonly IMagenta _magenta;
    private readonly ILogger<GetBulkEpgQueryHandler> _logger;

    public GetBulkEpgQueryHandler(IMagenta magenta, ILogger<GetBulkEpgQueryHandler> logger)
    {
        _magenta = magenta;
        _logger = logger;
    }

    public async Task<ApiResponse<Dictionary<int, List<EpgItemDto>>>> Handle(GetBulkEpgQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var tasks = request.ChannelIds.Select(async id =>
            {
                var epg = await _magenta.GetEpgAsync(id, request.From, request.To);
                return (id, epg);
            });

            var results = await Task.WhenAll(tasks);
            var dict = results.ToDictionary(r => r.id, r => r.epg);

            return ApiResponse<Dictionary<int, List<EpgItemDto>>>.SuccessResult(dict,
                $"Retrieved EPG for {dict.Count} channels");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get bulk EPG");
            return ApiResponse<Dictionary<int, List<EpgItemDto>>>.ErrorResult("Failed to get EPG");
        }
    }
}
