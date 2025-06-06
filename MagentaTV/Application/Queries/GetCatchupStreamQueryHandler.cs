using MagentaTV.Models;
using MagentaTV.Services.Stream;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GetCatchupStreamQueryHandler : IRequestHandler<GetCatchupStreamQuery, ApiResponse<StreamUrlDto>>
    {
        private readonly IStreamService _streamService;
        private readonly ILogger<GetCatchupStreamQueryHandler> _logger;

        public GetCatchupStreamQueryHandler(IStreamService streamService, ILogger<GetCatchupStreamQueryHandler> logger)
        {
            _streamService = streamService;
            _logger = logger;
        }

        public async Task<ApiResponse<StreamUrlDto>> Handle(GetCatchupStreamQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var url = await _streamService.GetCatchupStreamUrlAsync(request.ScheduleId);
                if (string.IsNullOrEmpty(url))
                {
                    return ApiResponse<StreamUrlDto>.ErrorResult("Catchup stream not found",
                        new List<string> { "Catchup stream pro tento pořad nebyl nalezen" });
                }

                var streamData = new StreamUrlDto
                {
                    ScheduleId = request.ScheduleId,
                    StreamUrl = url,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    Type = "CATCHUP"
                };

                return ApiResponse<StreamUrlDto>.SuccessResult(streamData, "Catchup stream URL získána");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get catchup stream URL for schedule {ScheduleId}", request.ScheduleId);
                return ApiResponse<StreamUrlDto>.ErrorResult("Failed to get catchup stream URL");
            }
        }
    }
}
