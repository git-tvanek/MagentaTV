using MagentaTV.Services;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GenerateEpgXmlQueryHandler : IRequestHandler<GenerateEpgXmlQuery, string>
    {
        private readonly IMagenta _magentaService;
        private readonly ILogger<GenerateEpgXmlQueryHandler> _logger;

        public GenerateEpgXmlQueryHandler(IMagenta magentaService, ILogger<GenerateEpgXmlQueryHandler> logger)
        {
            _magentaService = magentaService;
            _logger = logger;
        }

        public async Task<string> Handle(GenerateEpgXmlQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var epg = await _magentaService.GetEpgAsync(request.ChannelId);
                return _magentaService.GenerateXmlTv(epg, request.ChannelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate EPG XML for channel {ChannelId}", request.ChannelId);
                throw;
            }
        }
    }
}