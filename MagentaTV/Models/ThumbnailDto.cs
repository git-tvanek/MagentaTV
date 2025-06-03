namespace MagentaTV.Models
{
    public class ThumbnailDto
    {
        public string ThumbnailId { get; set; } = string.Empty;
        public long ScheduleId { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string LocalPath { get; set; } = string.Empty;
        public TimeSpan Timestamp { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long FileSize { get; set; }
    }
}