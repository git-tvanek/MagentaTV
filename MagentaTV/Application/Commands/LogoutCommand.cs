using MagentaTV.Models;
using MediatR;

namespace MagentaTV.Application.Commands
{
    public class LogoutCommand : IRequest<ApiResponse<string>>
    {
        public string? SessionId { get; set; }
    }
}