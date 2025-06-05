// MagentaTV/Program.cs
using MagentaTV.Configuration;
using MagentaTV.Services;
using MagentaTV.Services.Session;
using MagentaTV.Services.TokenStorage;
using MagentaTV.Middleware;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;
using MagentaTV.Application.EventHandlers;
using MagentaTV.Application.Events;
using MagentaTV.Extensions;
using MagentaTV.Services.Background.Services;
using MediatR;
using MagentaTV.Services.Background;

var builder = WebApplication.CreateBuilder(args);

// Background Services
builder.Services.AddBackgroundServices(builder.Configuration);
builder.Services.AddBackgroundService<TokenRefreshService>();
builder.Services.AddBackgroundService<SessionCleanupService>();
builder.Services.AddBackgroundService<CacheWarmingService>();

// MediatR Event Handlers
builder.Services.AddTransient<INotificationHandler<UserLoggedInEvent>, UserLoggedInEventHandler>();
builder.Services.AddTransient<INotificationHandler<TokensRefreshedEvent>, TokensRefreshedEventHandler>();
builder.Services.AddTransient<INotificationHandler<UserLoggedOutEvent>, UserLoggedOutEventHandler>();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "MagentaTV API",
        Version = "v1",
// HTTP context accessor for behaviors
builder.Services.AddHttpContextAccessor();

// MediatR and pipeline behaviors
builder.Services.AddMediatRWithBehaviors();
        Description = "API wrapper pro MagentaTV služby s session managementem"
    });
});

// Memory cache
builder.Services.AddMemoryCache();

// HTTP Client - simplified without Polly for now
builder.Services.AddHttpClient<IMagenta, Magenta>();

// Configuration options with validation
builder.Services.Configure<MagentaTVOptions>(
    builder.Configuration.GetSection(MagentaTVOptions.SectionName));
builder.Services.Configure<CacheOptions>(
    builder.Configuration.GetSection(CacheOptions.SectionName));
builder.Services.Configure<TokenStorageOptions>(
    builder.Configuration.GetSection(TokenStorageOptions.SectionName));
builder.Services.Configure<MagentaTV.Configuration.SessionOptions>(
    builder.Configuration.GetSection(MagentaTV.Configuration.SessionOptions.SectionName));
builder.Services.Configure<CorsOptions>(
    builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.Configure<RateLimitOptions>(
    builder.Configuration.GetSection(RateLimitOptions.SectionName));

// Validate configuration
builder.Services.AddSingleton<IValidateOptions<MagentaTV.Configuration.SessionOptions>, ValidateSessionOptions>();
builder.Services.AddSingleton<IValidateOptions<TokenStorageOptions>, ValidateTokenStorageOptions>();

// Register main services
builder.Services.AddScoped<IMagenta, Magenta>();

// Token Storage - choose implementation based on environment
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<ITokenStorage, InMemoryTokenStorage>();
}
else
{
    builder.Services.AddSingleton<ITokenStorage, EncryptedFileTokenStorage>();
}

// Session Management - choose implementation based on environment
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<ISessionManager, InMemorySessionManager>();
}
else
{
    // Pro produkci mùžete pozdìji implementovat DatabaseSessionManager nebo RedisSessionManager
    builder.Services.AddSingleton<ISessionManager, InMemorySessionManager>();
}

// CORS configuration
var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>();
if (corsOptions?.AllowedOrigins?.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(corsOptions.AllowedOrigins)
                  .WithMethods(corsOptions.AllowedMethods)
                  .WithHeaders(corsOptions.AllowedHeaders);

            if (corsOptions.AllowCredentials)
            {
                policy.AllowCredentials();
            }
        });
    });
}

// Rate Limiting - updated for .NET 9
var rateLimitOptions = builder.Configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>();
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = rateLimitOptions?.PermitLimit ?? 100,
                Window = TimeSpan.FromMinutes(rateLimitOptions?.WindowMinutes ?? 1),
                QueueLimit = rateLimitOptions?.QueueLimit ?? 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<SessionHealthCheck>("session-manager")
    .AddCheck<MagentaTVHealthCheck>("magentatv-api");

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddDebug();
}

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MagentaTV API v1");
        c.RoutePrefix = string.Empty; // Swagger na root URL
    });
}

// Security headers middleware
app.UseMiddleware<SecurityHeadersMiddleware>();

// Exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Request/Response logging (only in development)
if (app.Environment.IsDevelopment())
{
    app.UseMiddleware<RequestResponseLoggingMiddleware>();
}

// CORS
if (corsOptions?.AllowedOrigins?.Length > 0)
{
    app.UseCors();
}

// Rate limiting
app.UseRateLimiter();

// Session middleware - IMPORTANT: must be before authorization
app.UseMiddleware<SessionMiddleware>();

app.UseAuthorization();

app.MapControllers();

// Health checks endpoint
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(x => new
            {
                name = x.Key,
                status = x.Value.Status.ToString(),
                exception = x.Value.Exception?.Message,
                duration = x.Value.Duration.ToString()
            }),
            duration = report.TotalDuration.ToString()
        };
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
});

var serviceManager = app.Services.GetRequiredService<IBackgroundServiceManager>();
await serviceManager.StartServiceAsync<TokenRefreshService>();
await serviceManager.StartServiceAsync<SessionCleanupService>();
await serviceManager.StartServiceAsync<CacheWarmingService>();

app.Run();

// Configuration validators
public class ValidateSessionOptions : IValidateOptions<MagentaTV.Configuration.SessionOptions>
{
    public ValidateOptionsResult Validate(string? name, MagentaTV.Configuration.SessionOptions options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (ArgumentException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}

public class ValidateTokenStorageOptions : IValidateOptions<TokenStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, TokenStorageOptions options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (ArgumentException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}

// Health Checks
public class SessionHealthCheck : IHealthCheck
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<SessionHealthCheck> _logger;

    public SessionHealthCheck(ISessionManager sessionManager, ILogger<SessionHealthCheck> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _sessionManager.GetStatisticsAsync();

            var data = new Dictionary<string, object>
            {
                ["ActiveSessions"] = stats.TotalActiveSessions,
                ["UniqueUsers"] = stats.UniqueUsers,
                ["LastCleanup"] = stats.LastCleanup
            };

            return HealthCheckResult.Healthy("Session manager is healthy", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Session health check failed");
            return HealthCheckResult.Unhealthy("Session manager is unhealthy", ex);
        }
    }
}

public class MagentaTVHealthCheck : IHealthCheck
{
    private readonly IMagenta _magentaService;
    private readonly ILogger<MagentaTVHealthCheck> _logger;

    public MagentaTVHealthCheck(IMagenta magentaService, ILogger<MagentaTVHealthCheck> logger)
    {
        _magentaService = magentaService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
}
            var channels = await _magentaService.GetChannelsAsync();

            var data = new Dictionary<string, object>
            {
                ["ChannelCount"] = channels.Count,
                ["LastCheck"] = DateTime.UtcNow
            };

            return HealthCheckResult.Healthy("MagentaTV API is healthy", data);
        }
        catch (UnauthorizedAccessException)
        {
            return HealthCheckResult.Degraded("MagentaTV API requires authentication");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MagentaTV health check failed");
            return HealthCheckResult.Unhealthy("MagentaTV API is unhealthy", ex);
        }
    }
}