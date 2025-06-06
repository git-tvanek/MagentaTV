using MagentaTV.Models;
using MagentaTV.Services.Ffmpeg;
using MediatR;

namespace MagentaTV.Application.Queries;

public class GetFfmpegJobStatusQuery : IRequest<ApiResponse<FfmpegJobStatus>>
{
    public string JobId { get; set; } = string.Empty;
}
