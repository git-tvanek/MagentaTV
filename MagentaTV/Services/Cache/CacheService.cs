using MagentaTV.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace MagentaTV.Services.Cache
{
    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CacheService> _logger;
        private readonly ConcurrentDictionary<string, CacheStatistics> _stats = new();

        public CacheService(IMemoryCache cache, ILogger<CacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public Task<T?> GetAsync<T>(string key) where T : class
        {
            var stats = GetOrCreateStats(key);

            if (_cache.TryGetValue(key, out var cached))
            {
                stats.Hits++;
                stats.LastHit = DateTime.UtcNow;
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return Task.FromResult(cached as T);
            }

            stats.Misses++;
            stats.LastMiss = DateTime.UtcNow;
            _logger.LogDebug("Cache miss for key: {Key}", key);
            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            var options = new MemoryCacheEntryOptions();

            if (expiration.HasValue)
            {
                options.SetAbsoluteExpiration(expiration.Value);
            }
            else
            {
                // Smart expiration based on data type
                options.SetAbsoluteExpiration(GetSmartExpiration<T>());
            }

            // Add eviction callback for statistics
            options.RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                var stats = GetOrCreateStats(key.ToString()!);
                stats.Evictions++;
                stats.LastEviction = DateTime.UtcNow;
                stats.EvictionReason = reason.ToString();
            });

            _cache.Set(key, value, options);

            var entryStats = GetOrCreateStats(key);
            entryStats.Sets++;
            entryStats.LastSet = DateTime.UtcNow;

            _logger.LogDebug("Cache set for key: {Key}, expiration: {Expiration}", key, expiration);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            _cache.Remove(key);
            _logger.LogDebug("Cache removed for key: {Key}", key);
            return Task.CompletedTask;
        }

        public Task RemoveByPatternAsync(string pattern)
        {
            // This is a limitation of IMemoryCache - we'd need to track keys
            _logger.LogWarning("Pattern-based cache removal not supported with IMemoryCache. Consider Redis for production.");
            return Task.CompletedTask;
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            var cached = await GetAsync<T>(key);
            if (cached != null)
            {
                return cached;
            }

            try
            {
                var value = await factory();
                await SetAsync(key, value, expiration);
                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create cache value for key: {Key}", key);
                throw;
            }
        }

        public Task<Dictionary<string, CacheStatistics>> GetStatisticsAsync()
        {
            return Task.FromResult(new Dictionary<string, CacheStatistics>(_stats));
        }

        private CacheStatistics GetOrCreateStats(string key)
        {
            return _stats.GetOrAdd(key, _ => new CacheStatistics { Key = key });
        }

        private TimeSpan GetSmartExpiration<T>()
        {
            return typeof(T).Name switch
            {
                nameof(ChannelDto) => TimeSpan.FromHours(2),      // Channels change rarely
                nameof(EpgItemDto) => TimeSpan.FromMinutes(30),   // EPG changes more frequently
                "StreamUrlDto" => TimeSpan.FromMinutes(5),        // Stream URLs expire quickly
                _ => TimeSpan.FromMinutes(15)                     // Default
            };
        }
    }
}
