using MagentaTV.Models;

namespace MagentaTV.Services.Epg;

public class EpgService : IEpgService
{
    private readonly IMagenta _magenta;
    private readonly ILogger<EpgService> _logger;

    public EpgService(IMagenta magenta, ILogger<EpgService> logger)
    {
        _magenta = magenta;
        _logger = logger;
    }

    public Task<List<EpgItemDto>> GetEpgAsync(int channelId, DateTime? from = null, DateTime? to = null)
    {
        return _magenta.GetEpgAsync(channelId, from, to);
    }

    public string GenerateXmlTv(List<EpgItemDto> epg, int channelId)
    {
        return _magenta.GenerateXmlTv(epg, channelId);
    }
}
