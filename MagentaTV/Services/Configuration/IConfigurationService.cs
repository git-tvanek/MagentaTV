using MagentaTV.Configuration;

namespace MagentaTV.Services.Configuration;

public interface IConfigurationService
{
    MagentaTVOptions MagentaTV { get; }
    SessionOptions Session { get; }
    CacheOptions Cache { get; }
    TokenStorageOptions TokenStorage { get; }
    CorsOptions Cors { get; }
    RateLimitOptions RateLimit { get; }
    NetworkOptions Network { get; }
    TelemetryOptions Telemetry { get; }
    BackgroundServiceOptions BackgroundServices { get; }
}
