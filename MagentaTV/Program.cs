using MagentaTV.Services;
using MagentaTV.Middleware;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

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

// HTTP Client konfigurace
builder.Services.AddHttpClient<Magenta>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
{
    UseCookies = false, // Disable cookies for API calls
    MaxConnectionsPerServer = 10
});

// Registrace služeb
builder.Services.AddScoped<Magenta>();

// API Explorer a Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "MagentaTV API",
        Version = "v1",
        Description = "API wrapper pro MagentaTV služby",
        Contact = new() { Name = "MagentaTV API" }
    });

    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
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
            policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = builder.Configuration.GetValue<int>("RateLimit:PermitLimit", 100),
                Window = TimeSpan.FromMinutes(builder.Configuration.GetValue<int>("RateLimit:WindowMinutes", 1))
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
        // Basic health check - can be extended to check MagentaTV API availability
        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API is running");
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
        c.RoutePrefix = string.Empty; // Swagger UI na root URL
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

// Health checks endpoint
app.MapHealthChecks("/health");

// API Controllers
app.MapControllers();

// Graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    app.Logger.LogInformation("Application is shutting down...");
});

app.Logger.LogInformation("MagentaTV API starting up...");
app.Logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);

app.Run();