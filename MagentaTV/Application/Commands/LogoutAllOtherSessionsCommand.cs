using MagentaTV.Models;
using MediatR;

namespace MagentaTV.Application.Commands
{
    public class LogoutAllOtherSessionsCommand : IRequest<ApiResponse<string>>
    {
        public string CurrentSessionId { get; set; } = string.Empty;
    }
}