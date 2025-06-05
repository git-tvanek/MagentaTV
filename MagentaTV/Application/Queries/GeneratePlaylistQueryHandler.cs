using MagentaTV.Services;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GeneratePlaylistQueryHandler : IRequestHandler<GeneratePlaylistQuery, string>
    {
        private readonly IMagenta _magentaService;
        private readonly ILogger<GeneratePlaylistQueryHandler> _logger;

        public GeneratePlaylistQueryHandler(IMagenta magentaService, ILogger<GeneratePlaylistQueryHandler> logger)
        {
            _magentaService = magentaService;
            _logger = logger;
        }

        public async Task<string> Handle(GeneratePlaylistQuery request, CancellationToken cancellationToken)
        {
            try
            {
                return await _magentaService.GenerateM3UPlaylistAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate playlist");
                throw;
            }
        }
    }
}