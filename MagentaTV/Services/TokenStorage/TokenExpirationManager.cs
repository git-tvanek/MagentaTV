using System.Threading;

using Microsoft.Extensions.Logging;

namespace MagentaTV.Services.TokenStorage;

/// <summary>
/// Helper class that periodically removes expired tokens from the underlying cache.
/// </summary>
public class TokenExpirationManager : IDisposable
{
    private readonly TokenCache _cache;
    private readonly ILogger<TokenExpirationManager>? _logger;
    private readonly TokenStorageMetrics? _metrics;
    private readonly Timer _timer;
    private bool _disposed;

    public TokenExpirationManager(
        TokenCache cache,
        TokenStorageMetrics? metrics = null,
        ILogger<TokenExpirationManager>? logger = null)
    {
        _cache = cache;
        _logger = logger;
        _metrics = metrics;
        _timer = new Timer(_ => CleanupExpiredTokens(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    private void CleanupExpiredTokens()
    {
        foreach (var kvp in _cache.Entries)
        {
            if (kvp.Value.Data.IsExpired)
            {
                if (_cache.TryRemove(kvp.Key, out _))
                {
                    _metrics?.IncrementExpiration();
                    _logger?.LogDebug("Removed expired tokens for session {SessionId}", kvp.Key);
                }
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _timer.Dispose();
        }

        _disposed = true;
    }

    ~TokenExpirationManager()
    {
        Dispose(false);
    }
}
