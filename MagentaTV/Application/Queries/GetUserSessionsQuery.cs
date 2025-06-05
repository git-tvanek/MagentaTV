using MagentaTV.Models;
using MagentaTV.Models.Session;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GetUserSessionsQuery : IRequest<ApiResponse<List<SessionDto>>>
    {
        public string CurrentSessionId { get; set; } = string.Empty;
    }
}