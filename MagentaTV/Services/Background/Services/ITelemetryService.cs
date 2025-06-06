namespace MagentaTV.Services.Background.Services;

public interface ITelemetryService
{
    /// <summary>
    /// Triggers immediate telemetry collection.
    /// </summary>
    Task CollectAsync();
}
