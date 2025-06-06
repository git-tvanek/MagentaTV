namespace MagentaTV.Services.Stream;

public interface IStreamService
{
    Task<string?> GetStreamUrlAsync(int channelId);
    Task<string?> GetCatchupStreamUrlAsync(long scheduleId);
}
