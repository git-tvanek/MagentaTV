using MagentaTV.Application.Commands;
using MagentaTV.Application.Queries;
using MagentaTV.Models;
using MagentaTV.Models.Session;
using MagentaTV.Services.Session;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MagentaTV.Extensions;

namespace MagentaTV.Controllers;

[ApiController]
[Route("sessions")]
/// <summary>
/// Provides endpoints for inspecting and manipulating user sessions. These
/// endpoints require a valid session cookie and are primarily used for
/// session management in the UI.
/// </summary>
public class SessionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SessionController> _logger;

    public SessionController(IMediator mediator, ILogger<SessionController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }


    /// <summary>
    /// Returns details for the currently active session.
    /// </summary>
    [HttpGet("current")]
    [ProducesResponseType(typeof(ApiResponse<SessionInfoDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    public async Task<IActionResult> GetCurrentSession()
    {
        var sessionId = SessionCookieHelper.GetSessionId(Request);
        if (string.IsNullOrEmpty(sessionId))
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("No active session"));
        }

        var query = new GetCurrentSessionQuery { SessionId = sessionId };
        var result = await _mediator.Send(query);

        if (!result.Success)
        {
            SessionCookieHelper.RemoveSessionCookie(Response);
            return Unauthorized(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Retrieves a list of all sessions associated with the current user.
    /// </summary>
    [HttpGet("user")]
    [ProducesResponseType(typeof(ApiResponse<List<SessionDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    public async Task<IActionResult> GetUserSessions()
    {
        var currentSessionId = SessionCookieHelper.GetSessionId(Request);
        if (string.IsNullOrEmpty(currentSessionId))
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required"));
        }

        var query = new GetUserSessionsQuery { CurrentSessionId = currentSessionId };
        var result = await _mediator.Send(query);

        if (!result.Success)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }


    /// <summary>
    /// Revokes a specific session identified by <paramref name="sessionId"/>.
    /// </summary>
    [HttpPost("revoke/{sessionId}")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    [ProducesResponseType(typeof(ApiResponse<string>), 403)]
    public async Task<IActionResult> RevokeSession(string sessionId)
    {
        var currentSessionId = SessionCookieHelper.GetSessionId(Request);
        if (string.IsNullOrEmpty(currentSessionId))
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required"));
        }

        var command = new RevokeSessionCommand
        {
            CurrentSessionId = currentSessionId,
            TargetSessionId = sessionId
        };

        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            if (result.Message?.Contains("Invalid current session") == true)
                return Unauthorized(result);
            if (result.Message?.Contains("Forbidden") == true)
                return Forbid();
            if (result.Message?.Contains("not found") == true)
                return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Logs out every session for the current user except the one making the
    /// request.
    /// </summary>
    [HttpPost("logout-all")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    public async Task<IActionResult> LogoutAllOtherSessions()
    {
        var currentSessionId = SessionCookieHelper.GetSessionId(Request);
        if (string.IsNullOrEmpty(currentSessionId))
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("Authentication required"));
        }

        var command = new LogoutAllOtherSessionsCommand { CurrentSessionId = currentSessionId };
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Retrieves aggregated session statistics. Intended for administrative use.
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApiResponse<SessionStatistics>), 200)]
    public async Task<IActionResult> GetStatistics()
    {
        var query = new GetSessionStatisticsQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    
}