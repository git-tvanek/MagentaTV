﻿using MagentaTV.Models;
using MagentaTV.Services.Stream;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GetStreamUrlQueryHandler : IRequestHandler<GetStreamUrlQuery, ApiResponse<StreamUrlDto>>
    {
        private readonly IStreamService _streamService;
        private readonly ILogger<GetStreamUrlQueryHandler> _logger;

        public GetStreamUrlQueryHandler(IStreamService streamService, ILogger<GetStreamUrlQueryHandler> logger)
        {
            _streamService = streamService;
            _logger = logger;
        }

        public async Task<ApiResponse<StreamUrlDto>> Handle(GetStreamUrlQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var url = await _streamService.GetStreamUrlAsync(request.ChannelId);
                if (string.IsNullOrEmpty(url))
                {
                    return ApiResponse<StreamUrlDto>.ErrorResult("Stream not found",
                        new List<string> { "Stream pro tento kanál nebyl nalezen" });
                }

                var streamData = new StreamUrlDto
                {
                    ChannelId = request.ChannelId,
                    StreamUrl = url,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    Type = "LIVE"
                };

                return ApiResponse<StreamUrlDto>.SuccessResult(streamData, "Stream URL získána");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get stream URL for channel {ChannelId}", request.ChannelId);
                return ApiResponse<StreamUrlDto>.ErrorResult("Failed to get stream URL");
            }
        }
    }
}