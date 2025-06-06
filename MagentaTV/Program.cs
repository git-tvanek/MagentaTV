// Entry point for the MagentaTV REST API wrapper. This file configures
// dependency injection, middlewares and starts the ASP.NET Core application.
using MagentaTV.Configuration;
using MagentaTV.Services;
using MagentaTV.Services.Channels;
using MagentaTV.Services.Epg;
using MagentaTV.Services.Stream;
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
using MagentaTV.Hubs;
using MagentaTV.Services.Network;
using MagentaTV.Services.Cache;
using MagentaTV.Services.Configuration;
using MagentaTV.Services.Ffmpeg;
using MagentaTV.Services.Security;
using MagentaTV.Services.Middleware;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Mvc;
using Spectre.Console;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddBackgroundServices(builder.Configuration);

builder.Services.AddBackgroundService<TokenRefreshService>();
builder.Services.AddBackgroundService<SessionCleanupService>();
builder.Services.AddBackgroundService<CacheWarmingService>();
builder.Services.AddBackgroundService<TelemetryService>();
builder.Services.AddBackgroundService<DiscoveryResponderService>();


builder.Services.AddSingleton<ICacheWarmingService>(provider =>
    provider.GetRequiredService<CacheWarmingService>());
builder.Services.AddSingleton<ITelemetryService>(provider =>
    provider.GetRequiredService<TelemetryService>());

// MediatR Event Handlers
builder.Services.AddTransient<INotificationHandler<UserLoggedInEvent>, UserLoggedInEventHandler>();
builder.Services.AddTransient<INotificationHandler<TokensRefreshedEvent>, TokensRefreshedEventHandler>();
builder.Services.AddTransient<INotificationHandler<UserLoggedOutEvent>, UserLoggedOutEventHandler>();
builder.Services.AddTransient<INotificationHandler<FfmpegJobCompletedEvent>, FfmpegJobCompletedEventHandler>();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.AddOutputCache();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "MagentaTV API",
        Version = "v1",
        Description = "API wrapper pro MagentaTV služby s intelligent session managementem" // ✅ UPDATED description
    });
});

// HTTP context accessor for behaviors
builder.Services.AddHttpContextAccessor();

// MediatR and pipeline behaviors
builder.Services.AddMediatRWithBehaviors();

// Memory cache
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, CacheService>();
builder.Services.AddSingleton<IInputSanitizer, InputSanitizer>();
builder.Services.AddFfmpeg(builder.Configuration);

// Network configuration and service
builder.Services.Configure<NetworkOptions>(
    builder.Configuration.GetSection(NetworkOptions.SectionName));
builder.Services.AddSingleton<NetworkService>();
builder.Services.AddSingleton<INetworkService>(sp => sp.GetRequiredService<NetworkService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<NetworkService>());
builder.Services.Configure<DiscoveryOptions>(
    builder.Configuration.GetSection(DiscoveryOptions.SectionName));

// HTTP Client configured via NetworkService
builder.Services.AddHttpClient<IMagenta, Magenta>()
    .ConfigurePrimaryHttpMessageHandler(sp =>
    {
        var network = sp.GetRequiredService<INetworkService>();
        return network.CreateHttpMessageHandler();
    });

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
builder.Services.Configure<TelemetryOptions>(
    builder.Configuration.GetSection(TelemetryOptions.SectionName));

// Validate configuration
builder.Services.AddSingleton<IValidateOptions<MagentaTV.Configuration.SessionOptions>, ValidateSessionOptions>();
builder.Services.AddSingleton<IValidateOptions<TokenStorageOptions>, ValidateTokenStorageOptions>();

// Configuration service
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();

// Register main services
builder.Services.AddScoped<IMagenta, Magenta>();
builder.Services.AddScoped<IChannelService, ChannelService>();
builder.Services.AddScoped<IEpgService, EpgService>();
builder.Services.AddScoped<IStreamService, StreamService>();

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
    // Pro produkci můžete později implementovat DatabaseSessionManager nebo RedisSessionManager
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

// ✅ FIXED: Enhanced Health Checks with BackgroundServicesHealthCheck
builder.Services.AddHealthChecks()
    .AddCheck<SessionHealthCheck>("session-manager")
    .AddCheck<MagentaTVHealthCheck>("magentatv-api")
    .AddCheck<BackgroundServicesHealthCheck>("background-services"); // ✅ ADDED: Missing health check

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddDebug();
}

var app = builder.Build();

// Pretty console output using Spectre.Console
AnsiConsole.Write(new FigletText("MagentaTV API").Color(Color.MediumPurple));

var infoTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
infoTable.AddColumn("[yellow]Key[/]");
infoTable.AddColumn("[yellow]Value[/]");
infoTable.AddRow("Environment", app.Environment.EnvironmentName);
infoTable.AddRow("Version", typeof(Program).Assembly.GetName().Version?.ToString() ?? "n/a");
AnsiConsole.Write(new Panel(infoTable)
{
    Header = new PanelHeader("Startup Info"),
    Padding = new Padding(1, 1, 1, 1)
});


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

app.UseResponseCompression();

// Exception handling middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

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

app.UseMiddleware<UserRateLimitingMiddleware>();
app.UseRateLimiter();
app.UseOutputCache();

