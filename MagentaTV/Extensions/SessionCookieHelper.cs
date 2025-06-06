using Microsoft.AspNetCore.Http;

namespace MagentaTV.Extensions;

/// <summary>
/// Helper methods for working with session cookies
/// </summary>
public static class SessionCookieHelper
{
    /// <summary>
    /// Gets the session id from the request cookie or Authorization header
    /// </summary>
    public static string? GetSessionId(HttpRequest request)
    {
        // Try cookie first
        if (request.Cookies.TryGetValue("SessionId", out var cookieValue))
        {
            return cookieValue;
        }

        // Fallback to Authorization header
        var authHeader = request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Session "))
        {
            return authHeader.Substring("Session ".Length);
        }

        return null;
    }

    /// <summary>
    /// Sets the session cookie on the response
    /// </summary>
    public static void SetSessionCookie(HttpResponse response, string sessionId, bool secure)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        };

        response.Cookies.Append("SessionId", sessionId, cookieOptions);
    }

    /// <summary>
    /// Removes the session cookie from the response
    /// </summary>
    public static void RemoveSessionCookie(HttpResponse response)
    {
        response.Cookies.Delete("SessionId");
    }
}
