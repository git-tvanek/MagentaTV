namespace MagentaTV.Services.Ffmpeg;

public interface IFFmpegService
{
    Task<string> QueueConversionAsync(string inputUrl, string outputFile, CancellationToken cancellationToken);
    FfmpegJobStatus? GetJobStatus(string jobId);
}
