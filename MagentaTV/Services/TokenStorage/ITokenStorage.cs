namespace MagentaTV.Services.TokenStorage;

/// <summary>
/// Interface pro ukládání a načítání autentizačních tokenů
/// </summary>
public interface ITokenStorage
{
    /// <summary>
    /// Uloží tokeny do úložiště
    /// </summary>
    /// <param name="tokens">Token data k uložení</param>
    Task SaveTokensAsync(TokenData tokens);

    /// <summary>
    /// Uloží tokeny pro konkrétní session
    /// </summary>
    /// <param name="sessionId">ID session</param>
    /// <param name="tokens">Token data</param>
    Task SaveTokensAsync(string sessionId, TokenData tokens);

    /// <summary>
    /// Načte tokeny z úložiště
    /// </summary>
    /// <returns>Token data nebo null pokud nejsou dostupná</returns>
    Task<TokenData?> LoadTokensAsync();

    /// <summary>
    /// Načte tokeny pro konkrétní session
    /// </summary>
    /// <param name="sessionId">ID session</param>
    Task<TokenData?> LoadTokensAsync(string sessionId);

    /// <summary>
    /// Vymaže tokeny z úložiště
    /// </summary>
    Task ClearTokensAsync();

    /// <summary>
    /// Vymaže tokeny pro danou session
    /// </summary>
    Task ClearTokensAsync(string sessionId);

    /// <summary>
    /// Zkontroluje, jestli jsou v úložišti platné tokeny
    /// </summary>
    /// <returns>True pokud jsou dostupné platné tokeny</returns>
    Task<bool> HasValidTokensAsync();

    /// <summary>
    /// Zkontroluje platnost tokenů pro session
    /// </summary>
    Task<bool> HasValidTokensAsync(string sessionId);
}