using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Exceptions;
using MagentaTV.Configuration;
using MagentaTV.Models;
using MagentaTV.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace MagentaTV.Services;

/// <summary>
/// Implementace streaming služby s FFmpeg
/// </summary>
public class Streaming : IStreaming, IDisposable
{
    private readonly StreamingOptions _options;
    private readonly ILogger<Streaming> _logger;
    private readonly IMemoryCache _cache;
    private readonly IMagenta _magentaService;

    // Active streams and recordings tracking
    private readonly ConcurrentDictionary<string, StreamContext> _activeStreams = new();
    private readonly ConcurrentDictionary<string, RecordingContext> _activeRecordings = new();

    // Port management
    private readonly object _portLock = new();
    private readonly HashSet<int> _usedPorts = new();

    public Streaming(
        IOptions<StreamingOptions> options,
        ILogger<Streaming> logger,
        IMemoryCache cache,
        IMagenta magentaService)
    {
        _options = options.Value;
        _logger = logger;
        _cache = cache;
        _magentaService = magentaService;

        // Validate configuration
        _options.Validate();

        // Configure FFmpeg
        ConfigureFFmpeg();

        _logger.LogInformation("StreamingService initialized with {MaxStreams} max streams",
            _options.MaxConcurrentStreams);
    }

