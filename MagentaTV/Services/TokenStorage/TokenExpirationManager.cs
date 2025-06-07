using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MagentaTV.Services.TokenStorage;

/// <summary>
/// Helper class that periodically removes expired tokens from the underlying cache.
/// </summary>
public class TokenExpirationManager : IDisposable
{
    private readonly ConcurrentDictionary<string, TokenData> _tokens;
    private readonly ILogger<TokenExpirationManager> _logger;
    private readonly Timer _timer;

    public TokenExpirationManager(ConcurrentDictionary<string, TokenData> tokens, ILogger<TokenExpirationManager> logger)
    {
        _tokens = tokens;
        _logger = logger;
        _timer = new Timer(_ => CleanupExpiredTokens(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    private void CleanupExpiredTokens()
    {
        foreach (var kvp in _tokens.ToArray())
        {
            if (kvp.Value.IsExpired)
            {
                if (_tokens.TryRemove(kvp.Key, out _))
                {
                    _logger.LogDebug("Removed expired tokens for session {SessionId}", kvp.Key);
                }
            }
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
