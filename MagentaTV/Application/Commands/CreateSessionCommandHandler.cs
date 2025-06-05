using MagentaTV.Models;
using MagentaTV.Models.Session;
using MagentaTV.Services.Session;
using MediatR;
using MagentaTV.Application.Events;

namespace MagentaTV.Application.Commands
{
    public class CreateSessionCommandHandler : IRequestHandler<CreateSessionCommand, ApiResponse<SessionCreatedDto>>
    {
        private readonly ISessionManager _sessionManager;
        private readonly IMediator _mediator;
        private readonly ILogger<CreateSessionCommandHandler> _logger;

        public CreateSessionCommandHandler(
            ISessionManager sessionManager,
            IMediator mediator,
            ILogger<CreateSessionCommandHandler> logger)
        {
            _sessionManager = sessionManager;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<ApiResponse<SessionCreatedDto>> Handle(CreateSessionCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var sessionId = await _sessionManager.CreateSessionAsync(
                    request.Request, request.IpAddress, request.UserAgent);

                var response = new SessionCreatedDto
                {
                    SessionId = sessionId,
                    Message = "Session created successfully",
                    ExpiresAt = DateTime.UtcNow.AddHours(
                        request.Request.RememberMe ? 720 : request.Request.SessionDurationHours ?? 8)
                };

                // Publikujeme event
                await _mediator.Publish(new UserLoggedInEvent
                {
                    Username = request.Request.Username,
                    SessionId = sessionId,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = request.IpAddress
                }, cancellationToken);

                _logger.LogInformation("Session created successfully for user {Username}: {SessionId}",
                    request.Request.Username, sessionId);

                return ApiResponse<SessionCreatedDto>.SuccessResult(response, "Přihlášení proběhlo úspěšně");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Session creation failed - unauthorized: {Message}", ex.Message);
                return ApiResponse<SessionCreatedDto>.ErrorResult("Invalid credentials",
                    new List<string> { "Neplatné přihlašovací údaje" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create session for user {Username}", request.Request.Username);
                return ApiResponse<SessionCreatedDto>.ErrorResult("Internal server error",
                    new List<string> { "Došlo k chybě při vytváření session" });
            }
        }
    }
}