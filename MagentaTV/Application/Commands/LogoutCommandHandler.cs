using MagentaTV.Services.Session;
using MagentaTV.Services.TokenStorage;
using MagentaTV.Services;
using MediatR;
using MagentaTV.Application.Events;
using MagentaTV.Models;

namespace MagentaTV.Application.Commands
{
    public class LogoutCommandHandler : IRequestHandler<LogoutCommand, ApiResponse<string>>
    {
        private readonly ISessionManager _sessionManager;
        private readonly ITokenStorage _tokenStorage;
        private readonly IMagenta _magentaService;
        private readonly IMediator _mediator;
        private readonly ILogger<LogoutCommandHandler> _logger;

        public LogoutCommandHandler(
            ISessionManager sessionManager,
            ITokenStorage tokenStorage,
            IMagenta magentaService,
            IMediator mediator,
            ILogger<LogoutCommandHandler> logger)
        {
            _sessionManager = sessionManager;
            _tokenStorage = tokenStorage;
            _magentaService = magentaService;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<ApiResponse<string>> Handle(LogoutCommand request, CancellationToken cancellationToken)
        {
            try
            {
                string? username = null;

                // 1. Ukončíme session pokud existuje
                if (!string.IsNullOrEmpty(request.SessionId))
                {
                    var session = await _sessionManager.GetSessionAsync(request.SessionId);
                    username = session?.Username;

                    await _sessionManager.RemoveSessionAsync(request.SessionId);
                }

                // 2. Vymažeme MagentaTV tokeny
                await _tokenStorage.ClearTokensAsync();

                // 3. Zavoláme logout na Magenta service
                await _magentaService.LogoutAsync();

                // 4. Publikujeme event
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(request.SessionId))
                {
                    await _mediator.Publish(new UserLoggedOutEvent
                    {
                        Username = username,
                        SessionId = request.SessionId,
                        Timestamp = DateTime.UtcNow,
                        Reason = "Voluntary"
                    }, cancellationToken);
                }

                _logger.LogInformation("User logged out successfully");
                return ApiResponse<string>.SuccessResult("Logout successful", "Odhlášení proběhlo úspěšně");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout error");
                return ApiResponse<string>.ErrorResult("Internal server error",
                    new List<string> { "Došlo k chybě při odhlašování" });
            }
        }
    }
}
