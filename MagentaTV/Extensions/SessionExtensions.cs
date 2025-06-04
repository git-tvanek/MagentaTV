using MagentaTV.Services.Session;

namespace MagentaTV.Extensions;

/// <summary>
/// Extension methods pro práci se sessions v controllerech
/// </summary>
public static class SessionExtensions
{
    /// <summary>
    /// Získá current session data z HttpContext
    /// </summary>
    public static SessionData? GetCurrentSession(this HttpContext context)
    {
        return context.Items["CurrentSession"] as SessionData;
    }

    /// <summary>
    /// Získá username současného uživatele
    /// </summary>
    public static string? GetCurrentUsername(this HttpContext context)
    {
        return context.Items["CurrentUsername"] as string;
    }

    /// <summary>
    /// Zkontroluje jestli je uživatel přihlášený
    /// </summary>
    public static bool IsAuthenticated(this HttpContext context)
    {
        var session = context.GetCurrentSession();
        return session?.IsActive == true;
    }

    /// <summary>
    /// Vyžaduje aktivní session (throwne exception pokud není)
    /// </summary>
    public static SessionData RequireSession(this HttpContext context)
    {
        var session = context.GetCurrentSession();
        if (session?.IsActive != true)
        {
            throw new UnauthorizedAccessException("Active session required");
        }
        return session;
    }
}