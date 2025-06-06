namespace MagentaTV.Services.Ffmpeg;

public class FfmpegJobStatus
{
    public string JobId { get; set; } = string.Empty;
    public string OutputFile { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}
