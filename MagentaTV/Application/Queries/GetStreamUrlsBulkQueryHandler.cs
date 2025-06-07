using System;
using System.Collections.Generic;
using System.Linq;
using MagentaTV.Models;
using MagentaTV.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MagentaTV.Application.Queries;

public class GetStreamUrlsBulkQueryHandler : IRequestHandler<GetStreamUrlsBulkQuery, ApiResponse<Dictionary<int, string?>>>
{
    private readonly IMagenta _magenta;
    private readonly ILogger<GetStreamUrlsBulkQueryHandler> _logger;

    public GetStreamUrlsBulkQueryHandler(IMagenta magenta, ILogger<GetStreamUrlsBulkQueryHandler> logger)
    {
        _magenta = magenta;
        _logger = logger;
    }

    public async Task<ApiResponse<Dictionary<int, string?>>> Handle(GetStreamUrlsBulkQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var tasks = request.ChannelIds.Select(async id =>
            {
                var url = await _magenta.GetStreamUrlAsync(id);
                return (id, url);
            });

            var results = await Task.WhenAll(tasks);
            var dict = results.ToDictionary(r => r.id, r => r.url);

            return ApiResponse<Dictionary<int, string?>>.SuccessResult(dict,
                $"Retrieved streams for {dict.Count} channels");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get bulk stream URLs");
            return ApiResponse<Dictionary<int, string?>>.ErrorResult("Failed to get stream URLs");
        }
    }
}
