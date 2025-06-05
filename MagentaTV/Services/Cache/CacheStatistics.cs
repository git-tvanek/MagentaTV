namespace MagentaTV.Services.Cache
{
    public class CacheStatistics
    {
        public string Key { get; set; } = string.Empty;
        public long Hits { get; set; }
        public long Misses { get; set; }
        public long Sets { get; set; }
        public long Evictions { get; set; }
        public DateTime? LastHit { get; set; }
        public DateTime? LastMiss { get; set; }
        public DateTime? LastSet { get; set; }
        public DateTime? LastEviction { get; set; }
        public string? EvictionReason { get; set; }

        public double HitRatio => Hits + Misses == 0 ? 0 : (double)Hits / (Hits + Misses);
    }
}
