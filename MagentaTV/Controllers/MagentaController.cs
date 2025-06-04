using MagentaTV.Models;
using MagentaTV.Models.Session;
using MagentaTV.Services;
using MagentaTV.Services.Session;
using MagentaTV.Services.TokenStorage;
using MagentaTV.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace MagentaTV.Controllers;

[ApiController]
[Route("magenta")]
public class MagentaController : ControllerBase
{
    private readonly IMagenta _service;
    private readonly ITokenStorage _tokenStorage;
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<MagentaController> _logger;

    public MagentaController(
        IMagenta service,
        ITokenStorage tokenStorage,
        ISessionManager sessionManager,
        ILogger<MagentaController> logger)
    {
        _service = service;
        _tokenStorage = tokenStorage;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    /// <summary>
    /// Přihlášení uživatele - vytvoří session + tokeny se automaticky ukládají
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    public async Task<IActionResult> Login([FromBody] LoginDto login)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            return BadRequest(ApiResponse<string>.ErrorResult("Validation failed", errors));
        }

        try
        {
            // 1. Nejdříve ověříme credentials přes MagentaTV API
            var loginSuccess = await _service.LoginAsync(login.Username, login.Password);

            if (!loginSuccess)
            {
                _logger.LogWarning("Login failed for user {Username} - invalid credentials", login.Username);
                return Unauthorized(ApiResponse<string>.ErrorResult("Invalid credentials",
                    new List<string> { "Chyba přihlášení - neplatné údaje" }));
            }

            // 2. Po úspěšném přihlášení vytvoříme session
            var createSessionRequest = new CreateSessionRequest
            {
                Username = login.Username,
                Password = login.Password, // Potřeba pro session vytvoření
                RememberMe = false,
                SessionDurationHours = 8
            };

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

            var sessionId = await _sessionManager.CreateSessionAsync(createSessionRequest, ipAddress, userAgent);

            // 3. Načteme tokeny z storage a uložíme je do session
            var tokens = await _tokenStorage.LoadTokensAsync();
            if (tokens?.IsValid == true)
            {
                await _sessionManager.RefreshSessionTokensAsync(sessionId, tokens);
            }

            // 4. Nastavíme session cookie
            SetSessionCookie(sessionId);

            _logger.LogInformation("User {Username} logged in successfully with session {SessionId}",
                login.Username, sessionId);

            return Ok(ApiResponse<string>.SuccessResult("Login successful", "Přihlášení proběhlo úspěšně"));
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Login failed for user {Username} - unauthorized", login.Username);
            return Unauthorized(ApiResponse<string>.ErrorResult("Invalid credentials",
                new List<string> { "Chyba přihlášení - neplatné údaje" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error for user {Username}", login.Username);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error",
                new List<string> { "Došlo k chybě při přihlašování" }));
        }
    }

    /// <summary>
    /// Odhlášení uživatele - ukončí session + vymaže tokeny
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var sessionId = GetSessionIdFromRequest();

            // 1. Ukončíme session
            if (!string.IsNullOrEmpty(sessionId))
            {
                await _sessionManager.RemoveSessionAsync(sessionId);
                RemoveSessionCookie();
            }

            // 2. Vymažeme MagentaTV tokeny
            await _tokenStorage.ClearTokensAsync();

            // 3. Zavoláme logout na Magenta service
            await _service.LogoutAsync();

