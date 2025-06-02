namespace MagentaTV.Services.TokenStorage;

/// <summary>
/// Model pro ukládání token dat
/// </summary>
public class TokenData
{
    /// <summary>
    /// Access token pro API volání
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token pro obnovení access tokenu
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Kdy token vyprší
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Uživatelské jméno pro které je token platný
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Kdy byl token vytvořen
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Device ID pro které je token platný
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Zkontroluje, jestli je token expirovaný
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    /// Zkontroluje, jestli je token platný (není prázdný a není expirovaný)
    /// </summary>
    public bool IsValid => !string.IsNullOrEmpty(AccessToken) && !IsExpired;

    /// <summary>
    /// Zbývající čas do expiration
    /// </summary>
    public TimeSpan TimeToExpiry => ExpiresAt - DateTime.UtcNow;

    /// <summary>
    /// Je token blízko expiraci (méně než 5 minut)?
    /// </summary>
    public bool IsNearExpiry => TimeToExpiry < TimeSpan.FromMinutes(5);
}