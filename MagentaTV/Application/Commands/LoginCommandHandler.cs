using MagentaTV.Models.Session;
using MagentaTV.Models;
using MagentaTV.Services.Session;
using MagentaTV.Services.TokenStorage;
using MagentaTV.Services;
using MediatR;
using MagentaTV.Application.Events;

namespace MagentaTV.Application.Commands
{
    public class LoginCommandHandler : IRequestHandler<LoginCommand, ApiResponse<string>>
    {
        private readonly IMagenta _magentaService;
        private readonly ISessionManager _sessionManager;
        private readonly ITokenStorage _tokenStorage;
        private readonly IMediator _mediator;
        private readonly ILogger<LoginCommandHandler> _logger;

        public LoginCommandHandler(
            IMagenta magentaService,
            ISessionManager sessionManager,
            ITokenStorage tokenStorage,
            IMediator mediator,
            ILogger<LoginCommandHandler> logger)
        {
            _magentaService = magentaService;
            _sessionManager = sessionManager;
            _tokenStorage = tokenStorage;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<ApiResponse<string>> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            try
            {
                // 1. Ověř credentials
                var loginSuccess = await _magentaService.LoginAsync(request.Username, request.Password);
                if (!loginSuccess)
                {
                    return ApiResponse<string>.ErrorResult("Invalid credentials");
                }

                // 2. Vytvoř session
                var createSessionRequest = new CreateSessionRequest
                {
                    Username = request.Username,
                    Password = request.Password,
                    RememberMe = false,
                    SessionDurationHours = 8
                };

                var sessionId = await _sessionManager.CreateSessionAsync(
                    createSessionRequest, request.IpAddress, request.UserAgent);

                // 3. Načti a ulož tokeny
                var tokens = await _tokenStorage.LoadTokensAsync();
                if (tokens?.IsValid == true)
                {
                    await _sessionManager.RefreshSessionTokensAsync(sessionId, tokens);
                }

                // 4. Publikuj event
                await _mediator.Publish(new UserLoggedInEvent
                {
                    Username = request.Username,
                    SessionId = sessionId,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = request.IpAddress
                }, cancellationToken);

                return ApiResponse<string>.SuccessResult("Login successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed for user {Username}", request.Username);
                return ApiResponse<string>.ErrorResult("Login failed");
            }
        }
    }
}

