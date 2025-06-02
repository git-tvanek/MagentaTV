using MagentaTV.Models;
using MagentaTV.Services;
using MagentaTV.Services.TokenStorage;
using Microsoft.AspNetCore.Mvc;

namespace MagentaTV.Controllers;

[ApiController]
[Route("magenta")]
public class MagentaController : ControllerBase
{
    private readonly IMagenta _service;
    private readonly ITokenStorage _tokenStorage;
    private readonly ILogger<MagentaController> _logger;

    public MagentaController(IMagenta service, ITokenStorage tokenStorage, ILogger<MagentaController> logger)
    {
        _service = service;
        _tokenStorage = tokenStorage;
        _logger = logger;
    }

    /// <summary>
    /// Přihlášení uživatele - tokeny se automaticky ukládají
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
            var success = await _service.LoginAsync(login.Username, login.Password);

            if (success)
            {
                _logger.LogInformation("User {Username} logged in successfully", login.Username);
                return Ok(ApiResponse<string>.SuccessResult("Login successful", "Přihlášení proběhlo úspěšně"));
            }
            else
            {
                _logger.LogWarning("Login failed for user {Username}", login.Username);
                return Unauthorized(ApiResponse<string>.ErrorResult("Invalid credentials", new List<string> { "Chyba přihlášení - neplatné údaje" }));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error for user {Username}", login.Username);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error", new List<string> { "Došlo k chybě při přihlašování" }));
        }
    }

    /// <summary>
    /// Odhlášení uživatele - vymaže uložené tokeny
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await _tokenStorage.ClearTokensAsync();
            _logger.LogInformation("User logged out successfully");
            return Ok(ApiResponse<string>.SuccessResult("Logout successful", "Odhlášení proběhlo úspěšně"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout error");
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error", new List<string> { "Došlo k chybě při odhlašování" }));
        }
    }

    /// <summary>
    /// Získání stavu autentizace
    /// </summary>
    [HttpGet("auth/status")]
    [ProducesResponseType(typeof(ApiResponse<AuthStatusDto>), 200)]
    public async Task<IActionResult> GetAuthStatus()
    {
        try
        {
            var tokens = await _tokenStorage.LoadTokensAsync();
            var isAuthenticated = tokens?.IsValid == true;

            var status = new AuthStatusDto
            {
                IsAuthenticated = isAuthenticated,
                Username = tokens?.Username,
                ExpiresAt = tokens?.ExpiresAt,
                IsExpired = tokens?.IsExpired ?? true,
                TimeToExpiry = tokens?.IsValid == true ? tokens.ExpiresAt - DateTime.UtcNow : null
            };

            return Ok(ApiResponse<AuthStatusDto>.SuccessResult(status));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting auth status");
            return StatusCode(500, ApiResponse<AuthStatusDto>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// Získání seznamu kanálů
    /// </summary>
    [HttpGet("channels")]
    [ProducesResponseType(typeof(ApiResponse<List<ChannelDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    public async Task<IActionResult> GetChannels()
    {
        try
        {
            var channels = await _service.GetChannelsAsync();
            return Ok(ApiResponse<List<ChannelDto>>.SuccessResult(channels, $"Nalezeno {channels.Count} kanálů"));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required", new List<string> { "Vyžaduje přihlášení" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channels");
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error", new List<string> { "Došlo k chybě při načítání kanálů" }));
        }
    }

    /// <summary>
    /// Získání EPG pro kanál
    /// </summary>
    [HttpGet("epg/{channelId}")]
    [ProducesResponseType(typeof(ApiResponse<List<EpgItemDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    [ProducesResponseType(typeof(ApiResponse<string>), 404)]
    public async Task<IActionResult> GetEpg(int channelId, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        if (channelId <= 0)
        {
            return BadRequest(ApiResponse<string>.ErrorResult("Invalid channel ID", new List<string> { "ID kanálu musí být větší než 0" }));
        }

        try
        {
            var epg = await _service.GetEpgAsync(channelId, from, to);
            return Ok(ApiResponse<List<EpgItemDto>>.SuccessResult(epg, $"Nalezeno {epg.Count} pořadů pro kanál {channelId}"));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required", new List<string> { "Vyžaduje přihlášení" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting EPG for channel {ChannelId}", channelId);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error", new List<string> { "Došlo k chybě při načítání EPG" }));
        }
    }

    /// <summary>
    /// Získání stream URL pro kanál
    /// </summary>
    [HttpGet("stream/{channelId}")]
    [ProducesResponseType(typeof(ApiResponse<StreamUrlDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    [ProducesResponseType(typeof(ApiResponse<string>), 404)]
    public async Task<IActionResult> GetStreamUrl(int channelId)
    {
        if (channelId <= 0)
        {
            return BadRequest(ApiResponse<string>.ErrorResult("Invalid channel ID", new List<string> { "ID kanálu musí být větší než 0" }));
        }

        try
        {
            var url = await _service.GetStreamUrlAsync(channelId);
            if (string.IsNullOrEmpty(url))
            {
                return NotFound(ApiResponse<string>.ErrorResult("Stream not found", new List<string> { "Stream pro tento kanál nebyl nalezen" }));
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
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required", new List<string> { "Vyžaduje přihlášení" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stream URL for channel {ChannelId}", channelId);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error", new List<string> { "Došlo k chybě při získávání stream URL" }));
        }
    }

    /// <summary>
    /// Získání catchup stream URL
    /// </summary>
    [HttpGet("catchup/{scheduleId}")]
    [ProducesResponseType(typeof(ApiResponse<StreamUrlDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    [ProducesResponseType(typeof(ApiResponse<string>), 404)]
    public async Task<IActionResult> GetCatchupStream(long scheduleId)
    {
        if (scheduleId <= 0)
        {
            return BadRequest(ApiResponse<string>.ErrorResult("Invalid schedule ID", new List<string> { "ID pořadu musí být větší než 0" }));
        }

        try
        {
            var url = await _service.GetCatchupStreamUrlAsync(scheduleId);
            if (string.IsNullOrEmpty(url))
            {
                return NotFound(ApiResponse<string>.ErrorResult("Catchup stream not found", new List<string> { "Catchup stream pro tento pořad nebyl nalezen" }));
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
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required", new List<string> { "Vyžaduje přihlášení" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting catchup stream URL for schedule {ScheduleId}", scheduleId);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error", new List<string> { "Došlo k chybě při získávání catchup stream URL" }));
        }
    }

    /// <summary>
    /// Generování M3U playlistu
    /// </summary>
    [HttpGet("playlist")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    public async Task<IActionResult> GetPlaylist()
    {
        try
        {
            var playlist = await _service.GenerateM3UPlaylistAsync();
            var fileName = $"magentatv_playlist_{DateTime.Now:yyyyMMdd_HHmmss}.m3u";

            return File(System.Text.Encoding.UTF8.GetBytes(playlist), "audio/x-mpegurl", fileName);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required", new List<string> { "Vyžaduje přihlášení" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating playlist");
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error", new List<string> { "Došlo k chybě při generování playlistu" }));
        }
    }

    /// <summary>
    /// Export EPG ve formátu XMLTV
    /// </summary>
    [HttpGet("epgxml/{channelId}")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    [ProducesResponseType(typeof(ApiResponse<string>), 404)]
    public async Task<IActionResult> GetEpgXml(int channelId)
    {
        if (channelId <= 0)
        {
            return BadRequest(ApiResponse<string>.ErrorResult("Invalid channel ID", new List<string> { "ID kanálu musí být větší než 0" }));
        }

        try
        {
            var epg = await _service.GetEpgAsync(channelId);
            var xml = _service.GenerateXmlTv(epg, channelId);
            var fileName = $"epg_channel_{channelId}_{DateTime.Now:yyyyMMdd}.xml";

            return File(System.Text.Encoding.UTF8.GetBytes(xml), "application/xml", fileName);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required", new List<string> { "Vyžaduje přihlášení" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating EPG XML for channel {ChannelId}", channelId);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error", new List<string> { "Došlo k chybě při generování EPG XML" }));
        }
    }

    /// <summary>
    /// Ověření připojení k MagentaTV API
    /// </summary>
    [HttpGet("ping")]
    [ProducesResponseType(typeof(ApiResponse<PingResultDto>), 200)]
    public async Task<IActionResult> Ping()
    {
        try
        {
            var hasTokens = await _tokenStorage.HasValidTokensAsync();
            var tokens = await _tokenStorage.LoadTokensAsync();

            var result = new PingResultDto
            {
                Timestamp = DateTime.UtcNow,
                Status = "OK",
                HasValidTokens = hasTokens,
                Username = tokens?.Username,
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
}