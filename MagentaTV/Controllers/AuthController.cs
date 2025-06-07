using MagentaTV.Application.Commands;
using MagentaTV.Models;
using MagentaTV.Models.Session;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MagentaTV.Extensions;
using MagentaTV.Services.Security;

namespace MagentaTV.Controllers
{
    [ApiController]
    [Route("auth")]
    /// <summary>
    /// Handles authentication related endpoints such as logging in and out.
    /// All responses are returned using the <see cref="ApiResponse{T}"/> wrapper.
    /// </summary>
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AuthController> _logger;
        private readonly IInputSanitizer _sanitizer;

        public AuthController(IMediator mediator, ILogger<AuthController> logger, IInputSanitizer sanitizer)
        {
            _mediator = mediator;
            _logger = logger;
            _sanitizer = sanitizer;
        }

        /// <summary>
        /// Central login endpoint that validates user credentials and creates
        /// a new application session when successful.
        /// </summary>
        [HttpPost("login")]
        [ProducesResponseType(typeof(ApiResponse<SessionCreatedDto>), 200)]
        [ProducesResponseType(typeof(ApiResponse<string>), 400)]
        [ProducesResponseType(typeof(ApiResponse<string>), 401)]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(ApiResponse<string>.ErrorResult("Validation failed", errors));
            }

            var sanitizedUser = _sanitizer.Sanitize(loginDto.Username);

            using var securePassword = loginDto.GetSecurePassword();

            var command = new LoginCommand
            {
                Username = sanitizedUser,
                Password = securePassword,
                RememberMe = loginDto.RememberMe,
                SessionDurationHours = loginDto.SessionDurationHours,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                UserAgent = HttpContext.Request.Headers.UserAgent.ToString()
            };

            var result = await _mediator.Send(command);

            if (result.Success && result.Data != null)
            {
                SessionCookieHelper.SetSessionCookie(Response, result.Data.SessionId, true);
                return Ok(result);
            }

            return result.Message?.Contains("Invalid credentials") == true
                ? Unauthorized(result)
                : StatusCode(500, result);
        }

        /// <summary>
        /// Terminates the current session and removes the session cookie from
        /// the client.
        /// </summary>
        [HttpPost("logout")]
        [ProducesResponseType(typeof(ApiResponse<string>), 200)]
        public async Task<IActionResult> Logout()
        {
            var sessionId = SessionCookieHelper.GetSessionId(Request);
            var command = new LogoutCommand { SessionId = sessionId };
            var result = await _mediator.Send(command);

            if (result.Success)
            {
                SessionCookieHelper.RemoveSessionCookie(Response);
            }

            return Ok(result);
        }

        
    }
}