using MagentaTV.Services.Channels;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GeneratePlaylistQueryHandler : IRequestHandler<GeneratePlaylistQuery, string>
    {
    private readonly IChannelService _channelService;
        private readonly ILogger<GeneratePlaylistQueryHandler> _logger;

    public GeneratePlaylistQueryHandler(IChannelService channelService, ILogger<GeneratePlaylistQueryHandler> logger)
    {
        _channelService = channelService;
        _logger = logger;
    }

        public async Task<string> Handle(GeneratePlaylistQuery request, CancellationToken cancellationToken)
        {
            try
            {
                return await _channelService.GenerateM3UPlaylistAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate playlist");
                throw;
            }
        }
    }
}