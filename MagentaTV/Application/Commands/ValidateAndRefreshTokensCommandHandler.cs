using MagentaTV.Services.Session;
using MagentaTV.Services.TokenStorage;
using MediatR;

namespace MagentaTV.Application.Commands
{
    internal class ValidateAndRefreshTokensCommandHandler : IRequestHandler<ValidateAndRefreshTokensCommand>
    {
        private readonly ITokenStorage _tokenStorage;
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<ValidateAndRefreshTokensCommandHandler> _logger;

        public ValidateAndRefreshTokensCommandHandler(
            ITokenStorage tokenStorage,
            ISessionManager sessionManager,
            ILogger<ValidateAndRefreshTokensCommandHandler> logger)
        {
            _tokenStorage = tokenStorage;
            _sessionManager = sessionManager;
            _logger = logger;
        }

        public async Task Handle(ValidateAndRefreshTokensCommand request, CancellationToken cancellationToken)
        {
            var tokens = await _tokenStorage.LoadTokensAsync();

            if (tokens?.IsValid != true)
            {
                _logger.LogWarning("No valid tokens found for session {SessionId}, user {Username}",
                    request.Session.SessionId, request.Session.Username);

                if (!string.IsNullOrEmpty(tokens?.RefreshToken))
                {
                    try
                    {
                        // TODO: Implementovat refresh token functionality v Magenta service
                        _logger.LogDebug("Attempting token refresh for user {Username}", request.Session.Username);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Token refresh failed for user {Username}", request.Session.Username);
                    }
                }

                tokens = await _tokenStorage.LoadTokensAsync();
                if (tokens?.IsValid != true)
                {
                    _logger.LogError("No valid tokens available for user {Username} in session {SessionId}",
                        request.Session.Username, request.Session.SessionId);
                    throw new UnauthorizedAccessException("MagentaTV API tokens expired. Please login again.");
                }
            }

            if (tokens.IsValid)
            {
                await _sessionManager.RefreshSessionTokensAsync(request.Session.SessionId, tokens);
            }
        }
    }
}