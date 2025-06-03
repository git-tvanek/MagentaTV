using MagentaTV.Configuration;
using MagentaTV.Models;
namespace MagentaTV.Services;

/// <summary>
/// Interface pro FFmpeg streaming služby
/// </summary>
public interface IStreaming
{
    // Proxy Streaming
    Task<ProxyStreamDto> CreateProxyStreamAsync(string inputUrl, string quality,
        int channelId, StreamProxyOptions? options = null);
    Task StopProxyStreamAsync(string streamId);
    Task<List<ActiveStreamDto>> GetActiveStreamsAsync();

    // Recording
    Task<RecordingDto> ScheduleRecordingAsync(long scheduleId, RecordingRequest request);
    Task<RecordingDto> StartImmediateRecordingAsync(string streamUrl, RecordingRequest request);
    Task StopRecordingAsync(string recordingId);
    Task<List<RecordingDto>> GetActiveRecordingsAsync();
    Task<List<RecordingDto>> GetCompletedRecordingsAsync();

    // Media Processing
    Task<ThumbnailDto> GenerateThumbnailAsync(long scheduleId, TimeSpan timestamp);
    Task<ThumbnailDto> GenerateThumbnailFromUrlAsync(string streamUrl, TimeSpan timestamp);

    // Monitoring
    Task<StreamingHealthDto> GetHealthAsync();
    Task<bool> CheckStreamHealthAsync(string streamUrl);

    // Management
    Task CleanupExpiredStreamsAsync();
    Task CleanupOldRecordingsAsync();
    Task<ResourceUsage> GetResourceUsageAsync();
}