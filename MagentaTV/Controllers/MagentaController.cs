using System.Collections.Generic;
using MagentaTV.Application.Commands;
using MagentaTV.Application.Queries;
using MagentaTV.Extensions;
using MagentaTV.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MagentaTV.Controllers;

[ApiController]
[Route("magenta")]
/// <summary>
/// API endpoints that act as a thin wrapper over the underlying MagentaTV
/// service. Requires an active session for most operations.
/// </summary>
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
    /// Returns authentication status by combining current session details and
    /// stored token information.
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
    /// Retrieves the list of available channels. Requires an active session.
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
    /// Returns the Electronic Program Guide for the specified channel. Requires
    /// an active session.
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
    /// Returns the Electronic Program Guide for multiple channels.
    /// </summary>
    [HttpGet("epg/bulk")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<int, List<EpgItemDto>>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    public async Task<IActionResult> GetEpgBulk([FromQuery(Name = "ids")] string ids, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        if (string.IsNullOrWhiteSpace(ids))
        {
            return BadRequest(ApiResponse<string>.ErrorResult("Invalid channel IDs"));
        }

        var parsed = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();

        if (parsed.Count == 0)
        {
            return BadRequest(ApiResponse<string>.ErrorResult("Invalid channel IDs"));
        }

        try
        {
            var query = new GetBulkEpgQuery
            {
                ChannelIds = parsed,
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
    /// Retrieves the streaming URL for the given channel. An active session is
    /// required to access the stream.
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
    /// Retrieves the catch-up streaming URL for the specified schedule entry.
    /// Requires an active session.
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
    /// Generates an M3U playlist for the authenticated user. Requires an active
    /// session to obtain channel data and streaming URLs.
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
    /// Exports the EPG for a channel as an XMLTV document. Requires an active
    /// session.
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
    /// Performs a connectivity check against the MagentaTV API and returns
    /// information about the current session and token validity.
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