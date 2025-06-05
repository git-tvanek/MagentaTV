using MagentaTV.Models;
using MediatR;

namespace MagentaTV.Application.Commands
{
    public class SessionLogoutCommand : IRequest<ApiResponse<string>>
    {
        public string? SessionId { get; set; }
    }
}