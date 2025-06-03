using System.ComponentModel.DataAnnotations;

namespace MagentaTV.Configuration
{
    public class FFmpegOptions
    {
        public const string SectionName = "FFmpeg";

        [Required]
        public string FFmpegPath { get; set; } = "ffmpeg";

        [Required]
        public string FFprobePath { get; set; } = "ffprobe";

        public string WorkingDirectory { get; set; } = "temp";
        public string RecordingsDirectory { get; set; } = "recordings";
        public string ThumbnailsDirectory { get; set; } = "thumbnails";

        [Range(1, 10)]
        public int MaxConcurrentStreams { get; set; } = 5;

        [Range(1000, 65535)]
        public int BaseStreamingPort { get; set; } = 8000;

        public Dictionary<string, VideoQuality> QualityPresets { get; set; } = new()
        {
            ["480p"] = new() { Width = 854, Height = 480, Bitrate = "1500k" },
            ["720p"] = new() { Width = 1280, Height = 720, Bitrate = "3000k" },
            ["1080p"] = new() { Width = 1920, Height = 1080, Bitrate = "6000k" }
        };

        public TimeSpan ProcessTimeout { get; set; } = TimeSpan.FromMinutes(30);
        public bool EnableHardwareAcceleration { get; set; } = true;
        public string HardwareAccelerator { get; set; } = "auto"; // auto, nvenc, qsv, vaapi
    }
}