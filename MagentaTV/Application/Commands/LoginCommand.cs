using MagentaTV.Models;
using MagentaTV.Models.Session;
using MediatR;

namespace MagentaTV.Application.Commands
{
    public class LoginCommand : IRequest<ApiResponse<SessionCreatedDto>>
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; } = false;
        public int? SessionDurationHours { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
    }
}
