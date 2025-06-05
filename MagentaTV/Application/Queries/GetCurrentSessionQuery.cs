using MagentaTV.Models;
using MagentaTV.Models.Session;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GetCurrentSessionQuery : IRequest<ApiResponse<SessionInfoDto>>
    {
        public string SessionId { get; set; } = string.Empty;
    }
}