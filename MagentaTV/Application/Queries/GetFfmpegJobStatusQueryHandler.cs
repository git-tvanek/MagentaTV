using MagentaTV.Models;
using MagentaTV.Services.Ffmpeg;
using MediatR;

namespace MagentaTV.Application.Queries;

public class GetFfmpegJobStatusQueryHandler : IRequestHandler<GetFfmpegJobStatusQuery, ApiResponse<FfmpegJobStatus>>
{
    private readonly IFFmpegService _ffmpegService;

    public GetFfmpegJobStatusQueryHandler(IFFmpegService ffmpegService)
    {
        _ffmpegService = ffmpegService;
    }

    public Task<ApiResponse<FfmpegJobStatus>> Handle(GetFfmpegJobStatusQuery request, CancellationToken cancellationToken)
    {
        var status = _ffmpegService.GetJobStatus(request.JobId);
        if (status == null)
        {
            return Task.FromResult(ApiResponse<FfmpegJobStatus>.ErrorResult("Job not found"));
        }
        return Task.FromResult(ApiResponse<FfmpegJobStatus>.SuccessResult(status));
    }
}
