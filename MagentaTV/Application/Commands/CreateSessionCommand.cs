using MagentaTV.Models;
using MagentaTV.Models.Session;
using MediatR;

namespace MagentaTV.Application.Commands
{
    public class CreateSessionCommand : IRequest<ApiResponse<SessionCreatedDto>>
    {
        public CreateSessionRequest Request { get; set; } = null!;
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
    }
}