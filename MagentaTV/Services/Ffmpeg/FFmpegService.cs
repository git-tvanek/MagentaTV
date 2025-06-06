using System.Collections.Concurrent;
using FFMpegCore;
using MediatR;
using MagentaTV.Application.Events;
using MagentaTV.Services.Background;
using MagentaTV.Services.Background.Core;
using MagentaTV.Extensions;

namespace MagentaTV.Services.Ffmpeg;

public class FFmpegService : IFFmpegService
{
    private readonly IBackgroundServiceManager _backgroundManager;
    private readonly IMediator _mediator;
    private readonly ILogger<FFmpegService> _logger;
    private readonly ConcurrentDictionary<string, FfmpegJobStatus> _jobs = new();

    public FFmpegService(IBackgroundServiceManager backgroundManager, IMediator mediator, ILogger<FFmpegService> logger)
    {
        _backgroundManager = backgroundManager;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<string> QueueConversionAsync(string inputUrl, string outputFile, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString();
        var status = new FfmpegJobStatus
        {
            JobId = jobId,
            OutputFile = outputFile
        };

        _jobs[jobId] = status;

        var workItem = BackgroundServiceExtensions.CreateWorkItem(
            name: $"ffmpeg-{jobId}",
            type: "ffmpeg_convert",
            workItem: async (sp, ct) =>
            {
                try
                {
                    await FFMpegArguments
                        .FromUrlInput(new Uri(inputUrl))
                        .OutputToFile(outputFile, overwrite: true)
                        .ProcessAsynchronously(false, ct);
                    status.IsSuccess = true;
                }
                catch (Exception ex)
                {
                    status.IsSuccess = false;
                    status.ErrorMessage = ex.Message;
                    _logger.LogError(ex, "FFmpeg job {JobId} failed", jobId);
                }
                finally
                {
                    status.IsCompleted = true;
                    await _mediator.Publish(new FfmpegJobCompletedEvent
                    {
                        JobId = jobId,
                        OutputFile = outputFile,
                        Success = status.IsSuccess,
                        ErrorMessage = status.ErrorMessage
                    }, ct);
                }
            });

        await _backgroundManager.QueueWorkItemAsync(workItem);
        return jobId;
    }

    public FfmpegJobStatus? GetJobStatus(string jobId)
    {
        _jobs.TryGetValue(jobId, out var status);
        return status;
    }
}
