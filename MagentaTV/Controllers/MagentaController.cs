using MagentaTV.Application.Commands;
using MagentaTV.Application.Queries;
using MagentaTV.Extensions;
using MagentaTV.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MagentaTV.Controllers;

[ApiController]
[Route("magenta")]
public class MagentaController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<MagentaController> _logger;

    public MagentaController(IMediator mediator, ILogger<MagentaController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Získání stavu autentizace - kombinuje session + token data
    /// </summary>
    [HttpGet("auth/status")]
    [ProducesResponseType(typeof(ApiResponse<AuthStatusDto>), 200)]
    public async Task<IActionResult> GetAuthStatus()
    {
        var query = new GetAuthStatusQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
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
            // Session validation je řešena v SessionValidationBehavior
            var query = new GetChannelsQuery();
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required",
                new List<string> { "Vyžaduje přihlášení" }));
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
            var query = new GetEpgQuery
            {
                ChannelId = channelId,
                From = from,
                To = to
            };
            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required",
                new List<string> { "Vyžaduje přihlášení" }));
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
            var query = new GetStreamUrlQuery { ChannelId = channelId };
            var result = await _mediator.Send(query);

            if (!result.Success && result.Message?.Contains("not found") == true)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required",
                new List<string> { "Vyžaduje přihlášení" }));
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
            var query = new GetCatchupStreamQuery { ScheduleId = scheduleId };
            var result = await _mediator.Send(query);

            if (!result.Success && result.Message?.Contains("not found") == true)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required",
                new List<string> { "Vyžaduje přihlášení" }));
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
            var query = new GeneratePlaylistQuery();
            var playlist = await _mediator.Send(query);
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
            var query = new GenerateEpgXmlQuery { ChannelId = channelId };
            var xml = await _mediator.Send(query);
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
        var query = new PingQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    
}