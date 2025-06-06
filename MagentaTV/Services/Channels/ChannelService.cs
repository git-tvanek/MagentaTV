using MagentaTV.Models;

namespace MagentaTV.Services.Channels;

public class ChannelService : IChannelService
{
    private readonly IMagenta _magenta;
    private readonly ILogger<ChannelService> _logger;

    public ChannelService(IMagenta magenta, ILogger<ChannelService> logger)
    {
        _magenta = magenta;
        _logger = logger;
    }

    public Task<List<ChannelDto>> GetChannelsAsync()
    {
        return _magenta.GetChannelsAsync();
    }

    public Task<string> GenerateM3UPlaylistAsync()
    {
        return _magenta.GenerateM3UPlaylistAsync();
    }
}
