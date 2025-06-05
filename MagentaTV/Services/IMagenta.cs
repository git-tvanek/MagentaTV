using MagentaTV.Models;
using MagentaTV.Services.TokenStorage;

namespace MagentaTV.Services;

public interface IMagenta
{
    /// <summary>
    /// Přihlášení uživatele
    /// </summary>
    Task<bool> LoginAsync(string username, string password);

    /// <summary>
    /// Odhlášení uživatele a vymazání tokenů
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// Získání seznamu kanálů
    /// </summary>
    Task<List<ChannelDto>> GetChannelsAsync();

    /// <summary>
    /// Získání EPG pro kanál
    /// </summary>
    Task<List<EpgItemDto>> GetEpgAsync(int channelId, DateTime? from = null, DateTime? to = null);

    /// <summary>
    /// Získání stream URL pro kanál
    /// </summary>
    Task<string?> GetStreamUrlAsync(int channelId);

    /// <summary>
    /// Získání catchup stream URL
    /// </summary>
    Task<string?> GetCatchupStreamUrlAsync(long scheduleId);

    /// <summary>
    /// Generování M3U playlistu
    /// </summary>
    Task<string> GenerateM3UPlaylistAsync();

    /// <summary>
    /// Generování XMLTV
    /// </summary>
    string GenerateXmlTv(List<EpgItemDto> epg, int channelId);

    /// <summary>
    /// Obnoví access token pomocí refresh tokenu
    /// </summary>
    Task<TokenData?> RefreshTokensAsync(TokenData currentTokens);
}
