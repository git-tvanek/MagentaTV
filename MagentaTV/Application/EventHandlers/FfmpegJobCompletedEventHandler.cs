using MagentaTV.Application.Events;
using MagentaTV.Hubs;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace MagentaTV.Application.EventHandlers;

public class FfmpegJobCompletedEventHandler : INotificationHandler<FfmpegJobCompletedEvent>
{
    private readonly ILogger<FfmpegJobCompletedEventHandler> _logger;
    private readonly IHubContext<NotificationHub> _hubContext;

    public FfmpegJobCompletedEventHandler(
        IHubContext<NotificationHub> hubContext,
        ILogger<FfmpegJobCompletedEventHandler> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Handle(FfmpegJobCompletedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.Success)
        {
            _logger.LogInformation("FFmpeg job {JobId} completed. Output: {File}", notification.JobId, notification.OutputFile);
        }
        else
        {
            _logger.LogWarning("FFmpeg job {JobId} failed: {Error}", notification.JobId, notification.ErrorMessage);
        }

        await _hubContext.Clients.All.SendAsync("FfmpegJobCompleted", notification, cancellationToken);
    }
}
