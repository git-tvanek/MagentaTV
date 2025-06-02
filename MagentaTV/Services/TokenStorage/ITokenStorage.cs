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
    /// Načte tokeny z úložiště
    /// </summary>
    /// <returns>Token data nebo null pokud nejsou dostupná</returns>
    Task<TokenData?> LoadTokensAsync();

    /// <summary>
    /// Vymaže tokeny z úložiště
    /// </summary>
    Task ClearTokensAsync();

    /// <summary>
    /// Zkontroluje, jestli jsou v úložišti platné tokeny
    /// </summary>
    /// <returns>True pokud jsou dostupné platné tokeny</returns>
    Task<bool> HasValidTokensAsync();
}