// Session validation is now handled solely by SessionValidationBehavior
// app.UseMiddleware<SessionMiddleware>();

app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

// Legacy route redirects for removed session endpoints
app.MapPost("/sessions/create", () => Results.Redirect("/auth/login", permanent: true, preserveMethod: true));
app.MapPost("/sessions/logout", () => Results.Redirect("/auth/logout", permanent: true, preserveMethod: true));

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
                duration = x.Value.Duration.ToString(),
                description = x.Value.Description 
            }),
            duration = report.TotalDuration.ToString(),
            timestamp = DateTime.UtcNow 
        };
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true 
        }));
    }
});

try
{
    var serviceManager = app.Services.GetRequiredService<IBackgroundServiceManager>();

    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("Starting background services...", async ctx =>
        {
            await serviceManager.StartAllServicesIntelligentlyAsync();
            ctx.Status("Background services started");
        });

    AnsiConsole.MarkupLine("[green]Background services running[/]");

    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("All background services started intelligently");
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Failed to start background services intelligently, falling back to basic startup");

    AnsiConsole.MarkupLine("[yellow]Background services fallback startup[/]");

 
    try
    {
        var serviceManager = app.Services.GetRequiredService<IBackgroundServiceManager>();
        await serviceManager.StartCoreServicesAsync(); 
        await serviceManager.StartServiceAsync<CacheWarmingService>(); // Start cache warming in fallback mode

        logger.LogWarning("Background services started in fallback mode");
    }
    catch (Exception fallbackEx)
    {
        logger.LogCritical(fallbackEx, "Failed to start background services even in fallback mode");
        // Don't throw - let the app start without background services
        AnsiConsole.MarkupLine("[red]Background services failed to start[/]");
    }
}

app.Run();

#region Configuration Validators

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

#endregion

#region Health Checks

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
                ["LastCleanup"] = stats.LastCleanup,
                ["ExpiredSessions"] = stats.TotalExpiredSessions, // ✅ ADDED: More stats
                ["InactiveSessions"] = stats.TotalInactiveSessions // ✅ ADDED: More stats
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
    private readonly IChannelService _channelService;
    private readonly ILogger<MagentaTVHealthCheck> _logger;

    public MagentaTVHealthCheck(IChannelService channelService, ILogger<MagentaTVHealthCheck> logger)
    {
        _channelService = channelService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var channels = await _channelService.GetChannelsAsync();

            var data = new Dictionary<string, object>
            {
                ["ChannelCount"] = channels.Count,
                ["LastCheck"] = DateTime.UtcNow,
                ["HasChannels"] = channels.Count > 0 
            };

            return HealthCheckResult.Healthy("MagentaTV API is healthy", data);
        }
        catch (UnauthorizedAccessException)
        {
          
            var data = new Dictionary<string, object>
            {
                ["LastCheck"] = DateTime.UtcNow,
                ["RequiresAuth"] = true
            };

            return HealthCheckResult.Degraded("MagentaTV API requires authentication", null, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MagentaTV health check failed");

            var data = new Dictionary<string, object>
            {
                ["LastCheck"] = DateTime.UtcNow,
                ["ErrorType"] = ex.GetType().Name
            };

            return HealthCheckResult.Unhealthy("MagentaTV API is unhealthy", ex, data);
        }
    }
}


public class BackgroundServicesHealthCheck : IHealthCheck
{
    private readonly IBackgroundServiceManager _backgroundManager;
    private readonly ILogger<BackgroundServicesHealthCheck> _logger;

    public BackgroundServicesHealthCheck(
        IBackgroundServiceManager backgroundManager,
        ILogger<BackgroundServicesHealthCheck> logger)
    {
        _backgroundManager = backgroundManager;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _backgroundManager.GetStatsAsync();
            var services = await _backgroundManager.GetAllServicesInfoAsync();

            var failedServices = services.Where(s => s.Status == MagentaTV.Models.Background.BackgroundServiceStatus.Failed).ToList();
            var runningServices = services.Where(s => s.Status == MagentaTV.Models.Background.BackgroundServiceStatus.Running).ToList();

            var data = new Dictionary<string, object>
            {
                ["TotalServices"] = stats.TotalServices,
                ["RunningServices"] = stats.RunningServices,
                ["FailedServices"] = failedServices.Count,
                ["QueuedItems"] = stats.QueuedItems,
                ["QueueCapacity"] = stats.QueueCapacity,
                ["QueueUtilization"] = stats.QueuedItems / (double)stats.QueueCapacity * 100, 
                ["FailedServiceNames"] = failedServices.Select(s => s.Name).ToArray(),
                ["RunningServiceNames"] = runningServices.Select(s => s.Name).ToArray(),
                ["LastUpdated"] = stats.LastUpdated
            };

            if (failedServices.Any())
            {
                return HealthCheckResult.Degraded(
                    $"Some background services failed: {string.Join(", ", failedServices.Select(s => s.Name))}",
                    null,
                    data);
            }

            if (stats.RunningServices == 0)
            {
                return HealthCheckResult.Unhealthy("No background services are running", null, data);
            }

            return HealthCheckResult.Healthy("All background services are healthy", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background services health check failed");
            return HealthCheckResult.Unhealthy("Background services health check failed", ex);
        }
    }
}

#endregion