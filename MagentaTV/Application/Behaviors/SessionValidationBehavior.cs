using MagentaTV.Services.Session;
using MediatR;

namespace MagentaTV.Application.Behaviors
{
    public class SessionValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ISessionManager _sessionManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<SessionValidationBehavior<TRequest, TResponse>> _logger;

        public SessionValidationBehavior(
            ISessionManager sessionManager,
            IHttpContextAccessor httpContextAccessor,
            ILogger<SessionValidationBehavior<TRequest, TResponse>> logger)
        {
            _sessionManager = sessionManager;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            // Přeskoč validaci pro login a public endpoints
            var requestName = typeof(TRequest).Name;
            if (requestName == "LoginCommand" || requestName.StartsWith("Public"))
            {
                return await next();
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                throw new UnauthorizedAccessException("No HTTP context available");
            }

            var sessionId = GetSessionId(httpContext);
            if (string.IsNullOrEmpty(sessionId))
            {
                throw new UnauthorizedAccessException("No session found");
            }

            var isValid = await _sessionManager.ValidateSessionAsync(sessionId);
            if (!isValid)
            {
                throw new UnauthorizedAccessException("Invalid session");
            }

            // Načti session data pro pozdější použití
            _logger.LogDebug("Loading session data for {SessionId}", sessionId);
            var sessionData = await _sessionManager.GetSessionAsync(sessionId);
            if (sessionData == null)
            {
                _logger.LogWarning("Session {SessionId} validated but no data found", sessionId);
            }
            else
            {
                httpContext.Items["CurrentSession"] = sessionData;
                httpContext.Items["CurrentUsername"] = sessionData.Username;
                _logger.LogDebug("Session data set in HttpContext for {Username}", sessionData.Username);
            }

            // Aktualizuj aktivitu
            await _sessionManager.UpdateSessionActivityAsync(sessionId);

            return await next();
        }

        private string? GetSessionId(HttpContext context)
        {
            // Cookie nebo Authorization header
            if (context.Request.Cookies.TryGetValue("SessionId", out var cookieValue))
                return cookieValue;

            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Session "))
                return authHeader.Substring("Session ".Length);

            return null;
        }
    }
}

