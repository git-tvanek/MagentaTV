using MagentaTV.Models;
using MagentaTV.Services.Session;
using MediatR;

namespace MagentaTV.Application.Queries
{
    public class GetSessionStatisticsQuery : IRequest<ApiResponse<SessionStatistics>>
    {
    }
}