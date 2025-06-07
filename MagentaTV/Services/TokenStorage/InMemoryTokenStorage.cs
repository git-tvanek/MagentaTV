// MagentaTV/Services/TokenStorage/InMemoryTokenStorage.cs
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MagentaTV.Configuration;

namespace MagentaTV.Services.TokenStorage;

/// <summary>
/// In-memory implementace token storage pro development a testing
/// Tokeny se neukládají persistentně a zmizí po restartu aplikace
/// </summary>
public class InMemoryTokenStorage : ITokenStorage, IDisposable
{
    private readonly ConcurrentDictionary<string, TokenEntry> _tokens = new();
    private readonly ILogger<InMemoryTokenStorage> _logger;
    private readonly TokenExpirationManager _expirationManager;
    private readonly int _maxTokenCount;
    private readonly TokenStorageMetrics _metrics = new();
    private const string DefaultSessionId = "default";

    public InMemoryTokenStorage(ILogger<InMemoryTokenStorage> logger, IOptions<TokenStorageOptions> options)
    {
        _logger = logger;
        _maxTokenCount = options.Value.MaxTokenCount;
        _expirationManager = new TokenExpirationManager(_tokens, _metrics);
        _logger.LogInformation("InMemoryTokenStorage initialized - tokens will not persist across restarts");
    }

    /// <summary>
    /// Exposes current metrics for diagnostics.
    /// </summary>
    public TokenStorageMetrics Metrics => _metrics;

    /// <summary>
    /// Uloží tokeny do paměti (výchozí session)
    /// </summary>
    public Task SaveTokensAsync(TokenData tokens) => SaveTokensAsync(DefaultSessionId, tokens);

    /// <summary>
    /// Uloží tokeny do paměti pro danou session
    /// </summary>
    public Task SaveTokensAsync(string sessionId, TokenData tokens)
    {
        _tokens.AddOrUpdate(sessionId,
            _ => new TokenEntry(tokens),
            (_, existing) =>
            {
                existing.Data = tokens;
                existing.UpdateAccess();
                return existing;
            });

        _logger.LogDebug(
            "Tokens saved in memory for session {SessionId}, user: {Username}, expires: {ExpiresAt}",
            sessionId, tokens.Username, tokens.ExpiresAt);

        EnforceLimit();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Načte tokeny z paměti
    /// </summary>
    public Task<TokenData?> LoadTokensAsync() => LoadTokensAsync(DefaultSessionId);

    public Task<TokenData?> LoadTokensAsync(string sessionId)
    {
        if (_tokens.TryGetValue(sessionId, out var entry))
        {
            if (entry.Data.IsExpired)
            {
                _tokens.TryRemove(sessionId, out _);
                _metrics.IncrementExpiration();
                _metrics.IncrementMiss();
                _logger.LogDebug("Removed expired tokens for session {SessionId}", sessionId);
                return Task.FromResult<TokenData?>(null);
            }

            entry.UpdateAccess();
            _metrics.IncrementHit();

            _logger.LogDebug(
                "Loading tokens from memory for session {SessionId}, user: {Username}, valid: {IsValid}",
                sessionId, entry.Data.Username, entry.Data.IsValid);
            return Task.FromResult<TokenData?>(entry.Data);
        }

        _metrics.IncrementMiss();
        _logger.LogDebug("No tokens found in memory for session {SessionId}", sessionId);
        return Task.FromResult<TokenData?>(null);
    }

    /// <summary>
    /// Vymaže tokeny z paměti
    /// </summary>
    public Task ClearTokensAsync() => ClearTokensAsync(DefaultSessionId);

    public Task ClearTokensAsync(string sessionId)
    {
        _tokens.TryRemove(sessionId, out var removed);
        var username = removed?.Data.Username;
        _logger.LogDebug(
            "Tokens cleared from memory for session {SessionId}, user: {Username}",
            sessionId, username);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Zkontroluje, jestli jsou v paměti platné tokeny
    /// </summary>
    public Task<bool> HasValidTokensAsync() => HasValidTokensAsync(DefaultSessionId);

    public Task<bool> HasValidTokensAsync(string sessionId)
    {
        var hasValid = _tokens.TryGetValue(sessionId, out var entry) && entry.Data.IsValid;
        _logger.LogDebug("HasValidTokens check for {SessionId}: {HasValid}", sessionId, hasValid);
        return Task.FromResult(hasValid);
    }

    /// <summary>
    /// Získá informace o současném stavu tokenů (pro debugging)
    /// </summary>
    public TokenStatus GetTokenStatus(string sessionId = DefaultSessionId)
    {
        _tokens.TryGetValue(sessionId, out var entry);
        return new TokenStatus
        {
            HasTokens = entry != null,
            IsValid = entry?.Data.IsValid ?? false,
            Username = entry?.Data.Username,
            ExpiresAt = entry?.Data.ExpiresAt,
            TimeToExpiry = entry?.Data.TimeToExpiry
        };
    }

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
                _metrics.IncrementEviction();
            }
        }

        if (oldest.Count > 0)
        {
            _logger.LogDebug("Evicted {Count} token entries due to limit", oldest.Count);
        }
    }

    public void Dispose()
    {
        _expirationManager.Dispose();
    }
}

/// <summary>
/// Helper třída pro debugging token stavu
/// </summary>
public class TokenStatus
{
    public bool HasTokens { get; set; }
    public bool IsValid { get; set; }
    public string? Username { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public TimeSpan? TimeToExpiry { get; set; }
}
