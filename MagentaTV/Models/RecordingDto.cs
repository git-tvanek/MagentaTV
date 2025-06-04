using System.ComponentModel.DataAnnotations;

namespace MagentaTV.Models;

public class RecordingRequest
{
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    [Range(1, 28800)] // max 8 hours in seconds
    public TimeSpan? Duration { get; set; }

    [RegularExpression("^(360p|480p|720p|1080p)$")]
    public string Quality { get; set; } = "720p";

    [RegularExpression("^(mp4|mkv|avi)$")]
    public string Format { get; set; } = "mp4";

    public bool AutoCleanup { get; set; } = true;

    [StringLength(200)]
    public string? Title { get; set; }
}

public class RecordingDto
{
    public string RecordingId { get; set; } = string.Empty;
    public long ScheduleId { get; set; }
    public string ProgramTitle { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public RecordingStatus Status { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public string RelativeUrl { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public TimeSpan Duration { get; set; }
    public double Progress { get; set; }
    public string? Error { get; set; }
}