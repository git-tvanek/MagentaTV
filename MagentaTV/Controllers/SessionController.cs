using MagentaTV.Models;
using MagentaTV.Models.Session;
using MagentaTV.Services.Session;
using Microsoft.AspNetCore.Mvc;

namespace MagentaTV.Controllers;

[ApiController]
[Route("sessions")]
public class SessionController : ControllerBase
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<SessionController> _logger;

    public SessionController(ISessionManager sessionManager, ILogger<SessionController> logger)
    {
        _sessionManager = sessionManager;
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

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

            var sessionId = await _sessionManager.CreateSessionAsync(request, ipAddress, userAgent);

            // Nastavíme session cookie
            SetSessionCookie(sessionId);

            var response = new SessionCreatedDto
            {
                SessionId = sessionId,
                Message = "Session created successfully",
                ExpiresAt = DateTime.UtcNow.AddHours(
                    request.RememberMe ? 720 : request.SessionDurationHours ?? 8)
            };

            _logger.LogInformation("Session created successfully for user {Username}: {SessionId}",
                request.Username, sessionId);

            return Ok(ApiResponse<SessionCreatedDto>.SuccessResult(response, "Přihlášení proběhlo úspěšně"));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Session creation failed - unauthorized: {Message}", ex.Message);
            return Unauthorized(ApiResponse<string>.ErrorResult("Invalid credentials",
                new List<string> { "Neplatné přihlašovací údaje" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create session for user {Username}", request.Username);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error",
                new List<string> { "Došlo k chybě při vytváření session" }));
        }
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

        try
        {
            var sessionInfo = await _sessionManager.GetSessionInfoAsync(sessionId);
            if (sessionInfo == null)
            {
                RemoveSessionCookie();
                return Unauthorized(ApiResponse<string>.ErrorResult("Session not found"));
            }

            if (!sessionInfo.IsExpired)
            {
                await _sessionManager.UpdateSessionActivityAsync(sessionId);
            }

            return Ok(ApiResponse<SessionInfoDto>.SuccessResult(sessionInfo));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get session info for {SessionId}", sessionId);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error"));
        }
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

        try
        {
            var currentSession = await _sessionManager.GetSessionAsync(currentSessionId);
            if (currentSession?.IsActive != true)
            {
                return Unauthorized(ApiResponse<string>.ErrorResult("Invalid session"));
            }

            var userSessions = await _sessionManager.GetUserSessionsAsync(currentSession.Username);
            var sessionDtos = userSessions.Select(s => new SessionDto
            {
                SessionId = s.SessionId,
                Username = s.Username,
                CreatedAt = s.CreatedAt,
                LastActivity = s.LastActivity,
                ExpiresAt = s.ExpiresAt,
                IpAddress = s.IpAddress,
                UserAgent = s.UserAgent,
                Status = s.Status
            }).ToList();

            return Ok(ApiResponse<List<SessionDto>>.SuccessResult(sessionDtos,
                $"Nalezeno {sessionDtos.Count} sessions"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user sessions for session {SessionId}", currentSessionId);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// Ukončí current session (odhlášení)
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse<string>), 200)]
    public async Task<IActionResult> Logout()
    {
        var sessionId = GetSessionIdFromRequest();
        if (string.IsNullOrEmpty(sessionId))
        {
            return Ok(ApiResponse<string>.SuccessResult("No active session"));
        }

        try
        {
            await _sessionManager.RemoveSessionAsync(sessionId);
            RemoveSessionCookie();

            _logger.LogInformation("Session logged out: {SessionId}", sessionId);
            return Ok(ApiResponse<string>.SuccessResult("Logout successful", "Odhlášení proběhlo úspěšně"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to logout session {SessionId}", sessionId);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error"));
        }
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

        try
        {
            var currentSession = await _sessionManager.GetSessionAsync(currentSessionId);
            var targetSession = await _sessionManager.GetSessionAsync(sessionId);

            if (currentSession?.IsActive != true)
            {
                return Unauthorized(ApiResponse<string>.ErrorResult("Invalid current session"));
            }

            if (targetSession == null)
            {
                return NotFound(ApiResponse<string>.ErrorResult("Target session not found"));
            }

            // Uživatel může rušit pouze své vlastní sessions
            if (!currentSession.Username.Equals(targetSession.Username, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            await _sessionManager.RemoveSessionAsync(sessionId);

            _logger.LogInformation("Session {SessionId} revoked by user {Username}",
                sessionId, currentSession.Username);

            return Ok(ApiResponse<string>.SuccessResult("Session revoked successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke session {SessionId}", sessionId);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error"));
        }
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

        try
        {
            var currentSession = await _sessionManager.GetSessionAsync(currentSessionId);
            if (currentSession?.IsActive != true)
            {
                return Unauthorized(ApiResponse<string>.ErrorResult("Invalid session"));
            }

            var userSessions = await _sessionManager.GetUserSessionsAsync(currentSession.Username);
            var otherSessions = userSessions.Where(s => s.SessionId != currentSessionId).ToList();

            foreach (var session in otherSessions)
            {
                await _sessionManager.RemoveSessionAsync(session.SessionId);
            }

            _logger.LogInformation("All other sessions logged out for user {Username}, kept current: {SessionId}",
                currentSession.Username, currentSessionId);

            return Ok(ApiResponse<string>.SuccessResult("All other sessions logged out",
                $"Ukončeno {otherSessions.Count} ostatních sessions"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to logout all other sessions for {SessionId}", currentSessionId);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error"));
        }
    }

    /// <summary>
    /// Získá statistiky sessions (admin endpoint)
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ApiResponse<SessionStatistics>), 200)]
    public async Task<IActionResult> GetStatistics()
    {
        try
        {
            var stats = await _sessionManager.GetStatisticsAsync();
            return Ok(ApiResponse<SessionStatistics>.SuccessResult(stats));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get session statistics");
            return StatusCode(500, ApiResponse<string>.ErrorResult("Internal server error"));
        }
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
