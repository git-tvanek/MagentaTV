using System.ComponentModel.DataAnnotations;

namespace MagentaTV.Configuration;

public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    [Range(1, 10000)]
    public int PermitLimit { get; set; } = 100;

    [Range(1, 60)]
    public int WindowMinutes { get; set; } = 1;

    public string QueueProcessingOrder { get; set; } = "OldestFirst";

    [Range(1, 100)]
    public int QueueLimit { get; set; } = 10;
}