using MagentaTV.Models;

namespace MagentaTV.Services.Channels;

public interface IChannelService
{
    Task<List<ChannelDto>> GetChannelsAsync();
    Task<string> GenerateM3UPlaylistAsync();
}
