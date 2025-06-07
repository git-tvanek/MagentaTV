using System.Threading;

namespace MagentaTV.Services.TokenStorage;

/// <summary>
/// Metrics for tracking token storage operations.
/// </summary>
public class TokenStorageMetrics
{
    private long _hits;
    private long _misses;
    private long _evictions;
    private long _expirations;

    /// <summary>
    /// Number of successful token retrievals.
    /// </summary>
    public long Hits => Interlocked.Read(ref _hits);

    /// <summary>
    /// Number of failed token retrievals.
    /// </summary>
    public long Misses => Interlocked.Read(ref _misses);

    /// <summary>
    /// Number of tokens removed due to eviction.
    /// </summary>
    public long Evictions => Interlocked.Read(ref _evictions);

    /// <summary>
    /// Number of tokens removed due to expiration.
    /// </summary>
    public long Expirations => Interlocked.Read(ref _expirations);

    internal void IncrementHit() => Interlocked.Increment(ref _hits);
    internal void IncrementMiss() => Interlocked.Increment(ref _misses);
    internal void IncrementEviction() => Interlocked.Increment(ref _evictions);
    internal void IncrementExpiration() => Interlocked.Increment(ref _expirations);
}
