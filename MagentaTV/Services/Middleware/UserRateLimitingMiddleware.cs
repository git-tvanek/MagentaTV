using Microsoft.Extensions.Caching.Memory;

namespace MagentaTV.Services.Middleware;

/// <summary>
/// Middleware implementing simple sliding window rate limiting per user.
/// </summary>
public sealed class UserRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserRateLimitingMiddleware> _logger;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _window = TimeSpan.FromMinutes(1);
    private const int AuthenticatedLimit = 100;
    private const int AnonymousLimit = 20;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRateLimitingMiddleware"/> class.
    /// </summary>
    public UserRateLimitingMiddleware(RequestDelegate next, ILogger<UserRateLimitingMiddleware> logger, IMemoryCache cache)
    {
        _next = next;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// Processes the HTTP request and enforces per-user rate limits.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var key = GetUserKey(context);
        var timestamps = _cache.GetOrCreate(key, entry =>
        {
            entry.SlidingExpiration = _window;
            return new Queue<DateTime>();
        });

        var now = DateTime.UtcNow;
        lock (timestamps)
        {
            while (timestamps.Count > 0 && now - timestamps.Peek() > _window)
            {
                timestamps.Dequeue();
            }

            var limit = context.User.Identity?.IsAuthenticated == true ? AuthenticatedLimit : AnonymousLimit;
            if (timestamps.Count >= limit)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                _logger.LogWarning("Rate limit exceeded for {Key}", key);
                return;
            }

            timestamps.Enqueue(now);
        }

        await _next(context);
    }

    private static string GetUserKey(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return context.User.Identity.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "authenticated";
        }

        return "anon-" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }
}
