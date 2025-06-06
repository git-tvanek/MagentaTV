using MagentaTV.Models;
using MagentaTV.Services.Background.Core;
using MagentaTV.Services.Background;
using MagentaTV.Services.Channels;
using MagentaTV.Services.Epg;
using MediatR;

namespace MagentaTV.Application.Commands
{
    public class QueueBackgroundWorkCommandHandler : IRequestHandler<QueueBackgroundWorkCommand, ApiResponse<string>>
    {
        private readonly IBackgroundServiceManager _backgroundManager;
        private readonly ILogger<QueueBackgroundWorkCommandHandler> _logger;

        public QueueBackgroundWorkCommandHandler(
            IBackgroundServiceManager backgroundManager,
            ILogger<QueueBackgroundWorkCommandHandler> logger)
        {
            _backgroundManager = backgroundManager;
            _logger = logger;
        }

        public async Task<ApiResponse<string>> Handle(QueueBackgroundWorkCommand request, CancellationToken cancellationToken)
        {
            var workItem = new BackgroundWorkItem
            {
                Name = request.Name,
                Type = request.WorkType,
                Priority = request.Priority,
                Parameters = request.Parameters,
                WorkItem = CreateWorkItemFunction(request.WorkType, request.Parameters)
            };

            await _backgroundManager.QueueWorkItemAsync(workItem);

            return ApiResponse<string>.SuccessResult($"Work item {workItem.Id} queued successfully");
        }

        private Func<IServiceProvider, CancellationToken, Task> CreateWorkItemFunction(string workType, Dictionary<string, object> parameters)
        {
            return workType switch
            {
                "CACHE_REFRESH" => async (provider, ct) =>
                {
                    var channelService = provider.GetRequiredService<IChannelService>();
                    await channelService.GetChannelsAsync();
                }
                ,
                "EPG_PRELOAD" => async (provider, ct) =>
                {
                    var epgService = provider.GetRequiredService<IEpgService>();
                    var channelId = (int)parameters["channelId"];
                    await epgService.GetEpgAsync(channelId);
                }
                ,
                _ => (provider, ct) => Task.CompletedTask
            };
        }
    }
}
