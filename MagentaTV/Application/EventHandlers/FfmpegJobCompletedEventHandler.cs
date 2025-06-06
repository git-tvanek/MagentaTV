using MagentaTV.Application.Events;
using MediatR;

namespace MagentaTV.Application.EventHandlers;

public class FfmpegJobCompletedEventHandler : INotificationHandler<FfmpegJobCompletedEvent>
{
    private readonly ILogger<FfmpegJobCompletedEventHandler> _logger;

    public FfmpegJobCompletedEventHandler(ILogger<FfmpegJobCompletedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(FfmpegJobCompletedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.Success)
        {
            _logger.LogInformation("FFmpeg job {JobId} completed. Output: {File}", notification.JobId, notification.OutputFile);
        }
        else
        {
            _logger.LogWarning("FFmpeg job {JobId} failed: {Error}", notification.JobId, notification.ErrorMessage);
        }

        return Task.CompletedTask;
    }
}