    private void ConfigureFFmpeg()
    {
        try
        {
            GlobalFFOptions.Configure(new FFOptions
            {
                BinaryFolder = Path.GetDirectoryName(_options.FFmpegPath) ?? "/usr/bin",
                TemporaryFilesFolder = _options.TempDirectory,
                WorkingDirectory = _options.WorkingDirectory
            });

            _logger.LogInformation("FFmpeg configured: {FFmpegPath}", _options.FFmpegPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure FFmpeg");
            throw;
        }
    }

    public async Task<ProxyStreamDto> CreateProxyStreamAsync(
        string inputUrl, string quality, int channelId, StreamProxyOptions? options = null)
    {
        if (_activeStreams.Count >= _options.MaxConcurrentStreams)
        {
            throw new InvalidOperationException($"Maximum concurrent streams limit reached ({_options.MaxConcurrentStreams})");
        }

        if (!_options.QualityPresets.ContainsKey(quality))
        {
            throw new ArgumentException($"Unknown quality preset: {quality}");
        }

        var streamId = GenerateStreamId();
        var outputPort = GetAvailablePort();
        var preset = _options.QualityPresets[quality];
        options ??= new StreamProxyOptions();

        try
        {
            var outputUrl = $"http://localhost:{outputPort}/stream.m3u8";
            var hlsPath = Path.Combine(_options.WorkingDirectory, streamId);
            Directory.CreateDirectory(hlsPath);

            var playlistPath = Path.Combine(hlsPath, "stream.m3u8");

            var conversion = FFMpegArguments
                .FromUrlInput(new Uri(inputUrl), options => options
                    .WithCustomArgument("-fflags +genpts")
                    .WithCustomArgument("-avoid_negative_ts make_zero"))
                .OutputToFile(playlistPath, true, options => options
                    .WithVideoCodec(GetVideoCodec())
                    .WithAudioCodec(AudioCodec.Aac)
                    .WithVideoBitrate(preset.VideoBitrate)
                    .WithAudioBitrate(preset.AudioBitrate)
                    .Resize(preset.Width, preset.Height)
                    .WithCustomArgument("-f hls")
                    .WithCustomArgument($"-hls_time {options.SegmentDuration}")
                    .WithCustomArgument($"-hls_list_size {options.PlaylistSize}")
                    .WithCustomArgument("-hls_flags delete_segments")
                    .WithCustomArgument("-hls_allow_cache 0")
                    .WithCustomArgument("-hls_segment_filename")
                    .WithCustomArgument(Path.Combine(hlsPath, "segment_%03d.ts")));

            var cancellationTokenSource = new CancellationTokenSource();
            var task = conversion.ProcessAsynchronously(true, cancellationTokenSource.Token);

            var streamContext = new StreamContext
            {
                StreamId = streamId,
                ChannelId = channelId,
                Quality = quality,
                InputUrl = inputUrl,
                OutputUrl = outputUrl,
                PlaylistPath = playlistPath,
                HlsDirectory = hlsPath,
                Port = outputPort,
                Process = task,
                CancellationTokenSource = cancellationTokenSource,
                StartedAt = DateTime.UtcNow,
                Stats = new StreamStats { LastActivity = DateTime.UtcNow }
            };

            _activeStreams[streamId] = streamContext;
            ReleasePort(outputPort, false); // Mark as used

            // Start monitoring task
            _ = Task.Run(() => MonitorStream(streamContext));

            var proxyStream = new ProxyStreamDto
            {
                StreamId = streamId,
                ProxyUrl = outputUrl,
                ChannelId = channelId,
                Quality = quality,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(_options.StreamExpiration),
                Status = StreamStatus.Initializing,
                Stats = streamContext.Stats
            };

            _logger.LogInformation("Created proxy stream {StreamId} for channel {ChannelId} with quality {Quality}",
                streamId, channelId, quality);

            return proxyStream;
        }
        catch (Exception ex)
        {
            ReleasePort(outputPort);
            _logger.LogError(ex, "Failed to create proxy stream for channel {ChannelId}", channelId);
            throw;
        }
    }

    public async Task StopProxyStreamAsync(string streamId)
    {
        if (!_activeStreams.TryRemove(streamId, out var context))
        {
            throw new KeyNotFoundException($"Stream {streamId} not found");
        }

        try
        {
            context.CancellationTokenSource.Cancel();

            // Wait for process to stop (with timeout)
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var completedTask = await Task.WhenAny(context.Process, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _logger.LogWarning("Stream {StreamId} did not stop gracefully, forcing termination", streamId);
            }

            // Cleanup
            ReleasePort(context.Port);
            CleanupStreamDirectory(context.HlsDirectory);

            _logger.LogInformation("Stopped proxy stream {StreamId}", streamId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping stream {StreamId}", streamId);
            throw;
        }
        finally
        {
            context.Dispose();
        }
    }

    public async Task<List<ActiveStreamDto>> GetActiveStreamsAsync()
    {
        var activeStreams = new List<ActiveStreamDto>();

        foreach (var (streamId, context) in _activeStreams)
        {
            var channelName = await GetChannelNameAsync(context.ChannelId);

            activeStreams.Add(new ActiveStreamDto
            {
                StreamId = streamId,
                ChannelId = context.ChannelId,
                ChannelName = channelName,
                Quality = context.Quality,
                StartedAt = context.StartedAt,
                Status = GetStreamStatus(context),
                ViewerCount = 0, // TODO: Implement viewer tracking
                Stats = context.Stats
            });
        }

        return activeStreams;
    }

    public async Task<RecordingDto> ScheduleRecordingAsync(long scheduleId, RecordingRequest request)
    {
        if (_activeRecordings.Count >= _options.MaxConcurrentRecordings)
        {
            throw new InvalidOperationException($"Maximum concurrent recordings limit reached ({_options.MaxConcurrentRecordings})");
        }

        var recordingId = GenerateRecordingId();

        try
        {
            // Get stream URL from Magenta service
            var streamUrl = await _magentaService.GetCatchupStreamUrlAsync(scheduleId);
            if (string.IsNullOrEmpty(streamUrl))
            {
                throw new InvalidOperationException($"Failed to get stream URL for schedule {scheduleId}");
            }

            return await StartRecordingAsync(recordingId, scheduleId, streamUrl, request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule recording for schedule {ScheduleId}", scheduleId);
            throw;
        }
    }

    public async Task<RecordingDto> StartImmediateRecordingAsync(string streamUrl, RecordingRequest request)
    {
        var recordingId = GenerateRecordingId();
        return await StartRecordingAsync(recordingId, 0, streamUrl, request);
    }

    private async Task<RecordingDto> StartRecordingAsync(string recordingId, long scheduleId, string streamUrl, RecordingRequest request)
    {
        var duration = request.Duration ?? (request.EndTime - request.StartTime) ?? TimeSpan.FromHours(2);
        var outputFileName = $"{recordingId}.{request.Format}";
        var outputPath = Path.Combine(_options.RecordingsDirectory, outputFileName);
        var preset = _options.QualityPresets[request.Quality];

        try
        {
            var conversion = FFMpegArguments
                .FromUrlInput(new Uri(streamUrl))
                .OutputToFile(outputPath, true, options => options
                    .WithDuration(duration)
                    .WithVideoCodec(VideoCodec.Copy) // Stream copy for better performance
                    .WithAudioCodec(AudioCodec.Copy)
                    .WithCustomArgument("-avoid_negative_ts make_zero"));

            var cancellationTokenSource = new CancellationTokenSource();
            var progress = new Progress<double>(p => UpdateRecordingProgress(recordingId, p));

            var task = conversion
                .NotifyOnProgress(progress, duration)
                .ProcessAsynchronously(true, cancellationTokenSource.Token);

            var recordingContext = new RecordingContext
            {
                RecordingId = recordingId,
                ScheduleId = scheduleId,
                StreamUrl = streamUrl,
                OutputPath = outputPath,
                StartTime = DateTime.UtcNow,
                Duration = duration,
                Process = task,
                CancellationTokenSource = cancellationTokenSource,
                Request = request
            };

            _activeRecordings[recordingId] = recordingContext;

            var recording = new RecordingDto
            {
                RecordingId = recordingId,
                ScheduleId = scheduleId,
                ProgramTitle = request.Title ?? $"Recording {scheduleId}",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.Add(duration),
                Status = RecordingStatus.Starting,
                OutputPath = outputPath,
                RelativeUrl = $"/api/streaming/recordings/{outputFileName}",
                Duration = duration,
                Progress = 0
            };

            // Start monitoring task
            _ = Task.Run(() => MonitorRecording(recordingContext));

            _logger.LogInformation("Started recording {RecordingId} for schedule {ScheduleId}", recordingId, scheduleId);

            return recording;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording {RecordingId}", recordingId);
            throw;
        }
    }

    public async Task<ThumbnailDto> GenerateThumbnailAsync(long scheduleId, TimeSpan timestamp)
    {
        var streamUrl = await _magentaService.GetCatchupStreamUrlAsync(scheduleId);
        if (string.IsNullOrEmpty(streamUrl))
        {
            throw new InvalidOperationException($"Failed to get stream URL for schedule {scheduleId}");
        }

        return await GenerateThumbnailFromUrlAsync(streamUrl, timestamp, scheduleId);
    }

    public async Task<ThumbnailDto> GenerateThumbnailFromUrlAsync(string streamUrl, TimeSpan timestamp)
    {
        return await GenerateThumbnailFromUrlAsync(streamUrl, timestamp, 0);
    }

    private async Task<ThumbnailDto> GenerateThumbnailFromUrlAsync(string streamUrl, TimeSpan timestamp, long scheduleId)
    {
        var thumbnailId = GenerateThumbnailId();
        var thumbnailFileName = $"{thumbnailId}.jpg";
        var thumbnailPath = Path.Combine(_options.ThumbnailsDirectory, thumbnailFileName);

        try
        {
            await FFMpegArguments
                .FromUrlInput(new Uri(streamUrl), options => options
                    .Seek(timestamp))
                .OutputToFile(thumbnailPath, true, options => options
                    .WithVideoFrames(1)
                    .WithVideoCodec(VideoCodec.Mjpeg)
                    .WithCustomArgument("-q:v 2")
                    .Resize(320, 240))
                .ProcessAsynchronously();

            var fileInfo = new FileInfo(thumbnailPath);

            var thumbnail = new ThumbnailDto
            {
                ThumbnailId = thumbnailId,
                ScheduleId = scheduleId,
                ThumbnailUrl = $"/api/streaming/thumbnails/{thumbnailFileName}",
                LocalPath = thumbnailPath,
                Timestamp = timestamp,
                CreatedAt = DateTime.UtcNow,
                Width = 320,
                Height = 240,
                FileSize = fileInfo.Length
            };

            _logger.LogInformation("Generated thumbnail {ThumbnailId} for schedule {ScheduleId}", thumbnailId, scheduleId);

            return thumbnail;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate thumbnail for schedule {ScheduleId}", scheduleId);
            throw;
        }
    }

    public async Task<StreamingHealthDto> GetHealthAsync()
    {
        var health = new StreamingHealthDto
        {
            IsHealthy = true,
            ActiveStreamsCount = _activeStreams.Count,
            ActiveRecordingsCount = _activeRecordings.Count,
            ResourceUsage = await GetResourceUsageAsync(),
            Warnings = new List<string>()
        };

        try
        {
            // Check FFmpeg version
            var ffmpegInfo = await FFProbe.AnalyseAsync("https://www.w3schools.com/html/mov_bbb.mp4");
            health.FFmpegVersion = "Available";
            health.HardwareAccelerationAvailable = _options.EnableHardwareAcceleration;
        }
        catch (Exception ex)
        {
            health.IsHealthy = false;
            health.Warnings.Add($"FFmpeg not available: {ex.Message}");
        }

        // Check resource usage
        if (health.ResourceUsage.CpuUsagePercent > _options.MaxCpuUsagePercent)
        {
            health.Warnings.Add($"High CPU usage: {health.ResourceUsage.CpuUsagePercent:F1}%");
        }

        if (health.ResourceUsage.AvailableDiskBytes < 1024 * 1024 * 1024) // Less than 1GB
        {
            health.Warnings.Add($"Low disk space: {health.ResourceUsage.AvailableDiskBytes / (1024 * 1024 * 1024)}GB available");
        }

        if (health.Warnings.Any())
        {
            health.IsHealthy = false;
        }

        return health;
    }

    public async Task<ResourceUsage> GetResourceUsageAsync()
    {
        var resourceUsage = new ResourceUsage();

        try
        {
            // Get disk usage
            var driveInfo = new DriveInfo(Path.GetPathRoot(_options.WorkingDirectory)!);
            resourceUsage.AvailableDiskBytes = driveInfo.AvailableFreeSpace;
            resourceUsage.DiskUsageBytes = driveInfo.TotalSize - driveInfo.AvailableFreeSpace;

            // Get memory usage (approximate)
            resourceUsage.MemoryUsageBytes = GC.GetTotalMemory(false);

            // CPU usage would need performance counters (simplified here)
            resourceUsage.CpuUsagePercent = 0; // TODO: Implement actual CPU monitoring
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get resource usage");
        }

        return resourceUsage;
    }

    // Helper methods...
    private VideoCodec GetVideoCodec()
    {
        return _options.EnableHardwareAcceleration && _options.HardwareAccelerator == "nvenc"
            ? VideoCodec.LibX264 // Would be H264_Nvenc if available
            : VideoCodec.LibX264;
    }

    private int GetAvailablePort()
    {
        lock (_portLock)
        {
            for (int port = _options.BaseStreamingPort; port < _options.BaseStreamingPort + 1000; port++)
            {
                if (!_usedPorts.Contains(port) && IsPortAvailable(port))
                {
                    _usedPorts.Add(port);
                    return port;
                }
            }
        }
        throw new InvalidOperationException("No available ports");
    }

    private void ReleasePort(int port, bool remove = true)
    {
        lock (_portLock)
        {
            if (remove)
                _usedPorts.Remove(port);
        }
    }

    private static bool IsPortAvailable(int port)
    {
        var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
        var tcpListeners = ipGlobalProperties.GetActiveTcpListeners();
        return !tcpListeners.Any(l => l.Port == port);
    }

    private string GenerateStreamId() => $"stream_{Guid.NewGuid():N}"[..16];
    private string GenerateRecordingId() => $"rec_{Guid.NewGuid():N}"[..16];
    private string GenerateThumbnailId() => $"thumb_{Guid.NewGuid():N}"[..16];

    // Additional helper methods and cleanup logic...

    public void Dispose()
    {
        foreach (var stream in _activeStreams.Values)
        {
            stream.Dispose();
        }
        foreach (var recording in _activeRecordings.Values)
        {
            recording.Dispose();
        }
    }
}

// Context classes for tracking active operations
internal class StreamContext : IDisposable
{
    public string StreamId { get; set; } = "";
    public int ChannelId { get; set; }
    public string Quality { get; set; } = "";
    public string InputUrl { get; set; } = "";
    public string OutputUrl { get; set; } = "";
    public string PlaylistPath { get; set; } = "";
    public string HlsDirectory { get; set; } = "";
    public int Port { get; set; }
    public Task Process { get; set; } = Task.CompletedTask;
    public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    public DateTime StartedAt { get; set; }
    public StreamStats Stats { get; set; } = new();

    public void Dispose()
    {
        CancellationTokenSource?.Dispose();
    }
}

internal class RecordingContext : IDisposable
{
    public string RecordingId { get; set; } = "";
    public long ScheduleId { get; set; }
    public string StreamUrl { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public Task Process { get; set; } = Task.CompletedTask;
    public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    public RecordingRequest Request { get; set; } = new();
    public double Progress { get; set; }

    public void Dispose()
    {
        CancellationTokenSource?.Dispose();
    }
}