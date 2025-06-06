using MagentaTV.Services.Epg;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GenerateEpgXmlQueryHandler : IRequestHandler<GenerateEpgXmlQuery, string>
    {
        private readonly IEpgService _epgService;
        private readonly ILogger<GenerateEpgXmlQueryHandler> _logger;

        public GenerateEpgXmlQueryHandler(IEpgService epgService, ILogger<GenerateEpgXmlQueryHandler> logger)
        {
            _epgService = epgService;
            _logger = logger;
        }

        public async Task<string> Handle(GenerateEpgXmlQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var epg = await _epgService.GetEpgAsync(request.ChannelId);
                return _epgService.GenerateXmlTv(epg, request.ChannelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate EPG XML for channel {ChannelId}", request.ChannelId);
                throw;
            }
        }
    }
}