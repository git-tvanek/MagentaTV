namespace MagentaTV.Services.Stream;

public class StreamService : IStreamService
{
    private readonly IMagenta _magenta;
    private readonly ILogger<StreamService> _logger;

    public StreamService(IMagenta magenta, ILogger<StreamService> logger)
    {
        _magenta = magenta;
        _logger = logger;
    }

    public Task<string?> GetStreamUrlAsync(int channelId)
    {
        return _magenta.GetStreamUrlAsync(channelId);
    }

    public Task<string?> GetCatchupStreamUrlAsync(long scheduleId)
    {
        return _magenta.GetCatchupStreamUrlAsync(scheduleId);
    }
}
