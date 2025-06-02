// MagentaTV/Services/TokenStorage/InMemoryTokenStorage.cs
namespace MagentaTV.Services.TokenStorage;

/// <summary>
/// In-memory implementace token storage pro development a testing
/// Tokeny se neukládají persistentně a zmizí po restartu aplikace
/// </summary>
public class InMemoryTokenStorage : ITokenStorage
{
    private TokenData? _tokens;
    private readonly ILogger<InMemoryTokenStorage> _logger;
    private readonly object _lock = new();

    public InMemoryTokenStorage(ILogger<InMemoryTokenStorage> logger)
    {
        _logger = logger;
        _logger.LogInformation("InMemoryTokenStorage initialized - tokens will not persist across restarts");
    }

    /// <summary>
    /// Uloží tokeny do paměti
    /// </summary>
    public Task SaveTokensAsync(TokenData tokens)
    {
        lock (_lock)
        {
            _tokens = tokens;
            _logger.LogDebug("Tokens saved in memory for user: {Username}, expires: {ExpiresAt}",
                tokens.Username, tokens.ExpiresAt);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Načte tokeny z paměti
    /// </summary>
    public Task<TokenData?> LoadTokensAsync()
    {
        lock (_lock)
        {
            if (_tokens != null)
            {
                _logger.LogDebug("Loading tokens from memory for user: {Username}, valid: {IsValid}",
                    _tokens.Username, _tokens.IsValid);
            }
            else
            {
                _logger.LogDebug("No tokens found in memory");
            }

            return Task.FromResult(_tokens);
        }
    }

    /// <summary>
    /// Vymaže tokeny z paměti
    /// </summary>
    public Task ClearTokensAsync()
    {
        lock (_lock)
        {
            var username = _tokens?.Username;
            _tokens = null;
            _logger.LogDebug("Tokens cleared from memory for user: {Username}", username);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Zkontroluje, jestli jsou v paměti platné tokeny
    /// </summary>
    public Task<bool> HasValidTokensAsync()
    {
        lock (_lock)
        {
            var hasValid = _tokens?.IsValid == true;
            _logger.LogDebug("HasValidTokens check: {HasValid}", hasValid);
            return Task.FromResult(hasValid);
        }
    }

    /// <summary>
    /// Získá informace o současném stavu tokenů (pro debugging)
    /// </summary>
    public TokenStatus GetTokenStatus()
    {
        lock (_lock)
        {
            return new TokenStatus
            {
                HasTokens = _tokens != null,
                IsValid = _tokens?.IsValid ?? false,
                Username = _tokens?.Username,
                ExpiresAt = _tokens?.ExpiresAt,
                TimeToExpiry = _tokens?.TimeToExpiry
            };
        }
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