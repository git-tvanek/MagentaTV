using MagentaTV.Application.Commands;
using MagentaTV.Models;
using MagentaTV.Models.Session;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MagentaTV.Extensions;

namespace MagentaTV.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IMediator mediator, ILogger<AuthController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        /// <summary>
        /// Unified login endpoint - ověří credentials + vytvoří session
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

            var command = new LoginCommand
            {
                Username = loginDto.Username,
                Password = loginDto.Password,
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
        /// Logout endpoint
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