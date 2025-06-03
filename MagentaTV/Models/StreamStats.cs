namespace MagentaTV.Models
{
    public class StreamStats
    {
        public long BytesTransferred { get; set; }
        public double CurrentBitrate { get; set; }
        public int ViewerCount { get; set; }
        public TimeSpan Uptime { get; set; }
        public DateTime LastActivity { get; set; }
    }
}