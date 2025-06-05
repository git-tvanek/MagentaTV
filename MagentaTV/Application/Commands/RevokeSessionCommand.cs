using MagentaTV.Models;
using MediatR;

namespace MagentaTV.Application.Commands
{
    public class RevokeSessionCommand : IRequest<ApiResponse<string>>
    {
        public string CurrentSessionId { get; set; } = string.Empty;
        public string TargetSessionId { get; set; } = string.Empty;
    }
}