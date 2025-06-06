using MagentaTV.Models;
using MagentaTV.Services.Ffmpeg;
using MediatR;

namespace MagentaTV.Application.Commands;

public class ConvertVideoCommandHandler : IRequestHandler<ConvertVideoCommand, ApiResponse<string>>
{
    private readonly IFFmpegService _ffmpegService;
    private readonly ILogger<ConvertVideoCommandHandler> _logger;

    public ConvertVideoCommandHandler(IFFmpegService ffmpegService, ILogger<ConvertVideoCommandHandler> logger)
    {
        _ffmpegService = ffmpegService;
        _logger = logger;
    }

    public async Task<ApiResponse<string>> Handle(ConvertVideoCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var jobId = await _ffmpegService.QueueConversionAsync(request.InputUrl, request.OutputFile, cancellationToken);
            return ApiResponse<string>.SuccessResult(jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue FFmpeg job");
            return ApiResponse<string>.ErrorResult("Failed to queue FFmpeg job");
        }
    }
}
