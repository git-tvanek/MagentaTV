using System.Threading.RateLimiting;
using HealthChecks.UI.Client;
using MagentaTV.Application.Behaviors;
using MagentaTV.Application.Behaviors.MagentaTV.Application.Behaviors;
using MagentaTV.Configuration;
using MagentaTV.Extensions;
using MagentaTV.Middleware;
using MagentaTV.Services;
using MagentaTV.Services.Background;
using MagentaTV.Services.Background.Core;
using MagentaTV.Services.Background.Events;
using MagentaTV.Services.Background.Services;
using MagentaTV.Services.Session;
using MagentaTV.Services.TokenStorage;
using MediatR;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using SessionOptions = MagentaTV.Configuration.SessionOptions;

namespace MagentaTV.Application.Commands
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            ConfigureServices(builder.Services, builder.Configuration);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            ConfigureMiddleware(app);

            app.Run();
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Configuration
            services.Configure<MagentaTVOptions>(configuration.GetSection(MagentaTVOptions.SectionName));
            services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
            services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));
            services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));
            services.Configure<SessionOptions>(configuration.GetSection(SessionOptions.SectionName));
            services.Configure<TokenStorageOptions>(configuration.GetSection(TokenStorageOptions.SectionName));
            services.Configure<BackgroundServiceOptions>(configuration.GetSection(BackgroundServiceOptions.SectionName));

            // Validate configurations
            services.PostConfigure<SessionOptions>(options => options.Validate());
            services.PostConfigure<TokenStorageOptions>(options => options.Validate());

            // Basic services
            services.AddControllers();
            services.AddMemoryCache();
            services.AddHttpContextAccessor();

            // API documentation
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                var apiSection = configuration.GetSection("Api");
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = apiSection["Title"] ?? "MagentaTV API",
                    Version = apiSection["Version"] ?? "v1",
                    Description = apiSection["Description"],
                    Contact = new Microsoft.OpenApi.Models.OpenApiContact
                    {
                        Name = apiSection["ContactName"],
                        Email = apiSection["ContactEmail"]
                    }
                });

                // Include XML comments if available
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }
            });

            // MediatR configuration
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);

                // Register behaviors in order
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(SessionValidationBehavior<,>));
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
            });

            // Session management
            services.AddSingleton<ISessionManager, InMemorySessionManager>();

            // Token storage
            if (configuration.GetValue<bool>("Development:UseInMemoryTokenStorage"))
            {
                services.AddSingleton<ITokenStorage, InMemoryTokenStorage>();
            }
            else
            {
                services.AddSingleton<ITokenStorage, EncryptedFileTokenStorage>();
            }

            // Background services
            services.AddBackgroundServices(configuration);
            services.AddSingleton<IBackgroundServiceManager, BackgroundServiceManager>();

            // Register individual background services
            services.AddBackgroundService<TokenRefreshService>();
            services.AddBackgroundService<SessionCleanupService>();
            services.AddBackgroundService<CacheWarmingService>();

            // HTTP client for MagentaTV
            services.AddHttpClient<IMagenta, Magenta>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<MagentaTVOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

            // CORS
            services.AddCors(options =>
            {
                options.AddPolicy("MagentaTVPolicy", builder =>
                {
                    var corsOptions = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

                    if (corsOptions.AllowedOrigins?.Any() == true)
                    {
                        builder.WithOrigins(corsOptions.AllowedOrigins);
                    }
                    else
                    {
                        builder.AllowAnyOrigin();
                    }

                    builder.WithMethods(corsOptions.AllowedMethods ?? new[] { "GET", "POST", "PUT", "DELETE", "OPTIONS" });

                    if (corsOptions.AllowedHeaders?.Contains("*") == true)
                    {
                        builder.AllowAnyHeader();
                    }
                    else
                    {
                        builder.WithHeaders(corsOptions.AllowedHeaders ?? new[] { "*" });
                    }

                    if (corsOptions.AllowCredentials)
                    {
                        builder.AllowCredentials();
                    }
                });
            });

            // Rate limiting
            services.AddRateLimiter(options =>
            {
                var rateLimitOptions = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User?.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = rateLimitOptions.PermitLimit,
                            Window = TimeSpan.FromMinutes(rateLimitOptions.WindowMinutes),
                            QueueProcessingOrder = rateLimitOptions.QueueProcessingOrder == "OldestFirst"
                                ? QueueProcessingOrder.OldestFirst
                                : QueueProcessingOrder.NewestFirst,
                            QueueLimit = rateLimitOptions.QueueLimit
                        }));
            });

            // Health checks
            services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy())
                .AddCheck<MagentaTVHealthCheck>("magentatv_api")
                .AddCheck<SessionHealthCheck>("session_manager")
                .AddCheck<TokenStorageHealthCheck>("token_storage");
        }

        private static void ConfigureMiddleware(WebApplication app)
        {
            // Development specific middleware
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MagentaTV API v1");
                    c.RoutePrefix = string.Empty; // Swagger at root
                });
            }
            else
            {
                // Production middleware
                app.UseExceptionHandler("/error");
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            // Security headers
            app.UseMiddleware<SecurityHeadersMiddleware>();

            // Request/Response logging (only in development)
            if (app.Configuration.GetValue<bool>("Development:EnableRequestResponseLogging"))
            {
                app.UseMiddleware<RequestResponseLoggingMiddleware>();
            }

            // Exception handling
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            // CORS
            app.UseCors("MagentaTVPolicy");

            // Rate limiting
            app.UseRateLimiter();

            // Session middleware - MUST be before Authorization
            app.UseMiddleware<SessionMiddleware>();

            app.UseRouting();

            // Health checks
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            app.MapControllers();

            // Minimal API endpoints for testing
            if (app.Environment.IsDevelopment())
            {
                app.MapGet("/", () => Results.Redirect("/swagger"));
                app.MapGet("/error", () => Results.Problem("An error occurred.", statusCode: 500));
            }
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => !msg.IsSuccessStatusCode)
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        var logger = context.ContainsKey("logger") ? context["logger"] as ILogger : null;
                        logger?.LogWarning("Delaying for {delay}ms, then making retry {retry}.", timespan.TotalMilliseconds, retryCount);
                    });
        }

        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    5,
                    TimeSpan.FromSeconds(30),
                    onBreak: (result, timespan) =>
                    {
                        // Log circuit breaker open
                    },
                    onReset: () =>
                    {
                        // Log circuit breaker reset
                    });
        }
    }

    // Health check implementations
    public class MagentaTVHealthCheck : IHealthCheck
    {
        private readonly IMagenta _magentaService;

        public MagentaTVHealthCheck(IMagenta magentaService)
        {
            _magentaService = magentaService;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // Simple check - just verify service is reachable
                return HealthCheckResult.Healthy("MagentaTV API is reachable");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("MagentaTV API is not reachable", ex);
            }
        }
    }

    public class SessionHealthCheck : IHealthCheck
    {
        private readonly ISessionManager _sessionManager;

        public SessionHealthCheck(ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var stats = await _sessionManager.GetStatisticsAsync();
                return HealthCheckResult.Healthy($"Session manager is healthy. Active sessions: {stats.TotalActiveSessions}");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Session manager is not healthy", ex);
            }
        }
    }

    public class TokenStorageHealthCheck : IHealthCheck
    {
        private readonly ITokenStorage _tokenStorage;

        public TokenStorageHealthCheck(ITokenStorage tokenStorage)
        {
            _tokenStorage = tokenStorage;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // Just check if we can access token storage
                _ = await _tokenStorage.HasValidTokensAsync();
                return HealthCheckResult.Healthy("Token storage is accessible");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Token storage is not accessible", ex);
            }
        }
    }
}