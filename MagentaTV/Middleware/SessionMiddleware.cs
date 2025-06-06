using MagentaTV.Services.Session;
using MagentaTV.Extensions;

namespace MagentaTV.Middleware;

/// <summary>
/// Middleware pro automatickou validaci a aktualizaci sessions
/// </summary>
public class SessionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionMiddleware> _logger;

    public SessionMiddleware(RequestDelegate next, ILogger<SessionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISessionManager sessionManager)
    {
        // Získáme session ID z requestu
        var sessionId = SessionCookieHelper.GetSessionId(context.Request);

        if (!string.IsNullOrEmpty(sessionId))
        {
            try
            {
                // Ověříme session
                var isValid = await sessionManager.ValidateSessionAsync(sessionId);

                if (isValid)
                {
                    // Aktualizujeme aktivitu
                    await sessionManager.UpdateSessionActivityAsync(sessionId);

                    // Přidáme session info do HttpContext pro použití v controllerech
                    var sessionData = await sessionManager.GetSessionAsync(sessionId);
                    if (sessionData != null)
                    {
                        context.Items["CurrentSession"] = sessionData;
                        context.Items["CurrentUsername"] = sessionData.Username;
                    }
                }
                else
                {
                    // Neplatná session - odstraníme cookie
                    SessionCookieHelper.RemoveSessionCookie(context.Response);
                    _logger.LogDebug("Invalid session removed: {SessionId}", sessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Session validation failed for {SessionId}", sessionId);
            }
        }

        await _next(context);
    }
}
