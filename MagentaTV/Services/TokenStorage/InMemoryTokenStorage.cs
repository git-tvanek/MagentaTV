// MagentaTV/Services/TokenStorage/InMemoryTokenStorage.cs
using System.Collections.Concurrent;

namespace MagentaTV.Services.TokenStorage;

/// <summary>
/// In-memory implementace token storage pro development a testing
/// Tokeny se neukládají persistentně a zmizí po restartu aplikace
/// </summary>
public class InMemoryTokenStorage : ITokenStorage
{
    private readonly ConcurrentDictionary<string, TokenData> _tokens = new();
    private readonly ILogger<InMemoryTokenStorage> _logger;
    private const string DefaultSessionId = "default";

    public InMemoryTokenStorage(ILogger<InMemoryTokenStorage> logger)
    {
        _logger = logger;
        _logger.LogInformation("InMemoryTokenStorage initialized - tokens will not persist across restarts");
    }

    /// <summary>
    /// Uloží tokeny do paměti (výchozí session)
    /// </summary>
    public Task SaveTokensAsync(TokenData tokens) => SaveTokensAsync(DefaultSessionId, tokens);

    /// <summary>
    /// Uloží tokeny do paměti pro danou session
    /// </summary>
    public Task SaveTokensAsync(string sessionId, TokenData tokens)
    {
        _tokens.AddOrUpdate(sessionId, tokens, (_, _) => tokens);
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
        if (_tokens.TryGetValue(sessionId, out var data))
        {
            _logger.LogDebug(
                "Loading tokens from memory for session {SessionId}, user: {Username}, valid: {IsValid}",
                sessionId, data.Username, data.IsValid);
            return Task.FromResult<TokenData?>(data);
        }

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
        var username = removed?.Username;
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
        var hasValid = _tokens.TryGetValue(sessionId, out var data) && data.IsValid;
        _logger.LogDebug("HasValidTokens check for {SessionId}: {HasValid}", sessionId, hasValid);
        return Task.FromResult(hasValid);
    }

    /// <summary>
    /// Získá informace o současném stavu tokenů (pro debugging)
    /// </summary>
    public TokenStatus GetTokenStatus(string sessionId = DefaultSessionId)
    {
        _tokens.TryGetValue(sessionId, out var data);
        return new TokenStatus
        {
            HasTokens = data != null,
            IsValid = data?.IsValid ?? false,
            Username = data?.Username,
            ExpiresAt = data?.ExpiresAt,
            TimeToExpiry = data?.TimeToExpiry
        };
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