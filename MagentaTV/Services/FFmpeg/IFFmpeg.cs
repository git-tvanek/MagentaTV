namespace MagentaTV.Services.FFmpeg
{
    public interface IFFmpeg
    {
        Task<string> CreateProxyStreamAsync(string inputUrl, string quality, int channelId);
        Task<string> RecordStreamAsync(string inputUrl, string outputPath, TimeSpan duration);
        Task<string> ExtractThumbnailAsync(string inputUrl, string outputPath, TimeSpan timestamp);
        Task<StreamHealthInfo> CheckStreamHealthAsync(string streamUrl);
        Task<string> TranscodeAsync(string inputPath, string outputPath, TranscodeOptions options);
    }
}
