using MagentaTV.Models;

namespace MagentaTV.Services;

public interface IMagenta
{
    Task<bool> LoginAsync(string username, string password);
    Task<List<ChannelDto>> GetChannelsAsync();
    Task<List<EpgItemDto>> GetEpgAsync(int channelId, DateTime? from = null, DateTime? to = null);
    Task<string?> GetStreamUrlAsync(int channelId);
    Task<string?> GetCatchupStreamUrlAsync(long scheduleId);
    Task<string> GenerateM3UPlaylistAsync();
    string GenerateXmlTv(List<EpgItemDto> epg, int channelId);
}