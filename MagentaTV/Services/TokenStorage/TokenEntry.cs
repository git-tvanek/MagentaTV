namespace MagentaTV.Services.TokenStorage;

/// <summary>
/// Wrapper for token data including last access timestamp to enable LRU cleanup.
/// </summary>
public class TokenEntry
{
    public TokenData Data { get; set; }
    public DateTime LastAccess { get; private set; }
    public int AccessCount { get; private set; }

    public TokenEntry(TokenData data)
    {
        Data = data;
        LastAccess = DateTime.UtcNow;
    }

    public void UpdateAccess()
    {
        LastAccess = DateTime.UtcNow;
        AccessCount++;
    }
}
