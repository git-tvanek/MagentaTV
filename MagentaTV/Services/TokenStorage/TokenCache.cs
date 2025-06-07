using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MagentaTV.Services.TokenStorage;

/// <summary>
/// In-memory cache for tokens with simple LRU eviction.
/// </summary>
public class TokenCache
{
    private readonly ConcurrentDictionary<string, TokenEntry> _tokens = new();
    private readonly int _maxTokenCount;
    private readonly ILogger<TokenCache>? _logger;
    private readonly TokenStorageMetrics? _metrics;

    public TokenCache(int maxTokenCount, TokenStorageMetrics? metrics = null, ILogger<TokenCache>? logger = null)
    {
        _maxTokenCount = maxTokenCount;
        _metrics = metrics;
        _logger = logger;
    }

    /// <summary>
    /// Adds or updates tokens for the given session.
    /// </summary>
    public void Save(string sessionId, TokenData tokens)
    {
        _tokens.AddOrUpdate(sessionId,
            _ => new TokenEntry(tokens),
            (_, existing) =>
            {
                existing.Data = tokens;
                existing.UpdateAccess();
                return existing;
            });

        EnforceLimit();
    }

    /// <summary>
    /// Attempts to retrieve tokens for the session.
    /// </summary>
    public bool TryGet(string sessionId, out TokenEntry entry) => _tokens.TryGetValue(sessionId, out entry);

    /// <summary>
    /// Removes tokens for the given session.
    /// </summary>
    public bool TryRemove(string sessionId, out TokenEntry? removed) => _tokens.TryRemove(sessionId, out removed);

    /// <summary>
    /// Enumerates all entries. Intended for internal use.
    /// </summary>
    internal IEnumerable<KeyValuePair<string, TokenEntry>> Entries => _tokens.ToArray();

    private void EnforceLimit()
    {
        if (_tokens.Count < _maxTokenCount)
            return;

        var removeCount = Math.Max(1, _maxTokenCount / 10);
        var oldest = _tokens
            .OrderBy(kvp => kvp.Value.LastAccess)
            .Take(removeCount)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in oldest)
        {
            if (_tokens.TryRemove(key, out _))
            {
                _metrics?.IncrementEviction();
            }
        }

        if (oldest.Count > 0)
        {
            _logger?.LogDebug("Evicted {Count} token entries due to limit", oldest.Count);
        }
    }
}
