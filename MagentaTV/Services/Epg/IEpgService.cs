using MagentaTV.Models;

namespace MagentaTV.Services.Epg;

public interface IEpgService
{
    Task<List<EpgItemDto>> GetEpgAsync(int channelId, DateTime? from = null, DateTime? to = null);
    string GenerateXmlTv(List<EpgItemDto> epg, int channelId);
}
