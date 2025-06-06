using MagentaTV.Application.Commands;
using MagentaTV.Models;
using MagentaTV.Models.Session;
using MediatR;
using Microsoft.AspNetCore.Mvc;

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
                SetSessionCookie(result.Data.SessionId);
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
            var sessionId = GetSessionIdFromRequest();
            var command = new LogoutCommand { SessionId = sessionId };
            var result = await _mediator.Send(command);

            if (result.Success)
            {
                RemoveSessionCookie();
            }

            return Ok(result);
        }

        #region Cookie Helper Methods

        private string? GetSessionIdFromRequest()
        {
            // Cookie má prioritu
            if (Request.Cookies.TryGetValue("SessionId", out var cookieValue))
            {
                return cookieValue;
            }

            // Fallback na Authorization header
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Session "))
            {
                return authHeader.Substring("Session ".Length);
            }

            return null;
        }

        private void SetSessionCookie(string sessionId)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // HTTPS only v produkci  
                SameSite = SameSiteMode.Strict,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(30) // Cookie expiruje později než session
            };

            Response.Cookies.Append("SessionId", sessionId, cookieOptions);
        }

        private void RemoveSessionCookie()
        {
            Response.Cookies.Delete("SessionId");
        }

        #endregion
    }
}