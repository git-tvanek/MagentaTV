namespace MagentaTV.Models.Background
{
    public class BackgroundServiceStats
    {
        public int TotalServices { get; set; }
        public int RunningServices { get; set; }
        public int QueuedItems { get; set; }
        public int QueueCapacity { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
