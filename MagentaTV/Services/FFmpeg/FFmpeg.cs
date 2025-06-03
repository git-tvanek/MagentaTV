using MagentaTV.Configuration;
using MagentaTV.Services.FFmpeg;
using System.Diagnostics;

public class FFmpeg : IFFmpeg
{
    private readonly ILogger<FFmpeg> _logger;
    private readonly FFmpegOptions _options;

    public async Task<string> CreateProxyStreamAsync(string inputUrl, string quality, int channelId)
    {
        var outputPort = GetAvailablePort();
        var ffmpegArgs = BuildProxyStreamArgs(inputUrl, quality, outputPort);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _options.FFmpegPath,
                Arguments = ffmpegArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.Start();

        // Store process reference for cleanup
        _activeStreams[channelId] = process;

        return $"http://localhost:{outputPort}/stream.m3u8";
    }

    public async Task<string> RecordStreamAsync(string inputUrl, string outputPath, TimeSpan duration)
    {
        var args = $"-i \"{inputUrl}\" -t {duration.TotalSeconds} -c copy \"{outputPath}\"";

        var result = await RunFFmpegAsync(args);

        if (result.ExitCode == 0)
        {
            _logger.LogInformation("Recording completed: {OutputPath}", outputPath);
            return outputPath;
        }

        throw new InvalidOperationException($"Recording failed: {result.Error}");
    }
}