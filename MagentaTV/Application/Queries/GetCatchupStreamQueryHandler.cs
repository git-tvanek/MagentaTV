using MagentaTV.Models;
using MagentaTV.Services;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GetCatchupStreamQueryHandler : IRequestHandler<GetCatchupStreamQuery, ApiResponse<StreamUrlDto>>
    {
        private readonly IMagenta _magentaService;
        private readonly ILogger<GetCatchupStreamQueryHandler> _logger;

        public GetCatchupStreamQueryHandler(IMagenta magentaService, ILogger<GetCatchupStreamQueryHandler> logger)
        {
            _magentaService = magentaService;
            _logger = logger;
        }

        public async Task<ApiResponse<StreamUrlDto>> Handle(GetCatchupStreamQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var url = await _magentaService.GetCatchupStreamUrlAsync(request.ScheduleId);
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
