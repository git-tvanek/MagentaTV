using System.ComponentModel.DataAnnotations;

namespace MagentaTV.Configuration;

public class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 5;

    public string LogFilePath { get; set; } = "logs/telemetry.log";
}