            _logger.LogInformation("User logged out successfully");
            return Ok(ApiResponse<string>.SuccessResult("Logout successful", "Odhlášení proběhlo úspěšně"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout error");
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error",
                new List<string> { "Došlo k chybě při odhlašování" }));
        }
    }

    /// <summary>
    /// Získání stavu autentizace - kombinuje session + token data
    /// </summary>
    [HttpGet("auth/status")]
    [ProducesResponseType(typeof(ApiResponse<AuthStatusDto>), 200)]
    public async Task<IActionResult> GetAuthStatus()
    {
        try
        {
            var currentSession = HttpContext.GetCurrentSession();
            var tokens = await _tokenStorage.LoadTokensAsync();

            var status = new AuthStatusDto
            {
                IsAuthenticated = currentSession?.IsActive == true,
                Username = currentSession?.Username ?? tokens?.Username,
                ExpiresAt = currentSession?.ExpiresAt,
                IsExpired = currentSession?.IsExpired ?? true,
                TimeToExpiry = currentSession?.IsActive == true ? currentSession.TimeToExpiry : null
            };

            // Přidáme informace o MagentaTV tokenech
            if (tokens != null)
            {
                status.TimeToExpiry = tokens.IsValid ? tokens.TimeToExpiry : status.TimeToExpiry;
            }

            return Ok(ApiResponse<AuthStatusDto>.SuccessResult(status));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting auth status");
            return StatusCode(500, ApiResponse<AuthStatusDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// Získání seznamu kanálů - vyžaduje aktivní session
    /// </summary>
    [HttpGet("channels")]
    [ProducesResponseType(typeof(ApiResponse<List<ChannelDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    public async Task<IActionResult> GetChannels()
    {
        try
        {
            // Ověříme aktivní session
            var currentSession = HttpContext.RequireSession();

            // Ujistíme se, že máme platné tokeny
            await EnsureValidTokensAsync(currentSession);

            var channels = await _service.GetChannelsAsync();
            return Ok(ApiResponse<List<ChannelDto>>.SuccessResult(channels, $"Nalezeno {channels.Count} kanálů"));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required",
                new List<string> { "Vyžaduje přihlášení" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channels");
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error",
                new List<string> { "Došlo k chybě při načítání kanálů" }));
        }
    }

    /// <summary>
    /// Získání EPG pro kanál - vyžaduje aktivní session
    /// </summary>
    [HttpGet("epg/{channelId}")]
    [ProducesResponseType(typeof(ApiResponse<List<EpgItemDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    [ProducesResponseType(typeof(ApiResponse<string>), 404)]
    public async Task<IActionResult> GetEpg(int channelId, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        if (channelId <= 0)
        {
            return BadRequest(ApiResponse<string>.ErrorResult("Invalid channel ID",
                new List<string> { "ID kanálu musí být větší než 0" }));
        }

        try
        {
            // Ověříme aktivní session
            var currentSession = HttpContext.RequireSession();

            // Ujistíme se, že máme platné tokeny
            await EnsureValidTokensAsync(currentSession);

            var epg = await _service.GetEpgAsync(channelId, from, to);
            return Ok(ApiResponse<List<EpgItemDto>>.SuccessResult(epg,
                $"Nalezeno {epg.Count} pořadů pro kanál {channelId}"));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required",
                new List<string> { "Vyžaduje přihlášení" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting EPG for channel {ChannelId}", channelId);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error",
                new List<string> { "Došlo k chybě při načítání EPG" }));
        }
    }

    /// <summary>
    /// Získání stream URL pro kanál - vyžaduje aktivní session
    /// </summary>
    [HttpGet("stream/{channelId}")]
    [ProducesResponseType(typeof(ApiResponse<StreamUrlDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    [ProducesResponseType(typeof(ApiResponse<string>), 404)]
    public async Task<IActionResult> GetStreamUrl(int channelId)
    {
        if (channelId <= 0)
        {
            return BadRequest(ApiResponse<string>.ErrorResult("Invalid channel ID",
                new List<string> { "ID kanálu musí být větší než 0" }));
        }

        try
        {
            // Ověříme aktivní session
            var currentSession = HttpContext.RequireSession();

            // Ujistíme se, že máme platné tokeny
            await EnsureValidTokensAsync(currentSession);

            var url = await _service.GetStreamUrlAsync(channelId);
            if (string.IsNullOrEmpty(url))
            {
                return NotFound(ApiResponse<string>.ErrorResult("Stream not found",
                    new List<string> { "Stream pro tento kanál nebyl nalezen" }));
            }

            var streamData = new StreamUrlDto
            {
                ChannelId = channelId,
                StreamUrl = url,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30), // Stream URLs typically expire
                Type = "LIVE"
            };

            return Ok(ApiResponse<StreamUrlDto>.SuccessResult(streamData, "Stream URL získána"));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required",
                new List<string> { "Vyžaduje přihlášení" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stream URL for channel {ChannelId}", channelId);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error",
                new List<string> { "Došlo k chybě při získávání stream URL" }));
        }
    }

    /// <summary>
    /// Získání catchup stream URL - vyžaduje aktivní session
    /// </summary>
    [HttpGet("catchup/{scheduleId}")]
    [ProducesResponseType(typeof(ApiResponse<StreamUrlDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    [ProducesResponseType(typeof(ApiResponse<string>), 404)]
    public async Task<IActionResult> GetCatchupStream(long scheduleId)
    {
        if (scheduleId <= 0)
        {
            return BadRequest(ApiResponse<string>.ErrorResult("Invalid schedule ID",
                new List<string> { "ID pořadu musí být větší než 0" }));
        }

        try
        {
            // Ověříme aktivní session
            var currentSession = HttpContext.RequireSession();

            // Ujistíme se, že máme platné tokeny
            await EnsureValidTokensAsync(currentSession);

            var url = await _service.GetCatchupStreamUrlAsync(scheduleId);
            if (string.IsNullOrEmpty(url))
            {
                return NotFound(ApiResponse<string>.ErrorResult("Catchup stream not found",
                    new List<string> { "Catchup stream pro tento pořad nebyl nalezen" }));
            }

            var streamData = new StreamUrlDto
            {
                ScheduleId = scheduleId,
                StreamUrl = url,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                Type = "CATCHUP"
            };

            return Ok(ApiResponse<StreamUrlDto>.SuccessResult(streamData, "Catchup stream URL získána"));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required",
                new List<string> { "Vyžaduje přihlášení" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting catchup stream URL for schedule {ScheduleId}", scheduleId);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error",
                new List<string> { "Došlo k chybě při získávání catchup stream URL" }));
        }
    }

    /// <summary>
    /// Generování M3U playlistu - vyžaduje aktivní session
    /// </summary>
    [HttpGet("playlist")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    public async Task<IActionResult> GetPlaylist()
    {
        try
        {
            // Ověříme aktivní session
            var currentSession = HttpContext.RequireSession();

            // Ujistíme se, že máme platné tokeny
            await EnsureValidTokensAsync(currentSession);

            var playlist = await _service.GenerateM3UPlaylistAsync();
            var fileName = $"magentatv_playlist_{DateTime.Now:yyyyMMdd_HHmmss}.m3u";

            return File(System.Text.Encoding.UTF8.GetBytes(playlist), "audio/x-mpegurl", fileName);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required",
                new List<string> { "Vyžaduje přihlášení" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating playlist");
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error",
                new List<string> { "Došlo k chybě při generování playlistu" }));
        }
    }

    /// <summary>
    /// Export EPG ve formátu XMLTV - vyžaduje aktivní session
    /// </summary>
    [HttpGet("epgxml/{channelId}")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    [ProducesResponseType(typeof(ApiResponse<string>), 404)]
    public async Task<IActionResult> GetEpgXml(int channelId)
    {
        if (channelId <= 0)
        {
            return BadRequest(ApiResponse<string>.ErrorResult("Invalid channel ID",
                new List<string> { "ID kanálu musí být větší než 0" }));
        }

        try
        {
            // Ověříme aktivní session
            var currentSession = HttpContext.RequireSession();

            // Ujistíme se, že máme platné tokeny
            await EnsureValidTokensAsync(currentSession);

            var epg = await _service.GetEpgAsync(channelId);
            var xml = _service.GenerateXmlTv(epg, channelId);
            var fileName = $"epg_channel_{channelId}_{DateTime.Now:yyyyMMdd}.xml";

            return File(System.Text.Encoding.UTF8.GetBytes(xml), "application/xml", fileName);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required",
                new List<string> { "Vyžaduje přihlášení" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating EPG XML for channel {ChannelId}", channelId);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error",
                new List<string> { "Došlo k chybě při generování EPG XML" }));
        }
    }

    /// <summary>
    /// Ověření připojení k MagentaTV API - kombinuje session + token info
    /// </summary>
    [HttpGet("ping")]
    [ProducesResponseType(typeof(ApiResponse<PingResultDto>), 200)]
    public async Task<IActionResult> Ping()
    {
        try
        {
            var currentSession = HttpContext.GetCurrentSession();
            var hasValidTokens = await _tokenStorage.HasValidTokensAsync();
            var tokens = await _tokenStorage.LoadTokensAsync();

            var result = new PingResultDto
            {
                Timestamp = DateTime.UtcNow,
                Status = "OK",
                HasValidTokens = hasValidTokens && currentSession?.IsActive == true,
                Username = currentSession?.Username ?? tokens?.Username,
                TokenExpiresAt = tokens?.ExpiresAt
            };

            return Ok(ApiResponse<PingResultDto>.SuccessResult(result, "API je dostupná"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ping error");
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error"));
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Zajistí, že máme platné MagentaTV tokeny pro API volání
    /// </summary>
    private async Task EnsureValidTokensAsync(SessionData session)
    {
        var tokens = await _tokenStorage.LoadTokensAsync();

        // Pokud nemáme platné tokeny, pokusíme se o refresh nebo re-login
        if (tokens?.IsValid != true)
        {
            _logger.LogWarning("No valid tokens found for session {SessionId}, user {Username}",
                session.SessionId, session.Username);

            // Pokud máme refresh token, pokusíme se o refresh
            if (!string.IsNullOrEmpty(tokens?.RefreshToken))
            {
                try
                {
                    // TODO: Implementovat refresh token functionality v Magenta service
                    _logger.LogDebug("Attempting token refresh for user {Username}", session.Username);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Token refresh failed for user {Username}", session.Username);
                }
            }

            // Pokud stále nemáme platné tokeny, je potřeba re-login
            tokens = await _tokenStorage.LoadTokensAsync();
            if (tokens?.IsValid != true)
            {
                _logger.LogError("No valid tokens available for user {Username} in session {SessionId}",
                    session.Username, session.SessionId);
                throw new UnauthorizedAccessException("MagentaTV API tokens expired. Please login again.");
            }
        }

        // Aktualizujeme token info v session
        if (tokens.IsValid)
        {
            await _sessionManager.RefreshSessionTokensAsync(session.SessionId, tokens);
        }
    }

    private string? GetSessionIdFromRequest()
    {
        // Zkusíme cookie
        if (Request.Cookies.TryGetValue("SessionId", out var cookieValue))
        {
            return cookieValue;
        }

        // Zkusíme Authorization header
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Session "))
        {
            return authHeader.Substring("Session ".Length);
        }

        return null;
    }

    private void SetSessionCookie(string sessionId)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // HTTPS only v produkci
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(30) // Cookie expiruje později než session
        };

        Response.Cookies.Append("SessionId", sessionId, cookieOptions);
    }

    private void RemoveSessionCookie()
    {
        Response.Cookies.Delete("SessionId");
    }

    #endregion
}