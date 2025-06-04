using System.ComponentModel.DataAnnotations;

namespace MagentaTV.Configuration;

/// <summary>
/// Možnosti pro proxy streaming
/// </summary>
public class StreamProxyOptions
{
    /// <summary>
    /// Povolit adaptivní bitrate streaming
    /// </summary>
    public bool EnableAdaptiveBitrate { get; set; } = true;

    /// <summary>
    /// Délka segmentu v sekundách
    /// </summary>
    [Range(2, 30, ErrorMessage = "Segment duration must be between 2 and 30 seconds")]
    public int SegmentDuration { get; set; } = 6;

    /// <summary>
    /// Velikost playlistu (počet segmentů)
    /// </summary>
    [Range(3, 20, ErrorMessage = "Playlist size must be between 3 and 20 segments")]
    public int PlaylistSize { get; set; } = 10;

    /// <summary>
    /// Povolit hardwarovou akceleraci
    /// </summary>
    public bool EnableHardwareAcceleration { get; set; } = true;

    /// <summary>
    /// Výstupní formát streamu
    /// </summary>
    [RegularExpression("^(hls|dash|rtmp)$", ErrorMessage = "OutputFormat must be one of: hls, dash, rtmp")]
    public string OutputFormat { get; set; } = "hls";

    /// <summary>
    /// Povolit CORS headers pro web playery
    /// </summary>
    public bool EnableCors { get; set; } = true;

    /// <summary>
    /// Timeout pro inicializaci streamu v sekundách
    /// </summary>
    [Range(5, 120, ErrorMessage = "Startup timeout must be between 5 and 120 seconds")]
    public int StartupTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximální doba buffering před startem v sekundách
    /// </summary>
    [Range(1, 10, ErrorMessage = "Buffer time must be between 1 and 10 seconds")]
    public int BufferTimeSeconds { get; set; } = 3;

    /// <summary>
    /// Vlastní FFmpeg argumenty (pro pokročilé uživatele)
    /// </summary>
    public List<string> CustomArguments { get; set; } = new();
}

/// <summary>
/// Request pro vytvoření proxy streamu
/// </summary>
public class StreamProxyRequest
{
    /// <summary>
    /// Kvalita streamu
    /// </summary>
    [Required(ErrorMessage = "Quality is required")]
    [RegularExpression("^(360p|480p|720p|1080p)$", ErrorMessage = "Quality must be one of: 360p, 480p, 720p, 1080p")]
    public string Quality { get; set; } = "720p";

    /// <summary>
    /// Pokročilé možnosti streamu
    /// </summary>
    public StreamProxyOptions? Options { get; set; }

    /// <summary>
    /// Automaticky ukončit stream po této době (v minutách)
    /// </summary>
    [Range(1, 1440, ErrorMessage = "Auto stop time must be between 1 and 1440 minutes")]
    public int? AutoStopAfterMinutes { get; set; }

    /// <summary>
    /// Označení streamu pro snadnější identifikaci
    /// </summary>
    [StringLength(100, ErrorMessage = "Label cannot exceed 100 characters")]
    public string? Label { get; set; }
}