using MediatR;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace MagentaTV.Application.Behaviors
{
    public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
          where TRequest : notnull
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

        public CachingBehavior(IMemoryCache cache, ILogger<CachingBehavior<TRequest, TResponse>> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            // Pouze pro queries (read operations)
            if (!typeof(TRequest).Name.EndsWith("Query"))
            {
                return await next();
            }

            var cacheKey = $"{typeof(TRequest).Name}:{JsonSerializer.Serialize(request)}";

            if (_cache.TryGetValue(cacheKey, out TResponse? cachedResponse))
            {
                _logger.LogDebug("Cache hit for {RequestName}", typeof(TRequest).Name);
                return cachedResponse!;
            }

            _logger.LogDebug("Cache miss for {RequestName}", typeof(TRequest).Name);
            var response = await next();

            // Cache na 5 minut pro queries
            _cache.Set(cacheKey, response, TimeSpan.FromMinutes(5));

            return response;
        }
    }
}
