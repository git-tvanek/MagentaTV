using MagentaTV.Application.Commands;
using MagentaTV.Application.Queries;
using MagentaTV.Models;
using MagentaTV.Models.Session;
using MagentaTV.Services.Session;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MagentaTV.Controllers;

[ApiController]
[Route("sessions")]
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
    /// Vytvoří novou session (přihlášení)
    /// </summary>
    [HttpPost("create")]
    [ProducesResponseType(typeof(ApiResponse<SessionCreatedDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 400)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse<string>.ErrorResult("Validation failed", errors));
        }

        var command = new CreateSessionCommand
        {
            Request = request,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent = HttpContext.Request.Headers.UserAgent.ToString()
        };

        var result = await _mediator.Send(command);

        if (result.Success)
        {
            // Nastavíme session cookie
            SetSessionCookie(result.Data!.SessionId);
            return Ok(result);
        }

        return result.Message?.Contains("Invalid credentials") == true
            ? Unauthorized(result)
            : StatusCode(500, result);
    }

    /// <summary>
    /// Získá informace o current session
    /// </summary>
    [HttpGet("current")]
    [ProducesResponseType(typeof(ApiResponse<SessionInfoDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    public async Task<IActionResult> GetCurrentSession()
    {
        var sessionId = GetSessionIdFromRequest();
        if (string.IsNullOrEmpty(sessionId))
        {
            return Unauthorized(ApiResponse<string>.ErrorResult("No active session"));
        }

        var query = new GetCurrentSessionQuery { SessionId = sessionId };
        var result = await _mediator.Send(query);

        if (!result.Success)
        {
            RemoveSessionCookie();
            return Unauthorized(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Získá všechny sessions současného uživatele
    /// </summary>
    [HttpGet("user")]
    [ProducesResponseType(typeof(ApiResponse<List<SessionDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    public async Task<IActionResult> GetUserSessions()
    {
        var currentSessionId = GetSessionIdFromRequest();
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
    /// Ukončí current session (odhlášení)
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    public async Task<IActionResult> Logout()
    {
        var sessionId = GetSessionIdFromRequest();
        var command = new SessionLogoutCommand { SessionId = sessionId };
        var result = await _mediator.Send(command);

        if (result.Success)
        {
            RemoveSessionCookie();
        }

        return Ok(result);
    }

    /// <summary>
    /// Ukončí konkrétní session
    /// </summary>
    [HttpPost("revoke/{sessionId}")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    [ProducesResponseType(typeof(ApiResponse<string>), 403)]
    public async Task<IActionResult> RevokeSession(string sessionId)
    {
        var currentSessionId = GetSessionIdFromRequest();
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
    /// Ukončí všechny sessions uživatele kromě současné
    /// </summary>
    [HttpPost("logout-all")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    [ProducesResponseType(typeof(ApiResponse<string>), 401)]
    public async Task<IActionResult> LogoutAllOtherSessions()
    {
        var currentSessionId = GetSessionIdFromRequest();
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
    /// Získá statistiky sessions (admin endpoint)
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApiResponse<SessionStatistics>), 200)]
    public async Task<IActionResult> GetStatistics()
    {
        var query = new GetSessionStatisticsQuery();
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    #region Helper Methods

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