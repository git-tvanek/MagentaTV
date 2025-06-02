using System.ComponentModel.DataAnnotations;

namespace MagentaTV.Configuration;

public class CacheOptions
{
    public const string SectionName = "Cache";

    [Range(1, 1440)]
    public int DefaultExpirationMinutes { get; set; } = 15;

    [Range(1, 1440)]
    public int ChannelsExpirationMinutes { get; set; } = 60;

    [Range(1, 1440)]
    public int EpgExpirationMinutes { get; set; } = 30;

    [Range(1, 60)]
    public int StreamUrlExpirationMinutes { get; set; } = 5;
}