// MagentaTV/Services/TokenStorage/InMemoryTokenStorage.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MagentaTV.Configuration;

namespace MagentaTV.Services.TokenStorage;

/// <summary>
/// In-memory implementace token storage pro development a testing
/// Tokeny se neukládají persistentně a zmizí po restartu aplikace
/// </summary>
public class InMemoryTokenStorage : ITokenStorage, IDisposable
{
    private readonly TokenCache _cache;
    private readonly ILogger<InMemoryTokenStorage> _logger;
    private readonly TokenExpirationManager _expirationManager;
    private readonly TokenStorageMetrics _metrics = new();
    private bool _disposed;
    private const string DefaultSessionId = "default";

    public InMemoryTokenStorage(ILogger<InMemoryTokenStorage> logger, IOptions<TokenStorageOptions> options)
    {
        _logger = logger;
        _cache = new TokenCache(options.Value.MaxTokenCount, _metrics, NullLogger<TokenCache>.Instance);
        _expirationManager = new TokenExpirationManager(_cache, _metrics, NullLogger<TokenExpirationManager>.Instance);
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
        _cache.Save(sessionId, tokens);

        _logger.LogDebug(
            "Tokens saved in memory for session {SessionId}, user: {Username}, expires: {ExpiresAt}",
            sessionId, tokens.Username, tokens.ExpiresAt);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Načte tokeny z paměti
    /// </summary>
    public Task<TokenData?> LoadTokensAsync() => LoadTokensAsync(DefaultSessionId);

    public Task<TokenData?> LoadTokensAsync(string sessionId)
    {
        if (_cache.TryGet(sessionId, out var entry))
        {
            if (entry.Data.IsExpired)
            {
                _cache.TryRemove(sessionId, out _);
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
        _cache.TryRemove(sessionId, out var removed);
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
        var hasValid = _cache.TryGet(sessionId, out var entry) && entry.Data.IsValid;
        _logger.LogDebug("HasValidTokens check for {SessionId}: {HasValid}", sessionId, hasValid);
        return Task.FromResult(hasValid);
    }

    /// <summary>
    /// Získá informace o současném stavu tokenů (pro debugging)
    /// </summary>
    public TokenStatus GetTokenStatus(string sessionId = DefaultSessionId)
    {
        _cache.TryGet(sessionId, out var entry);
        return new TokenStatus
        {
            HasTokens = entry != null,
            IsValid = entry?.Data.IsValid ?? false,
            Username = entry?.Data.Username,
            ExpiresAt = entry?.Data.ExpiresAt,
            TimeToExpiry = entry?.Data.TimeToExpiry
        };
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
            _expirationManager.Dispose();
        }

        _disposed = true;
    }

    ~InMemoryTokenStorage()
    {
        Dispose(false);
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
