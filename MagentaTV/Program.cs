using MagentaTV.Services;
using MagentaTV.Services.TokenStorage;
using MagentaTV.Middleware;
using MagentaTV.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// === KONFIGURACE OPTIONS ===
builder.Services.Configure<MagentaTVOptions>(
    builder.Configuration.GetSection(MagentaTVOptions.SectionName));
builder.Services.Configure<CacheOptions>(
    builder.Configuration.GetSection(CacheOptions.SectionName));
builder.Services.Configure<TokenStorageOptions>(
    builder.Configuration.GetSection(TokenStorageOptions.SectionName));
builder.Services.Configure<RateLimitOptions>(
    builder.Configuration.GetSection(RateLimitOptions.SectionName));

// Validace TokenStorageOptions pøi startu
builder.Services.AddOptions<TokenStorageOptions>()
    .Configure(options => options.Validate())
    .ValidateDataAnnotations()
    .ValidateOnStart();

// === LOGGING KONFIGURACE ===
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

if (builder.Environment.IsProduction())
{
    builder.Logging.AddEventSourceLogger();
}

// === SLUŽBY KONFIGURACE ===
builder.Services.AddControllers(options =>
{
    options.ModelValidatorProviders.Clear();
});

// === TOKEN STORAGE KONFIGURACE ===
// Registrace Token Storage PØED HTTP Client
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
{
    // Pro development a testing používej in-memory storage
    builder.Services.AddSingleton<ITokenStorage, InMemoryTokenStorage>();
    builder.Logging.AddFilter("MagentaTV.Services.TokenStorage", LogLevel.Debug);
}
else
{
    // Pro production používej encrypted file storage
    builder.Services.AddSingleton<ITokenStorage, EncryptedFileTokenStorage>();
}

// HTTP Client konfigurace
builder.Services.AddHttpClient<Magenta>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
{
    UseCookies = false,
    MaxConnectionsPerServer = 10
});

// Registrace Magenta service s ALL required dependencies
builder.Services.AddScoped<Magenta>();

// Aliasy pro dependency injection
builder.Services.AddScoped<IMagenta>(provider => provider.GetRequiredService<Magenta>());

// API Explorer a Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "MagentaTV API",
        Version = "v1",
        Description = "API wrapper pro MagentaTV služby s persistent token storage",
        Contact = new() { Name = "MagentaTV API" }
    });

    // Add authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS konfigurace
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultPolicy", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
            policy.WithOrigins(corsOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    var rateLimitConfig = builder.Configuration.GetSection("RateLimit");

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = rateLimitConfig.GetValue<int>("PermitLimit", 100),
                Window = TimeSpan.FromMinutes(rateLimitConfig.GetValue<int>("WindowMinutes", 1))
            }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsync("Too many requests", token);
    };
});

// Memory Cache
builder.Services.AddMemoryCache();

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("magenta-api", () =>
    {
        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API is running");
    })
    .AddAsyncCheck("token-storage", async () =>
    {
        try
        {
            var app = builder.Services.BuildServiceProvider();
            var tokenStorage = app.GetService<ITokenStorage>();
            if (tokenStorage != null)
            {
                await tokenStorage.HasValidTokensAsync();
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Token storage accessible");
            }
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded("Token storage not available");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Token storage error", ex);
        }
    });

// === APLIKACE KONFIGURACE ===
var app = builder.Build();

// Development middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MagentaTV API v1");
        c.RoutePrefix = string.Empty;
        c.DisplayOperationId();
        c.DisplayRequestDuration();
    });
}

// Global Exception Handler
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Request/Response Logging middleware pro development
if (app.Environment.IsDevelopment())
{
    app.UseMiddleware<RequestResponseLoggingMiddleware>();
}

// Security headers
app.UseMiddleware<SecurityHeadersMiddleware>();

// CORS
app.UseCors("DefaultPolicy");

// Rate Limiting
app.UseRateLimiter();

// Routing
app.UseRouting();

// === TOKEN STORAGE CLEANUP ON STARTUP ===
var tokenOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<TokenStorageOptions>>().Value;
if (tokenOptions.ClearOnStartup)
{
    var tokenStorage = app.Services.GetRequiredService<ITokenStorage>();
    await tokenStorage.ClearTokensAsync();
    app.Logger.LogInformation("Token storage cleared on startup as configured");
}

// Health checks endpoint
app.MapHealthChecks("/health");

// === ADDITIONAL TOKEN MANAGEMENT ENDPOINTS ===
app.MapPost("/auth/logout", async (ITokenStorage tokenStorage) =>
{
    try
    {
        await tokenStorage.ClearTokensAsync();
        return Results.Ok(new { message = "Logged out successfully", timestamp = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Logout failed: {ex.Message}");
    }
}).WithTags("Authentication");

app.MapGet("/auth/status", async (ITokenStorage tokenStorage) =>
{
    try
    {
        var tokens = await tokenStorage.LoadTokensAsync();
        var hasValidTokens = tokens?.IsValid == true;

        return Results.Ok(new
        {
            isAuthenticated = hasValidTokens,
            username = tokens?.Username,
            expiresAt = tokens?.ExpiresAt,
            isExpired = tokens?.IsExpired,
            timeToExpiry = tokens?.TimeToExpiry,
            isNearExpiry = tokens?.IsNearExpiry,
            timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to get auth status: {ex.Message}");
    }
}).WithTags("Authentication");

// Debug endpoint pro development
if (app.Environment.IsDevelopment())
{
    app.MapGet("/debug/tokens", async (ITokenStorage tokenStorage) =>
    {
        try
        {
            var tokens = await tokenStorage.LoadTokensAsync();

            return Results.Ok(new
            {
                hasTokens = tokens != null,
                isValid = tokens?.IsValid,
                username = tokens?.Username,
                deviceId = tokens?.DeviceId,
                createdAt = tokens?.CreatedAt,
                expiresAt = tokens?.ExpiresAt,
                timeToExpiry = tokens?.TimeToExpiry,
                storageType = tokenStorage.GetType().Name,
                tokenExpiresAt = tokens?.ExpiresAt,
                isExpired = tokens?.IsExpired,
                isNearExpiry = tokens?.IsNearExpiry
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Debug failed: {ex.Message}");
        }
    }).WithTags("Debug");
}

// API Controllers
app.MapControllers();

// Graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    app.Logger.LogInformation("Application is shutting down...");
});

// Log startup information
var tokenStorageType = app.Services.GetRequiredService<ITokenStorage>().GetType().Name;
app.Logger.LogInformation("MagentaTV API starting up...");
app.Logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
app.Logger.LogInformation("Token Storage: {TokenStorage}", tokenStorageType);
app.Logger.LogInformation("Auto Load Tokens: {AutoLoad}", tokenOptions.AutoLoad);
app.Logger.LogInformation("Auto Save Tokens: {AutoSave}", tokenOptions.AutoSave);
app.Logger.LogInformation("Token Expiration: {TokenExpirationHours} hours", tokenOptions.TokenExpirationHours);

// Start aplikace
app.Run();