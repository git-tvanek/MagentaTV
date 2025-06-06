namespace MagentaTV.Configuration;

public class FFmpegOptions
{
    public const string SectionName = "FFmpeg";

    public string BinaryFolder { get; set; } = "ffmpeg";
    public string TemporaryFilesFolder { get; set; } = "ffmpeg_temp";
}
