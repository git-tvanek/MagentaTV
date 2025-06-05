namespace MagentaTV.Services.Cache
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task RemoveAsync(string key);
        Task RemoveByPatternAsync(string pattern);
        Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class;
        Task<Dictionary<string, CacheStatistics>> GetStatisticsAsync();
    }
}
