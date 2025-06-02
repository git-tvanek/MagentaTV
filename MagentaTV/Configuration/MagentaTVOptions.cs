using MagentaTV.Models;
using System.ComponentModel.DataAnnotations;

namespace MagentaTV.Configuration;

public class MagentaTVOptions
{
    public const string SectionName = "MagentaTV";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://czgo.magio.tv";

    [Required]
    public string ApiVersion { get; set; } = "v2";

    [Required]
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36 MagioGO/4.0.21";

    [Required]
    public string DeviceName { get; set; } = "Android-STB";

    [Required]
    public string DeviceType { get; set; } = "OTT_STB";

    [Required]
    public string Quality { get; set; } = "p5";

    [Required]
    [RegularExpression("^[a-z]{2}$", ErrorMessage = "Language must be a 2-letter lowercase code")]
    public string Language { get; set; } = "cz";

    [Range(5, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    [Range(1, 10)]
    public int RetryAttempts { get; set; } = 3;

    [Range(1, 1440)]
    public int CacheExpirationMinutes { get; set; } = 15;


    public static ValidationResult? ValidateTimeRange(EpgItemDto dto, ValidationContext context)
    {
        if (dto.EndTime <= dto.StartTime)
        {
            return new ValidationResult("EndTime must be after StartTime");
        }

        return ValidationResult.Success;
    }
}
