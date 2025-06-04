using System.ComponentModel.DataAnnotations;

namespace MagentaTV.Configuration;

/// <summary>
/// Konfigurace pro FFmpeg a streaming služby
/// </summary>
public class StreamingOptions
{
    public const string SectionName = "Streaming";

    /// <summary>
    /// Cesta k FFmpeg executable
    /// </summary>
    [Required]
    public string FFmpegPath { get; set; } = "ffmpeg";

    /// <summary>
    /// Cesta k FFprobe executable
    /// </summary>
    [Required]
    public string FFprobePath { get; set; } = "ffprobe";

    // Directories
    /// <summary>
    /// Pracovní adresář pro dočasné soubory
    /// </summary>
    public string WorkingDirectory { get; set; } = "data/streaming";

    /// <summary>
    /// Adresář pro nahrávky
    /// </summary>
    public string RecordingsDirectory { get; set; } = "data/recordings";

    /// <summary>
    /// Adresář pro thumbnails
    /// </summary>
    public string ThumbnailsDirectory { get; set; } = "data/thumbnails";

    /// <summary>
    /// Dočasný adresář
    /// </summary>
    public string TempDirectory { get; set; } = "data/temp";

    // Streaming Settings
    /// <summary>
    /// Maximální počet současných streamů
    /// </summary>
    [Range(1, 50)]
    public int MaxConcurrentStreams { get; set; } = 10;

    /// <summary>
    /// Maximální počet současných nahrávek
    /// </summary>
    [Range(1, 100)]
    public int MaxConcurrentRecordings { get; set; } = 5;

    /// <summary>
    /// Základní port pro streaming
    /// </summary>
    [Range(1000, 65535)]
    public int BaseStreamingPort { get; set; } = 8000;

    // Performance
    /// <summary>
    /// Povolit hardwarovou akceleraci
    /// </summary>
    public bool EnableHardwareAcceleration { get; set; } = true;

    /// <summary>
    /// Typ hardwarové akcelerace (auto, nvenc, qsv, vaapi)
    /// </summary>
    public string HardwareAccelerator { get; set; } = "auto";

    /// <summary>
    /// Maximální využití CPU v procentech
    /// </summary>
    [Range(10, 100)]
    public int MaxCpuUsagePercent { get; set; } = 80;

    // Timeouts & Cleanup
    /// <summary>
    /// Timeout pro FFmpeg procesy
    /// </summary>
    public TimeSpan ProcessTimeout { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Doba platnosti streamů
    /// </summary>
    public TimeSpan StreamExpiration { get; set; } = TimeSpan.FromHours(4);

    /// <summary>
    /// Interval čištění neaktivních streamů
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(30);

    // Storage
    /// <summary>
    /// Maximální velikost nahrávky v bajtech
    /// </summary>
    public long MaxRecordingSize { get; set; } = 10L * 1024 * 1024 * 1024; // 10GB

    /// <summary>
    /// Doba uchovávání nahrávek
    /// </summary>
    public TimeSpan RecordingRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Automatické mazání starých nahrávek
    /// </summary>
    public bool AutoCleanupRecordings { get; set; } = true;

    // Quality Presets
    /// <summary>
    /// Přednastavené kvality
    /// </summary>
    public Dictionary<string, QualityPreset> QualityPresets { get; set; } = new()
    {
        ["360p"] = new() { Width = 640, Height = 360, VideoBitrate = "800k", AudioBitrate = "96k" },
        ["480p"] = new() { Width = 854, Height = 480, VideoBitrate = "1200k", AudioBitrate = "128k" },
        ["720p"] = new() { Width = 1280, Height = 720, VideoBitrate = "2500k", AudioBitrate = "128k" },
        ["1080p"] = new() { Width = 1920, Height = 1080, VideoBitrate = "5000k", AudioBitrate = "192k" }
    };

    /// <summary>
    /// Validace konfigurace
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FFmpegPath))
            throw new ArgumentException("FFmpegPath cannot be empty", nameof(FFmpegPath));

        if (string.IsNullOrWhiteSpace(WorkingDirectory))
            throw new ArgumentException("WorkingDirectory cannot be empty", nameof(WorkingDirectory));

        if (MaxConcurrentStreams < 1 || MaxConcurrentStreams > 50)
            throw new ArgumentException("MaxConcurrentStreams must be between 1 and 50", nameof(MaxConcurrentStreams));

        // Ensure directories exist
        try
        {
            Directory.CreateDirectory(WorkingDirectory);
            Directory.CreateDirectory(RecordingsDirectory);
            Directory.CreateDirectory(ThumbnailsDirectory);
            Directory.CreateDirectory(TempDirectory);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to create directories: {ex.Message}");
        }
    }
}

/// <summary>
/// Přednastavení kvality videa
/// </summary>
public class QualityPreset
{
    public int Width { get; set; }
    public int Height { get; set; }
    public string VideoBitrate { get; set; } = "";
    public string AudioBitrate { get; set; } = "";
    public string VideoCodec { get; set; } = "h264";
    public string AudioCodec { get; set; } = "aac";
}