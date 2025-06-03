namespace MagentaTV.Models
{
    public class ResourceUsage
    {
        public double CpuUsagePercent { get; set; }
        public long MemoryUsageBytes { get; set; }
        public long DiskUsageBytes { get; set; }
        public long AvailableDiskBytes { get; set; }
    }
}
